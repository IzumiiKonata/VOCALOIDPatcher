using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Jobs;

public static class JobTools
{
    private static readonly PropertyInfo? RelTickValueProp =
        typeof(VSMRelTick).GetProperty("Value") ?? typeof(VSMRelTick).GetProperty("Tick");

    private static readonly PropertyInfo? NoteRelPosProp =
        typeof(WIVSMNote).GetProperty("RelPosTick") ?? typeof(WIVSMNote).GetProperty("RelPosition");

    private static readonly PropertyInfo? NoteDurationProp =
        typeof(WIVSMNote).GetProperty("DurationTick") ?? typeof(WIVSMNote).GetProperty("Duration");

    private static readonly MethodInfo? NoteSetDurationMethod =
        typeof(WIVSMNote).GetMethod("SetDuration");

    private static readonly PropertyInfo? AbsTickValueProp =
        typeof(VSMAbsTick).GetProperty("Value") ?? typeof(VSMAbsTick).GetProperty("Tick");

    private static readonly PropertyInfo? PartAbsProp =
        typeof(WIVSMMidiPart).GetProperty("AbsPosTick") ?? typeof(WIVSMMidiPart).GetProperty("AbsPosition");

    private static readonly PropertyInfo? PartDurProp =
        typeof(WIVSMMidiPart).GetProperty("DurationTick") ?? typeof(WIVSMMidiPart).GetProperty("Duration");

    private static bool TryGetContext(out WIVSMSequence vsm, out WIVSMMidiPart part, out List<WIVSMNote> notes)
    {
        vsm = null!;
        part = null!;
        notes = new List<WIVSMNote>();

        var sequence = App.Shared?.Document?.Sequence;
        var vsmSequence = sequence?.VSMSequence;
        var activePart = sequence?.ActiveMidiPart;
        if (vsmSequence == null || activePart == null)
            return false;

        var target = activePart.HasSelectedNote ? activePart.SelectedNotes : activePart.Notes;
        if (target == null || target.Count == 0)
            return false;

        vsm = vsmSequence;
        part = activePart;
        notes = target;
        return true;
    }

    private static int Resolution => Yamaha.VOCALOID.Design.Sequence.resolution;

    private static long RelTicks(object? relTick)
        => relTick == null || RelTickValueProp == null ? 0L : Convert.ToInt64(RelTickValueProp.GetValue(relTick));

    private static long RelStart(WIVSMNote note) => RelTicks(NoteRelPosProp?.GetValue(note));

    private static long DurationOf(WIVSMNote note) => RelTicks(NoteDurationProp?.GetValue(note));

    private static long AbsTicks(object? absTick)
        => absTick == null || AbsTickValueProp == null ? 0L : Convert.ToInt64(AbsTickValueProp.GetValue(absTick));

    private static long PartAbs(WIVSMMidiPart part) => AbsTicks(PartAbsProp?.GetValue(part));

    private static long PartDur(WIVSMMidiPart part) => RelTicks(PartDurProp?.GetValue(part));

    private static bool SetNoteDuration(WIVSMNote note, int ticks)
    {
        if (ticks < 1)
            ticks = 1;
        if (NoteSetDurationMethod == null)
            return false;

        var paramType = NoteSetDurationMethod.GetParameters()[0].ParameterType;
        object arg = paramType == typeof(int) ? ticks : new VSMRelTick(ticks);
        return NoteSetDurationMethod.Invoke(note, new[] { arg }) is true;
    }

    public static void ApplySwing(int subdivision, double ratio)
    {
        if (!TryGetContext(out var vsm, out var part, out var notes))
            return;

        ratio = Math.Clamp(ratio, 1.0, 99.0);
        int unit = Math.Max(1, Resolution * 4 / subdivision);
        int pair = unit * 2;

        try
        {
            using var transaction = new Transaction(vsm);
            transaction.Result = true;

            foreach (var note in notes.OrderByDescending(RelStart))
            {
                long start = RelStart(note);
                long end = start + DurationOf(note);
                int newStart = (int)Math.Round(MapTick(start, unit, pair, ratio));
                int newEnd = (int)Math.Round(MapTick(end, unit, pair, ratio));
                int newDur = Math.Max(1, newEnd - newStart);

                if (newStart < 0)
                    continue;

                if (part.MoveNote(new VSMRelTick(newStart), note))
                    SetNoteDuration(note, newDur);
            }
        }
        catch (Exception e)
        {
            Debug.Print($"[Job] Swing 失败: {e.Message}");
        }

        ShowOtherTracksNotesPatch.RefreshPianoroll();
    }

    private static double MapTick(long tick, int unit, int pair, double ratio)
    {
        long pairBase = tick / pair * pair;
        long o = tick - pairBase;
        double mapped = o < unit
            ? o * ratio / 50.0
            : unit * ratio / 50.0 + (o - unit) * (100.0 - ratio) / 50.0;
        return pairBase + mapped;
    }

    public static void ApplyLyric(string syllable)
    {
        if (string.IsNullOrEmpty(syllable) || !TryGetContext(out var vsm, out _, out var notes))
            return;

        try
        {
            using var transaction = new Transaction(vsm);
            transaction.Result = true;
            foreach (var note in notes)
                note.Lyric = syllable;
        }
        catch (Exception e)
        {
            Debug.Print($"[Job] 歌词替换失败: {e.Message}");
        }

        ShowOtherTracksNotesPatch.RefreshPianoroll();
    }

    public static void ApplyQuantizeLength(int gridTicks, double strength)
    {
        if (gridTicks < 1 || !TryGetContext(out var vsm, out _, out var notes))
            return;

        strength = Math.Clamp(strength, 0.0, 1.0);

        try
        {
            using var transaction = new Transaction(vsm);
            transaction.Result = true;
            foreach (var note in notes)
            {
                long dur = DurationOf(note);
                long quantized = Math.Max(gridTicks, (long)Math.Round((double)dur / gridTicks) * gridTicks);
                int newDur = (int)Math.Round(dur + (quantized - dur) * strength);
                SetNoteDuration(note, newDur);
            }
        }
        catch (Exception e)
        {
            Debug.Print($"[Job] 量化时值失败: {e.Message}");
        }

        ShowOtherTracksNotesPatch.RefreshPianoroll();
    }

    public enum HarmonyInterval
    {
        OctaveDown,
        ThirdDown,
        ThirdUp,
        FourthUp,
        FifthUp,
        SixthUp,
        OctaveUp
    }

    private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };

    public static void ApplyHarmony(int rootId, IReadOnlyList<HarmonyInterval> intervals, bool forceNewTrack)
    {
        if (intervals == null || intervals.Count == 0)
            return;

        var sequence = App.Shared?.Document?.Sequence;
        var vsm = sequence?.VSMSequence;
        var sourcePart = sequence?.ActiveMidiPart;
        if (vsm == null || sourcePart == null)
            return;

        bool hasSelection = sourcePart.HasSelectedNote;
        var sourceNotes = hasSelection ? sourcePart.SelectedNotes : sourcePart.Notes;
        if (sourceNotes == null || sourceNotes.Count == 0)
            return;

        HashSet<(long, int)>? keep = hasSelection
            ? new HashSet<(long, int)>(sourceNotes.Select(n => (RelStart(n), n.NoteNumber)))
            : null;

        try
        {
            using var transaction = new Transaction(vsm);
            transaction.Result = true;

            long srcAbs = PartAbs(sourcePart);
            long srcEnd = srcAbs + PartDur(sourcePart);

            foreach (var interval in intervals)
            {
                var target = forceNewTrack
                    ? CreateHarmonyTrack(vsm, sourcePart)
                    : FindFreeTrack(vsm, sourcePart, srcAbs, srcEnd) ?? CreateHarmonyTrack(vsm, sourcePart);
                if (target == null)
                {
                    Debug.Print("[Job] 和声: 无空闲轨道且无法新建");
                    break;
                }

                var harmony = target.DuplicatePart(new VSMAbsTick((int)srcAbs), sourcePart);
                if (harmony == null)
                    continue;

                foreach (var note in harmony.Notes.ToList())
                {
                    int num = note.NoteNumber;
                    if (keep != null && !keep.Contains((RelStart(note), num)))
                    {
                        harmony.RemoveNote(note);
                        continue;
                    }

                    note.SetNoteNumber(Math.Clamp(Transpose(num, rootId, interval), 0, 127));
                }
            }
        }
        catch (Exception e)
        {
            Debug.Print($"[Job] 和声失败: {e.Message}");
        }

        ShowOtherTracksNotesPatch.RefreshPianoroll();
    }

    private static int Transpose(int noteNum, int rootId, HarmonyInterval interval) => interval switch
    {
        HarmonyInterval.OctaveDown => noteNum - 12,
        HarmonyInterval.OctaveUp => noteNum + 12,
        HarmonyInterval.ThirdDown => DiatonicShift(noteNum, rootId, -2),
        HarmonyInterval.ThirdUp => DiatonicShift(noteNum, rootId, 2),
        HarmonyInterval.FourthUp => DiatonicShift(noteNum, rootId, 3),
        HarmonyInterval.FifthUp => DiatonicShift(noteNum, rootId, 4),
        HarmonyInterval.SixthUp => DiatonicShift(noteNum, rootId, 5),
        _ => noteNum
    };

    private static int DiatonicShift(int noteNum, int rootId, int degrees)
    {
        int rel = ((noteNum - rootId) % 12 + 12) % 12;
        int octave = (int)Math.Floor((noteNum - rootId) / 12.0);
        int index = NearestScaleIndex(rel);
        int newIndex = index + degrees;
        int newOctave = octave + (int)Math.Floor(newIndex / 7.0);
        int newDegree = ((newIndex % 7) + 7) % 7;
        return rootId + newOctave * 12 + MajorScale[newDegree];
    }

    private static int NearestScaleIndex(int rel)
    {
        int best = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < MajorScale.Length; i++)
        {
            int distance = Math.Abs(MajorScale[i] - rel);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private static WIVSMMidiTrack? FindFreeTrack(WIVSMSequence vsm, WIVSMMidiPart sourcePart, long lo, long hi)
    {
        foreach (var track in vsm.MidiTracks)
        {
            if (track.HasPart(sourcePart))
                continue;
            bool overlaps = track.MidiParts.Any(p =>
            {
                long a = PartAbs(p);
                return a < hi && a + PartDur(p) > lo;
            });
            if (!overlaps)
                return track;
        }

        return null;
    }

    private static WIVSMMidiTrack? CreateHarmonyTrack(WIVSMSequence vsm, WIVSMMidiPart sourcePart)
    {
        if (vsm.NumTrack >= vsm.MaxNumTrack)
            return null;

        var type = vsm.MidiTracks.FirstOrDefault(t => t.HasPart(sourcePart))?.Type ?? VSMTrackType.MidiAi;
        string name = TranslationManager.Get("VOCALOIDPatcher_Job_Harmony_TrackName") ?? "Harmony";
        return vsm.InsertTrackEx(vsm.NumTrack, type, name) as WIVSMMidiTrack;
    }

}
