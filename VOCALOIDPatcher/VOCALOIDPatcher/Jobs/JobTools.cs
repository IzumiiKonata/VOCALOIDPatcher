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

    private static readonly MethodInfo? NoteSetVibratoDurationMethod =
        typeof(WIVSMNote).GetMethod("SetVibratoDuration");

    private static readonly PropertyInfo? NoteVibratoDurProp =
        typeof(WIVSMNote).GetProperty("VibratoDurationTick") ?? typeof(WIVSMNote).GetProperty("VibratoDuration");

    private static readonly MethodInfo? PartDefaultControllerMethod =
        typeof(WIVSMMidiPart).GetMethod("DefaultControllerValue", new[] { typeof(VSMControllerType) });

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

    private struct SingerProfile
    {
        public int Absolute, Ear, Notice, Fix, Throat, Tension, Relation, PreTime, PostTime;
    }

    private static SingerProfile Profile(int singer, int skill)
    {
        static SingerProfile P(int a, int e, int n, int f, int t, int te, int r, int pre, int post) =>
            new() { Absolute = a, Ear = e, Notice = n, Fix = f, Throat = t, Tension = te, Relation = r, PreTime = pre, PostTime = post };

        return (singer, Math.Clamp(skill, 1, 4)) switch
        {
            (0, 1) => P(35, 35, 40, 45, 40, 45, 25, 35, 35),
            (0, 2) => P(65, 60, 55, 55, 60, 65, 50, 60, 55),
            (0, 3) => P(80, 75, 70, 70, 75, 85, 75, 85, 80),
            (0, 4) => P(90, 85, 90, 85, 95, 100, 95, 90, 90),
            (1, 1) => P(50, 65, 55, 50, 60, 45, 60, 65, 55),
            (1, 2) => P(65, 75, 70, 65, 75, 30, 75, 75, 60),
            (1, 3) => P(85, 85, 85, 80, 90, 15, 90, 85, 75),
            (1, 4) => P(100, 95, 100, 90, 100, 0, 100, 95, 90),
            (2, 1) => P(50, 55, 65, 45, 50, 55, 50, 65, 65),
            (2, 2) => P(70, 70, 75, 60, 65, 65, 70, 76, 75),
            (2, 3) => P(85, 85, 85, 75, 85, 75, 85, 87, 85),
            (2, 4) => P(95, 100, 95, 85, 95, 85, 100, 98, 95),
            (3, 1) => P(5, 50, 50, 45, 50, 30, 50, 40, 35),
            (3, 2) => P(30, 65, 60, 55, 65, 20, 70, 60, 55),
            (3, 3) => P(60, 75, 70, 70, 85, 10, 85, 85, 80),
            (3, 4) => P(90, 85, 80, 80, 95, 0, 100, 95, 90),
            (4, 1) => P(5, 30, 85, 30, 25, 15, 10, 30, 25),
            (4, 2) => P(30, 50, 85, 40, 40, 25, 35, 50, 50),
            (4, 3) => P(55, 70, 85, 50, 55, 35, 60, 70, 70),
            (4, 4) => P(80, 85, 85, 60, 75, 45, 85, 90, 90),
            (5, 1) => P(75, 75, 65, 45, 50, 50, 45, 50, 45),
            (5, 2) => P(85, 80, 75, 60, 65, 70, 65, 65, 60),
            (5, 3) => P(85, 85, 85, 75, 75, 85, 80, 75, 70),
            (5, 4) => P(90, 90, 95, 90, 85, 100, 90, 90, 85),
            (6, 1) => P(85, 50, 30, 45, 35, 60, 65, 55, 50),
            (6, 2) => P(90, 70, 50, 60, 60, 80, 75, 75, 70),
            (6, 3) => P(95, 85, 75, 75, 80, 90, 85, 90, 85),
            _ => P(100, 100, 95, 95, 95, 95, 95, 95, 95)
        };
    }

    public static void ApplyHumanize(int singerIndex, int skillLevel, bool vibratoOff)
    {
        if (!TryGetContext(out var vsm, out var part, out var notes))
            return;

        var ordered = notes.OrderBy(RelStart).ToList();
        var p = Profile(singerIndex, skillLevel);

        int absolutePitch = RandRange(-80.0 * (100 - p.Absolute), 80.0 * (100 - p.Absolute)) / 10;
        double detunePitch = 50.0 * (100 - p.Throat);
        double noteScale = 0.002 * p.Fix;
        double earRange = 20.0 * (100 - p.Ear);
        double holdTick = 25.0 * p.Throat;

        int bioRate = RandRange(640, 960);
        double bioPitch = RandRange(100, 150);
        double bioScale = bioPitch / ((bioRate / 2.0) * (bioRate / 2.0));
        double bioRoom = RandRange(1, bioRate - 1);
        int bioType = RandRange(1, 3);

        try
        {
            using var transaction = new Transaction(vsm);
            transaction.Result = true;

            foreach (var note in ordered)
                RandomizeExpression(note, p, vibratoOff);

            long prevEnd = long.MinValue;
            foreach (var note in ordered)
            {
                long pos = RelStart(note);
                long dur = DurationOf(note);
                int pre = RandRange(-0.5 * (100 - p.PreTime), 0.5 * (100 - p.PreTime));
                int post = RandRange(-0.5 * (100 - p.PostTime), 0.5 * (100 - p.PostTime));

                long newPos = Math.Max(0, pos + pre);
                long newDur;
                if (newPos <= prevEnd)
                {
                    newPos = prevEnd + 1;
                    int shrink = Math.Min(Math.Abs(post), (int)(dur * 0.4));
                    newDur = dur - shrink;
                }
                else
                {
                    int delta = (int)Math.Clamp(post - pre, -0.4 * dur, dur);
                    newDur = dur + delta;
                }

                if (newDur < 1)
                    newDur = 1;

                if (part.MoveNote(new VSMRelTick((int)newPos), note))
                    SetNoteDuration(note, (int)newDur);

                prevEnd = RelStart(note) + DurationOf(note);
            }

            var seg = ordered
                .Select(n => (Start: RelStart(n), Dur: DurationOf(n), Num: n.NoteNumber))
                .OrderBy(s => s.Start)
                .ToList();
            if (seg.Count == 0)
            {
                ShowOtherTracksNotesPatch.RefreshPianoroll();
                return;
            }

            long spanStart = seg[0].Start;
            long spanEnd = seg[^1].Start + seg[^1].Dur;

            var notePitch = new double[seg.Count];
            int prevNum = int.MinValue;
            for (int i = 0; i < seg.Count; i++)
            {
                notePitch[i] = seg[i].Num == prevNum
                    ? RandRange(-detunePitch * (100 - p.Tension) / 100.0, detunePitch * (100 - p.Tension) / 100.0)
                    : RandRange(-detunePitch, detunePitch);
                prevNum = seg[i].Num;
            }

            var shift = BuildPitchShift(seg, notePitch, p, detunePitch, noteScale, earRange, holdTick);

            var pitBase = ReadControllerList(part, VSMControllerType.PitchBend);
            var pbsList = ReadControllerList(part, VSMControllerType.PitchBendSens);
            int defPit = SafeDefault(part, VSMControllerType.PitchBend, 0);
            int defPbs = SafeDefault(part, VSMControllerType.PitchBendSens, 2);

            RemoveControllersInRange(part, VSMControllerType.PitchBend, spanStart, spanEnd);

            int step = Math.Max(1, (int)((spanEnd - spanStart) / 8000));
            for (long t = spanStart; t <= spanEnd; t += step)
            {
                double basePit = ValueAt(pitBase, t, defPit);
                int pbs = Math.Max(1, (int)ValueAt(pbsList, t, defPbs));
                double sp = shift.GetValueOrDefault(t, 0.0);
                double noise = Noise(bioType, bioRate, bioPitch, bioScale, bioRoom);
                double outPit = basePit + (absolutePitch + sp + noise) / pbs;
                part.InsertController(new VSMRelTick((int)t), VSMControllerType.PitchBend,
                    (int)Math.Clamp(Math.Round(outPit), -8192, 8191));

                bioRoom += step;
                if (bioRoom >= bioRate)
                {
                    bioRoom = 0;
                    bioType = RandRange(1, 3);
                }
            }

            part.InsertController(new VSMRelTick((int)(spanEnd + 1)), VSMControllerType.PitchBend,
                (int)Math.Clamp(ValueAt(pitBase, spanEnd + 1, defPit), -8192, 8191));
        }
        catch (Exception e)
        {
            Debug.Print($"[Job] 人性化失败: {e.Message}");
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

    private static Dictionary<long, double> BuildPitchShift(
        List<(long Start, long Dur, int Num)> seg, double[] notePitch, SingerProfile p,
        double detunePitch, double noteScale, double earRange, double holdTick)
    {
        var shift = new Dictionary<long, double>();
        double lastPitch = 0;

        for (int i = 0; i < seg.Count; i++)
        {
            long outPos = seg[i].Start;
            long outDur = seg[i].Dur;

            double noticeTick = 7.0 * (100 - p.Notice) * RandRange(75, 125) / 100.0;
            double earPitch = RandRange(-earRange, earRange);
            double targetPitch = earPitch + (notePitch[i] - earPitch) * (100 - p.Tension) / 100.0;
            double scale = Math.Abs(noteScale);
            if (notePitch[i] < 0)
                scale = -scale;
            double fixTick = scale == 0 ? 0 : 2.0 * Math.Sqrt(Math.Abs(notePitch[i] - targetPitch) / (2.0 * Math.Abs(scale)));

            double boundTick = outPos + noticeTick + fixTick + holdTick;
            double nextBoundTick = boundTick + RandRange(120, 960);
            double boundPitch = targetPitch;
            double nextBoundPitch = targetPitch + RandRange(-detunePitch / 4.0, detunePitch / 4.0);
            int boundTime = 1;
            double boundScale = nextBoundTick > boundTick ? (nextBoundPitch - boundPitch) / Sq(nextBoundTick - boundTick) : 0;
            int bnd = RandRange(1, 3);

            for (long t = outPos; t < outPos + outDur; t++)
            {
                double sp;
                if (t < outPos + noticeTick)
                {
                    sp = notePitch[i];
                }
                else if (t < outPos + noticeTick + fixTick / 2)
                {
                    double d = t - (outPos + noticeTick);
                    sp = notePitch[i] - d * d * scale;
                }
                else if (t < outPos + noticeTick + fixTick)
                {
                    double d = outPos + noticeTick + fixTick - t;
                    sp = targetPitch + d * d * scale;
                }
                else if (t < outPos + noticeTick + fixTick + holdTick)
                {
                    sp = targetPitch;
                }
                else
                {
                    if (t >= nextBoundTick)
                    {
                        boundTick = nextBoundTick;
                        boundPitch = nextBoundPitch;
                        boundTime++;
                        nextBoundTick = boundTick + RandRange(120, 960);
                        double amp = boundTime <= 4 ? detunePitch * 0.8 * boundTime / 4.0 : detunePitch * 0.8;
                        nextBoundPitch = targetPitch + RandRange(-amp, amp);
                        boundScale = nextBoundTick > boundTick ? (nextBoundPitch - boundPitch) / Sq(nextBoundTick - boundTick) : 0;
                        bnd = RandRange(1, 3);
                    }

                    if (bnd == 1)
                    {
                        double d = t - boundTick;
                        sp = boundPitch + d * d * boundScale;
                    }
                    else if (bnd == 2)
                    {
                        double d = nextBoundTick - t;
                        sp = nextBoundPitch - d * d * boundScale;
                    }
                    else
                    {
                        sp = nextBoundTick <= boundTick
                            ? boundPitch
                            : boundPitch + (nextBoundPitch - boundPitch) * (t - boundTick) / (nextBoundTick - boundTick);
                    }
                }

                shift[t] = sp;
                lastPitch = sp;
            }

            if (i < seg.Count - 1)
            {
                long nextPos = seg[i + 1].Start;
                long gapStart = outPos + outDur;
                if (nextPos > gapStart)
                {
                    double half = (nextPos - gapStart) / 2.0;
                    double spaceScale = (notePitch[i + 1] - lastPitch) / 2.0 / (half * half);
                    for (long t = gapStart; t < nextPos; t++)
                    {
                        double d;
                        double sp;
                        if (t < gapStart + half)
                        {
                            d = t - gapStart;
                            sp = lastPitch + d * d * spaceScale;
                        }
                        else
                        {
                            d = nextPos - t;
                            sp = notePitch[i + 1] - d * d * spaceScale;
                        }

                        shift[t] = sp;
                    }
                }
            }
        }

        return shift;
    }

    private static void RandomizeExpression(WIVSMNote note, SingerProfile p, bool vibratoOff)
    {
        if (vibratoOff)
            note.VibratoType = VSMVibratoType.None;
        else
            ScaleVibratoDuration(note, RandRange(100 - (50 - (p.PreTime + p.PostTime) / 4), 100 + (50 - (p.PreTime + p.PostTime) / 4)) / 100.0);

        try
        {
            var expr = note.GetNoteExpression();
            int spread = 50 - p.Relation / 2;
            expr.Decay = Math.Clamp((int)Math.Floor(expr.Decay * RandRange(100 - spread, 100 + spread) / 100.0), 0, 100);
            expr.Accent = Math.Clamp((int)Math.Floor(expr.Accent * RandRange(100 - spread, 100 + spread) / 100.0), 0, 100);
            note.SetNoteExpression(expr);
        }
        catch
        {
        }
    }

    private static void ScaleVibratoDuration(WIVSMNote note, double factor)
    {
        if (NoteSetVibratoDurationMethod == null)
            return;

        long cur = VibratoDurOf(note);
        if (cur <= 0)
            return;

        int target = (int)Math.Round(cur * factor);
        var paramType = NoteSetVibratoDurationMethod.GetParameters()[0].ParameterType;
        object arg = paramType == typeof(int) ? target : new VSMRelTick(target);
        NoteSetVibratoDurationMethod.Invoke(note, new[] { arg });
    }

    private static long VibratoDurOf(WIVSMNote note)
    {
        var value = NoteVibratoDurProp?.GetValue(note);
        return value switch
        {
            null => 0,
            int i => i,
            _ => RelTicks(value)
        };
    }

    private static List<(long Tick, int Value)> ReadControllerList(WIVSMMidiPart part, VSMControllerType type)
    {
        var list = new List<(long, int)>();
        ulong count = part.GetNumController(type);
        for (ulong i = 0; i < count; i++)
        {
            var controller = part.GetController(type, i);
            if (controller != null)
                list.Add((ControllerRel(controller), controller.Value));
        }

        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return list;
    }

    private static double ValueAt(List<(long Tick, int Value)> list, long tick, int fallback)
    {
        int result = fallback;
        foreach (var (t, v) in list)
        {
            if (t <= tick)
                result = v;
            else
                break;
        }

        return result;
    }

    private static int SafeDefault(WIVSMMidiPart part, VSMControllerType type, int fallback)
    {
        if (PartDefaultControllerMethod == null)
            return fallback;
        try
        {
            return PartDefaultControllerMethod.Invoke(part, new object[] { type }) is int v ? v : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static double Noise(int type, double rate, double range, double scale, double room)
    {
        double half = rate / 2.0;
        if (type == 1)
            return room < half ? -range / 2 + range / half * room : range / 2 - range / half * (room - half);
        if (type == 2)
            return room < half ? -range / 2 + room * room * scale : range / 2 - (room - half) * (room - half) * scale;
        return room < half ? range / 2 - (half - room) * (half - room) * scale : -range / 2 + (rate - room) * (rate - room) * scale;
    }

    private static double Sq(double x) => x * x;

    private static int RandRange(double a, double b)
    {
        int lo = (int)a;
        int hi = (int)b;
        if (lo > hi)
            (lo, hi) = (hi, lo);
        return Rng.Next(lo, hi + 1);
    }
}
