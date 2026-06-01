using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Patch.Patches;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VDM;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Formats;

public static class V6Bridge
{
    private static readonly PropertyInfo? RelTickValue =
        typeof(VSMRelTick).GetProperty("Value") ?? typeof(VSMRelTick).GetProperty("Tick");

    private static readonly PropertyInfo? AbsTickValue =
        typeof(VSMAbsTick).GetProperty("Value") ?? typeof(VSMAbsTick).GetProperty("Tick");

    private static readonly PropertyInfo? NoteRelPos =
        typeof(WIVSMNote).GetProperty("RelPosTick") ?? typeof(WIVSMNote).GetProperty("RelPosition");

    private static readonly PropertyInfo? NoteDuration =
        typeof(WIVSMNote).GetProperty("DurationTick") ?? typeof(WIVSMNote).GetProperty("Duration");

    private static readonly PropertyInfo? PartAbsPos =
        typeof(WIVSMMidiPart).GetProperty("AbsPosTick") ?? typeof(WIVSMMidiPart).GetProperty("AbsPosition");

    private static readonly PropertyInfo? TempoRelPos =
        typeof(WIVSMTempo).GetProperty("RelPosTick") ?? typeof(WIVSMTempo).GetProperty("RelPosition");

    private static readonly PropertyInfo? TrackName = typeof(WIVSMMidiTrack).GetProperty("Name");

    private static readonly MethodInfo? ResetPartPhonemesMethod =
        typeof(WIVSMNote).Assembly.GetType("Yamaha.VOCALOID.G2PAMultiLingualManager")
            ?.GetMethod("ResetPhonemes", new[] { typeof(WIVSMMidiPart) });

    private static long Unwrap(object? tickStruct) =>
        tickStruct == null || RelTickValue == null ? 0L : Convert.ToInt64(RelTickValue.GetValue(tickStruct));

    private static long UnwrapAbs(object? tickStruct) =>
        tickStruct == null || AbsTickValue == null ? 0L : Convert.ToInt64(AbsTickValue.GetValue(tickStruct));

    private static long NoteOn(WIVSMNote note) => Unwrap(NoteRelPos?.GetValue(note));

    private static long NoteLen(WIVSMNote note) => Unwrap(NoteDuration?.GetValue(note));

    private static long PartAbs(WIVSMMidiPart part) => UnwrapAbs(PartAbsPos?.GetValue(part));

    private static long TempoTick(WIVSMTempo tempo) => Unwrap(TempoRelPos?.GetValue(tempo));

    private static int VoiceBankLangId(WIVSMMidiPart part, bool isAi)
    {
        string member = isAi ? "NativeLangIDFromAiVoiceBank" : "NativeLangIDFromVoiceBank";
        try
        {
            var prop = part.GetType().GetProperty(member);
            if (prop != null)
                return Convert.ToInt32(prop.GetValue(part));

            var ext = typeof(WIVSMMidiPart).Assembly
                .GetType("Yamaha.VOCALOID.WIVSMMidiPartExtension")?.GetMethod(member);
            if (ext != null)
                return Convert.ToInt32(ext.Invoke(null, new object[] { part }));
        }
        catch
        {
            // ignore
        }

        return -1;
    }

    private static void ResetPartPhonemes(WIVSMMidiPart part)
    {
        try
        {
            ResetPartPhonemesMethod?.Invoke(null, new object[] { part });
        }
        catch
        {
            // ignore
        }
    }

    private static bool TryGetSequence(out WIVSMSequence vsm)
    {
        vsm = null!;
        var sequence = App.Shared?.Document?.Sequence?.VSMSequence;
        if (sequence == null)
            return false;
        vsm = sequence;
        return true;
    }

    public static void Import(Project project)
    {
        if (!TryGetSequence(out var vsm))
            return;

        var validated = project.RequireValid();

        var trackType = VSMTrackType.Midi;
        bool isAi = false;

        var db = App.DatabaseManager;
        VoiceBank? voiceBank = db != null && db.NumVoiceBanks > 0 ? db.GetVoiceBankByIndex(0) : null;
        voiceBank ??= db?.DefaultVoiceBank;
        string sourceVoiceBankId = voiceBank?.CompID ?? string.Empty;
        string sourceAiVoiceBankId = db?.DefaultAiVoiceBank?.CompID ?? string.Empty;

        using var transaction = new Transaction(vsm);
        transaction.Result = true;

        foreach (var timeSignature in validated.TimeSignatures)
        {
            if (timeSignature.MeasurePosition == 0)
                continue;
            vsm.InsertTimeSig(timeSignature.MeasurePosition, new VSMTimeSigEvent(timeSignature.Numerator, timeSignature.Denominator));
        }

        foreach (var tempo in validated.Tempos)
        {
            int value = Math.Clamp((int)Math.Round(tempo.Bpm * 100), WIVSMTempo.MinValue, WIVSMTempo.MaxValue);

            if (tempo.TickPosition == 0)
            {
                var firstTempo = vsm.Tempos.FirstOrDefault(t => TempoTick(t) == 0) ?? vsm.Tempos.FirstOrDefault();
                if (firstTempo != null)
                    firstTempo.Value = value;
                else
                    vsm.InsertTempo(new VSMRelTick(0), value);
                vsm.GlobalTempo = value;
                continue;
            }

            vsm.InsertTempo(new VSMRelTick((int)tempo.TickPosition), value);
        }

        foreach (var track in validated.Tracks)
        {
            if (vsm.NumTrack >= vsm.MaxNumTrack)
                break;

            if (vsm.InsertTrackEx(vsm.NumTrack, trackType, track.Name) is not WIVSMMidiTrack v6Track)
                continue;

            long span = track.Notes.Count > 0 ? track.Notes.Max(n => n.TickOff) : Constants.TicksInFullNote;
            if (v6Track.InsertPart(new VSMAbsTick(0), new VSMRelTick((int)span), track.Name) is not { } part)
                continue;

            if (!string.IsNullOrEmpty(sourceAiVoiceBankId))
                part.SetAiVoiceBankID(sourceAiVoiceBankId);
            if (!string.IsNullOrEmpty(sourceVoiceBankId))
                part.SetVoiceBankID(sourceVoiceBankId);

            var noteExpression = part.GetDefaultNoteExpression();
            var aiNoteExpression = part.GetDefaultAiNoteExpression();

            int langId = VoiceBankLangId(part, isAi);
            if (langId < 0)
                langId = part.LangID;

            string defaultLyric = string.Empty;
            string defaultPhoneme = string.Empty;
            bool hasDefault = langId >= 0
                && DefaultLyricManager.GetUserSettingDefaultLyric((VSMLanguageID)langId, out defaultLyric, out defaultPhoneme)
                && !string.IsNullOrEmpty(defaultPhoneme);

            int insertedInPart = 0;
            foreach (var note in track.Notes)
            {
                string lyric = string.IsNullOrEmpty(note.Lyric) ? Constants.DefaultLyric : note.Lyric;
                var noteEvent = new VSMNoteEvent((int)note.Length, Math.Clamp(note.Key, 0, 127), 64);
                var relPos = new VSMRelTick((int)note.TickOn);

                WIVSMNote? inserted = hasDefault
                    ? part.InsertNote(relPos, noteEvent, noteExpression, aiNoteExpression, lyric, defaultPhoneme, true, langId)
                    : part.InsertNote(relPos, noteEvent, noteExpression, aiNoteExpression, lyric, "", false, langId);

                if (inserted != null)
                    insertedInPart++;
            }

            if (hasDefault && insertedInPart > 0)
                ResetPartPhonemes(part);
        }

        ShowOtherTracksNotesPatch.RefreshPianoroll();
    }

    public static Project Export()
    {
        if (!TryGetSequence(out var vsm))
            throw new InvalidOperationException("No active sequence.");

        var tracks = new List<Track>();
        int trackIndex = 0;
        foreach (var v6Track in vsm.MidiTracks)
        {
            var notes = new List<Note>();
            int noteIndex = 0;
            foreach (var part in v6Track.MidiParts)
            {
                long partAbs = PartAbs(part);
                foreach (var note in part.Notes)
                {
                    long on = partAbs + NoteOn(note);
                    long off = on + NoteLen(note);
                    notes.Add(new Note(noteIndex++, note.NoteNumber, note.Lyric ?? Constants.DefaultLyric, on, off));
                }
            }

            string name = TrackName?.GetValue(v6Track) as string ?? $"Track {trackIndex + 1}";
            tracks.Add(new Track(trackIndex++, name, notes));
        }

        var tempos = vsm.Tempos.Select(t => new Tempo(TempoTick(t), t.Value / 100.0)).ToList();
        if (tempos.Count == 0)
            tempos.Add(Tempo.Default);

        var timeSignatures = vsm.TimeSigs.Select(t => new TimeSignature(t.PosBar, t.Numer, t.Denom)).ToList();
        if (timeSignatures.Count == 0)
            timeSignatures.Add(TimeSignature.Default);

        var sequence = App.Shared?.Document?.Sequence;
        string projectName = sequence?.GetType().GetProperty("Name")?.GetValue(sequence) as string ?? "Untitled";
        return new Project(
            Format.UfData,
            new List<ImportFile>(),
            projectName,
            tracks,
            timeSignatures,
            tempos,
            0,
            new List<ImportWarning>());
    }
}
