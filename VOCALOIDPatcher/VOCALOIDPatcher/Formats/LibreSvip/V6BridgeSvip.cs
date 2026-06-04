using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Patch.Patches;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VDM;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Formats.LibreSvip;

public static class V6BridgeSvip
{
    private const int TicksInFullNote = Core.Constants.TicksInBeat * 4;
    private const string DefaultLyric = "あ";

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

    private static readonly PropertyInfo? TrackNameProp = typeof(WIVSMMidiTrack).GetProperty("Name");

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

    private static Project RequireValid(Project project)
    {
        for (int index = 0; index < project.TrackList.Count; index++)
        {
            if (project.TrackList[index] is not SingingTrack track)
                continue;
            var firstNote = track.NoteList.FirstOrDefault();
            if (firstNote != null && firstNote.StartPos < 0)
                throw new InvalidOperationException($"轨道 {index} 的首个音符位置为负: {firstNote.StartPos}");
        }
        return project;
    }

    public static void Import(Project project)
    {
        if (!TryGetSequence(out var vsm))
            return;

        RequireValid(project);

        var trackType = VSMTrackType.Midi;
        bool isAi = false;

        var db = App.DatabaseManager;
        VoiceBank? voiceBank = db != null && db.NumVoiceBanks > 0 ? db.GetVoiceBankByIndex(0) : null;
        voiceBank ??= db?.DefaultVoiceBank;
        string sourceVoiceBankId = voiceBank?.CompID ?? string.Empty;
        string sourceAiVoiceBankId = db?.DefaultAiVoiceBank?.CompID ?? string.Empty;

        using var transaction = new Transaction(vsm);
        transaction.Result = true;

        foreach (var timeSignature in project.TimeSignatureList)
        {
            if (timeSignature.BarIndex == 0)
                continue;
            vsm.InsertTimeSig(timeSignature.BarIndex, new VSMTimeSigEvent(timeSignature.Numerator, timeSignature.Denominator));
        }

        foreach (var tempo in project.SongTempoList)
        {
            int value = Math.Clamp((int)Math.Round(tempo.Bpm * 100), WIVSMTempo.MinValue, WIVSMTempo.MaxValue);

            if (tempo.Position == 0)
            {
                var firstTempo = vsm.Tempos.FirstOrDefault(t => TempoTick(t) == 0) ?? vsm.Tempos.FirstOrDefault();
                if (firstTempo != null)
                    firstTempo.Value = value;
                else
                    vsm.InsertTempo(new VSMRelTick(0), value);
                vsm.GlobalTempo = value;
                continue;
            }

            vsm.InsertTempo(new VSMRelTick(tempo.Position), value);
        }

        foreach (var track in project.TrackList.OfType<SingingTrack>())
        {
            if (vsm.NumTrack >= vsm.MaxNumTrack)
                break;

            if (vsm.InsertTrackEx(vsm.NumTrack, trackType, track.Title) is not WIVSMMidiTrack v6Track)
                continue;

            long span = track.NoteList.Count > 0 ? track.NoteList.Max(n => (long)n.EndPos) : TicksInFullNote;
            if (v6Track.InsertPart(new VSMAbsTick(0), new VSMRelTick((int)span), track.Title) is not { } part)
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

            string defaultPhoneme = string.Empty;
            bool hasDefault = langId >= 0
                && DefaultLyricManager.GetUserSettingDefaultLyric((VSMLanguageID)langId, out _, out defaultPhoneme)
                && !string.IsNullOrEmpty(defaultPhoneme);

            int insertedInPart = 0;
            foreach (var note in track.NoteList)
            {
                string lyric = string.IsNullOrEmpty(note.Lyric) ? DefaultLyric : note.Lyric;
                var noteEvent = new VSMNoteEvent(note.Length, Math.Clamp(note.KeyNumber, 0, 127), 64);
                var relPos = new VSMRelTick(note.StartPos);

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
            foreach (var part in v6Track.MidiParts)
            {
                long partAbs = PartAbs(part);
                foreach (var note in part.Notes)
                {
                    long on = partAbs + NoteOn(note);
                    long off = on + NoteLen(note);
                    notes.Add(new Note
                    {
                        StartPos = (int)on,
                        Length = (int)(off - on),
                        KeyNumber = note.NoteNumber,
                        Lyric = note.Lyric ?? DefaultLyric,
                    });
                }
            }

            string name = TrackNameProp?.GetValue(v6Track) as string ?? $"Track {trackIndex + 1}";
            tracks.Add(new SingingTrack { Title = name, NoteList = notes });
            trackIndex++;
        }

        var tempos = vsm.Tempos.Select(t => new SongTempo((int)TempoTick(t), t.Value / 100.0)).ToList();
        if (tempos.Count == 0)
            tempos.Add(new SongTempo());

        var timeSignatures = vsm.TimeSigs.Select(t => new TimeSignature(t.PosBar, t.Numer, t.Denom)).ToList();
        if (timeSignatures.Count == 0)
            timeSignatures.Add(new TimeSignature());

        return new Project
        {
            SongTempoList = tempos,
            TimeSignatureList = timeSignatures,
            TrackList = tracks,
        };
    }
}
