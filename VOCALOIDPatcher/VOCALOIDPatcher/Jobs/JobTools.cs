using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VOCALOIDPatcher.Patch.Patches;
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

    private static int RandSigned(int amount) => Rng.Next(-amount, amount + 1);

    private static double RandSignedDouble(double amount) => (Rng.NextDouble() * 2.0 - 1.0) * amount;
}
