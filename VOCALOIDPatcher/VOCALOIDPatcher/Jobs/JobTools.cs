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
    private static readonly Random Rng = new();

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

    private static readonly PropertyInfo? ControllerRelProp =
        typeof(WIVSMMidiController).GetProperty("RelPosTick") ?? typeof(WIVSMMidiController).GetProperty("RelPosition");

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

    private static long ControllerRel(WIVSMMidiController controller) => RelTicks(ControllerRelProp?.GetValue(controller));

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

    public static void ApplyHumanize(int timingTicks, double durationPercent, int velocityAmount)
    {
        if (!TryGetContext(out var vsm, out var part, out var notes))
            return;

        try
        {
            using var transaction = new Transaction(vsm);
            transaction.Result = true;

            foreach (var note in notes)
            {
                if (velocityAmount > 0)
                    note.NoteVelocity = Math.Clamp(note.NoteVelocity + RandSigned(velocityAmount), 0, 127);

                if (durationPercent > 0)
                {
                    long dur = DurationOf(note);
                    double factor = 1.0 + RandSignedDouble(durationPercent / 100.0);
                    SetNoteDuration(note, (int)Math.Round(dur * factor));
                }

                if (timingTicks > 0)
                {
                    long start = RelStart(note);
                    int newStart = (int)Math.Max(0, start + RandSigned(timingTicks));
                    part.MoveNote(new VSMRelTick(newStart), note);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Print($"[Job] Humanize 失败: {e.Message}");
        }

        ShowOtherTracksNotesPatch.RefreshPianoroll();
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

    public static void ApplyDynEnvelope(int initialLevel, int attackLevel, int attackTime, int holdTime,
        int decayTime, int sustainLevel, int fadeLevel, int releaseLevel, int releaseTime)
    {
        if (!TryGetContext(out var vsm, out var part, out var notes))
            return;

        const int defaultDyn = 64;
        int step = Math.Max(1, Resolution / 48);
        int paramTime = attackTime + holdTime + decayTime + releaseTime;

        try
        {
            using var transaction = new Transaction(vsm);
            transaction.Result = true;

            long lastEnd = -1;
            foreach (var note in notes.OrderBy(RelStart))
            {
                long start = RelStart(note);
                long end = start + DurationOf(note);
                RemoveControllersInRange(part, VSMControllerType.Dynamics, start, end);

                if (end - start < paramTime)
                {
                    InsertDyn(part, start, defaultDyn);
                    lastEnd = end;
                    continue;
                }

                long tA = start + attackTime;
                long tH = tA + holdTime;
                long tD = tH + decayTime;
                long tR = end - releaseTime;
                RampDyn(part, start, tA, initialLevel, attackLevel, step);
                RampDyn(part, tA, tH, attackLevel, attackLevel, step);
                RampDyn(part, tH, tD, attackLevel, sustainLevel, step);
                RampDyn(part, tD, tR, sustainLevel, fadeLevel, step);
                RampDyn(part, tR, end, fadeLevel, releaseLevel, step);
                InsertDyn(part, end, releaseLevel);
                lastEnd = end;
            }

            if (lastEnd >= 0)
                InsertDyn(part, lastEnd, defaultDyn);
        }
        catch (Exception e)
        {
            Debug.Print($"[Job] 动态包络失败: {e.Message}");
        }

        ShowOtherTracksNotesPatch.RefreshPianoroll();
    }

    private static void RampDyn(WIVSMMidiPart part, long start, long end, int vStart, int vEnd, int step)
    {
        if (end <= start)
            return;
        for (long t = start; t < end; t += step)
        {
            double f = (double)(t - start) / (end - start);
            InsertDyn(part, t, (int)Math.Round(vStart + (vEnd - vStart) * f));
        }
    }

    private static void InsertDyn(WIVSMMidiPart part, long tick, int value)
    {
        if (tick < 0)
            return;
        part.InsertController(new VSMRelTick((int)tick), VSMControllerType.Dynamics, Math.Clamp(value, 0, 127));
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

    private static void RemoveControllersInRange(WIVSMMidiPart part, VSMControllerType type, long lo, long hi)
    {
        ulong count = part.GetNumController(type);
        var toRemove = new List<WIVSMMidiController>();
        for (ulong i = 0; i < count; i++)
        {
            var controller = part.GetController(type, i);
            if (controller == null)
                continue;
            long rel = ControllerRel(controller);
            if (rel >= lo && rel <= hi)
                toRemove.Add(controller);
        }

        foreach (var controller in toRemove)
            part.RemoveController(controller);
    }

    private static int RandSigned(int amount) => Rng.Next(-amount, amount + 1);

    private static double RandSignedDouble(double amount) => (Rng.NextDouble() * 2.0 - 1.0) * amount;
}
