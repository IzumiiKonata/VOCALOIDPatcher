using System;
using System.Reflection;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

public class AlwaysShowWaveformPatch : PatchBase
{
    public override string PatchName        => "AlwaysShowWaveformPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "DrawRenderedWaveCanvas";

    public override Type[] ArgumentTypes => new[] { typeof(MusicalEditorViewModel) };

    private static readonly FieldInfo? WaveCanvasField =
        AccessTools.Field(typeof(PianorollView), "xRenderedWaveCanvas");

    private static readonly MethodInfo? InsertMethod =
        AccessTools.Method(typeof(PianorollView), "InsertRenderedWave", new[] { typeof(MusicalEditorViewModel) });

    [HarmonyPrefix]
    private static bool Prefix(PianorollView __instance, MusicalEditorViewModel vm)
    {
        if (!Settings.AlwaysShowWaveform)
            return true;

        if (Settings.SvEditorStyle)
            PrecomputeBaselines(vm);

        try
        {
            if (vm == null || WaveCanvasField == null || InsertMethod == null)
                return true;

            if (WaveCanvasField.GetValue(__instance) is not FastCanvas canvas)
                return true;

            canvas.ClearElement();
            InsertMethod.Invoke(__instance, new object[] { vm });
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static void PrecomputeBaselines(MusicalEditorViewModel vm)
    {
        try
        {
            WaveformSvState.Clear();

            var part = vm?.ActivePart;
            var seq = vm?.VSMSequence;
            if (part == null || seq == null)
                return;

            var scores = vm.GetScoreEnumerator(part);
            var samples = vm.GetSampleEnumerator(part);
            if (scores == null || samples == null)
                return;

            long samplesPerFrame = seq.NumSampleInFrame;
            if (samplesPerFrame <= 0)
                return;

            long frameCount = samples.NumSamples / samplesPerFrame;
            WaveformSvState.Precompute(scores, frameCount);
        }
        catch
        {
            WaveformSvState.Clear();
        }
    }

    public static void RefreshWaveform() => ShowOtherTracksNotesPatch.RequestRefreshPianoroll();
}

public class WaveformRenderPatch : PatchBase
{
    public override string PatchName        => "WaveformRenderPatch";
    public override Type   TargetClass      => typeof(UIRenderedWave);
    public override string TargetMethodName => "OnRender";

    public override Type[] ArgumentTypes => new[] { typeof(DrawingContext) };

    [HarmonyPrefix]
    private static void Prefix(DrawingContext drawingContext, out int __state)
    {
        __state = 0;
        if (!Settings.AlwaysShowWaveform || drawingContext == null)
            return;

        if (Settings.SvEditorStyle)
            WaveformSvState.Activate();

        double opacity = Settings.WaveformOpacity;
        if (opacity < 1.0)
        {
            drawingContext.PushOpacity(opacity);
            __state = 1;
        }
    }

    [HarmonyFinalizer]
    private static void Finalizer(DrawingContext drawingContext, int __state)
    {
        WaveformSvState.Deactivate();

        if (__state == 1 && drawingContext != null)
            drawingContext.Pop();
    }
}

public class NoteRowRemapPatch : PatchBase
{
    public override string PatchName        => "NoteRowRemapPatch";
    public override Type   TargetClass      => typeof(MusicalEditorViewModel);
    public override string TargetMethodName => "CalcNoteNumberTopPosition";

    public override Type[] ArgumentTypes => new[] { typeof(int) };

    [HarmonyPostfix]
    private static void Postfix(int noteNumber, ref double __result, MusicalEditorViewModel __instance)
    {
        if (!WaveformSvState.Active)
            return;

        __result = WaveformSvState.Adjust(noteNumber, __result, __instance.OneKeyHeight);
    }
}

public class ScoreFrameCaptureListPatch : PatchBase
{
    public override string PatchName        => "ScoreFrameCaptureListPatch";
    public override Type   TargetClass      => typeof(VSMScoreList);
    public override string TargetMethodName => "ScoreAtIndex";

    public override Type[] ArgumentTypes => new[] { typeof(long) };

    [HarmonyPostfix]
    private static void Postfix(long index)
    {
        if (WaveformSvState.Active)
            WaveformSvState.CurrentFrame = index;
    }
}

public class ScoreFrameCaptureFilePatch : PatchBase
{
    public override string PatchName        => "ScoreFrameCaptureFilePatch";
    public override Type   TargetClass      => typeof(VSMScoreFile);
    public override string TargetMethodName => "ScoreAtIndex";

    public override Type[] ArgumentTypes => new[] { typeof(long) };

    [HarmonyPostfix]
    private static void Postfix(long index)
    {
        if (WaveformSvState.Active)
            WaveformSvState.CurrentFrame = index;
    }
}

#if !NET6_0
public class ScoreFrameCaptureCombinedPatch : PatchBase
{
    public override string PatchName        => "ScoreFrameCaptureCombinedPatch";
    public override Type   TargetClass      => typeof(VSMCombinedScore);
    public override string TargetMethodName => "ScoreAtIndex";

    public override Type[] ArgumentTypes => new[] { typeof(long) };

    [HarmonyPostfix]
    private static void Postfix(long index)
    {
        if (WaveformSvState.Active)
            WaveformSvState.CurrentFrame = index;
    }
}
#endif

internal static class WaveformSvState
{
    private const int GroupSemitones = 7;
    private const double DownwardRows = 2.0;
    private const long MaxFrames = 4_000_000;

    public static bool Active { get; private set; }
    public static long CurrentFrame { get; set; }

    private static int[]? _baselineByFrame;

    public static void Activate() => Active = true;
    public static void Deactivate() => Active = false;

    public static void Clear() => _baselineByFrame = null;

    public static void Precompute(IVSMScoreEnumerator scores, long frameCount)
    {
        _baselineByFrame = null;
        if (scores == null || frameCount <= 0 || frameCount > MaxFrames)
            return;

        var arr = new int[frameCount];

        long groupStart = -1;
        int groupMin = 0;
        int groupMax = 0;

        for (long i = 0; i < frameCount; i++)
        {
            float pit = scores.ScoreAtIndex(i).NotePit;
            if (pit == float.MinValue)
            {
                arr[i] = int.MinValue;
                continue;
            }

            int noteNumber = (int)VSMScore.GetRawNoteNumberFromPitch(pit);
            if (groupStart < 0)
            {
                groupStart = i;
                groupMin = noteNumber;
                groupMax = noteNumber;
                continue;
            }

            int newMin = Math.Min(groupMin, noteNumber);
            int newMax = Math.Max(groupMax, noteNumber);
            if (newMax - newMin <= GroupSemitones)
            {
                groupMin = newMin;
                groupMax = newMax;
            }
            else
            {
                FillGroup(arr, groupStart, i, groupMin);
                groupStart = i;
                groupMin = noteNumber;
                groupMax = noteNumber;
            }
        }

        if (groupStart >= 0)
            FillGroup(arr, groupStart, frameCount, groupMin);

        _baselineByFrame = arr;
    }

    private static void FillGroup(int[] arr, long start, long end, int baseline)
    {
        for (long i = start; i < end; i++)
            if (arr[i] != int.MinValue)
                arr[i] = baseline;
    }

    public static double Adjust(int noteNumber, double top, double oneKeyHeight)
    {
        var arr = _baselineByFrame;
        long frame = CurrentFrame;
        if (arr == null || frame < 0 || frame >= arr.LongLength)
            return top;

        int baseline = arr[frame];
        if (baseline == int.MinValue)
            return top;

        return top + (noteNumber - baseline + DownwardRows) * oneKeyHeight;
    }
}
