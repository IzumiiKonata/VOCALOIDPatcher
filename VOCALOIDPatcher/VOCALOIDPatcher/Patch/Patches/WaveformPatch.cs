using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.Design.UI;
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

#if !NET6_0
    private const double FadeWidth = 24.0;
#endif

    [HarmonyPrefix]
    private static bool Prefix(UIRenderedWave __instance, DrawingContext drawingContext, out int __state)
    {
        __state = 0;
        if (!Settings.AlwaysShowWaveform || drawingContext == null)
            return true;

#if !NET6_0
        if (Settings.SvEditorStyle && WaveformSvState.HasBaselines)
        {
            try
            {
                if (CustomRender(__instance, drawingContext))
                    return false;
            }
            catch
            {
            }
        }
#endif

        if (Settings.SvEditorStyle)
            WaveformSvState.Activate();

        double opacity = Settings.WaveformOpacity;
        if (opacity < 1.0)
        {
            drawingContext.PushOpacity(opacity);
            __state = 1;
        }
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(DrawingContext drawingContext, int __state)
    {
        WaveformSvState.Deactivate();

        if (__state == 1 && drawingContext != null)
            drawingContext.Pop();
    }

#if !NET6_0
    private struct Column
    {
        public double X;
        public double DeltaTop;
        public double DeltaBottom;
        public int Baseline;
        public double Center;
    }

    private static bool CustomRender(UIRenderedWave wave, DrawingContext dc)
    {
        WaveformSvState.Deactivate();

        var vm = wave.MusicalEditorVM;
        var seq = vm?.VSMSequence;
        var scores = wave.ScoreEnumerator;
        var samples = wave.SampleEnumerator;
        if (vm == null || seq == null || scores == null || samples == null)
            return false;

        long samplesPerFrame = seq.NumSampleInFrame;
        if (samplesPerFrame <= 0)
            return false;

        var samplingRate = seq.GetSamplingRate();
        double left = Canvas.GetLeft(wave);
        double right = Canvas.GetRight(wave);
        double oneKeyHeight = vm.OneKeyHeight;
        double waveHeight = Musical.RenderedWaveHeight * (oneKeyHeight / General.KeyBaseHeight) * .5;

        var centers = new Dictionary<int, double>();
        var cols = new List<Column>();

        for (double x = left; x <= right; x += 1.0)
        {
            double q1 = vm.GetQuarterFromViewPosition(x);
            double q2 = vm.GetQuarterFromViewPosition(x + 1.0);
            long sample = seq.GetSampleFromTime(seq.GetTimeFromQuarter(q1) - wave.SampleBeginAbsTime, samplingRate);
            long span = seq.GetSampleFromTime(seq.GetTimeFromQuarter(q1, q2), samplingRate);
            if (span == 0L)
                continue;

            long frame = sample / samplesPerFrame;
            if (scores.ScoreAtIndex(frame).NotePit == VSMScore.UnusedPitchData)
                continue;

            int baseline = WaveformSvState.BaselineAtFrame(frame);
            if (baseline == int.MinValue)
                continue;

            var thumbs = samples.ThumbWithRange(sample, sample + span);
            if (thumbs.Count == 0)
                continue;
            var thumb = thumbs[0];
            if (!thumb.HasValue)
                continue;

            var self = thumb.GetValueOrDefault();
            self.Join((short)0, (short)0);
            double dTop = -(waveHeight / 2.0) * AudioMath.ShortToFloat(self.Max);
            double dBottom = -(waveHeight / 2.0) * AudioMath.ShortToFloat(self.Min);

            if (!centers.TryGetValue(baseline, out double center))
            {
                center = WaveformSvState.Center(vm.CalcNoteNumberTopPosition(baseline), oneKeyHeight);
                centers[baseline] = center;
            }

            cols.Add(new Column
            {
                X = x - left,
                DeltaTop = dTop,
                DeltaBottom = dBottom,
                Baseline = baseline,
                Center = center
            });
        }

        if (cols.Count == 0)
            return false;

        var bg = wave.Background;
        var pen = wave.MainPen;

        double globalOpacity = Settings.WaveformOpacity;
        bool pushedOpacity = globalOpacity < 1.0;
        if (pushedOpacity)
            dc.PushOpacity(globalOpacity);

        try
        {
            var main = new List<(double X, double Top, double Bottom, int Key)>(cols.Count);
            foreach (var c in cols)
                main.Add((c.X, c.Center + c.DeltaTop, c.Center + c.DeltaBottom, c.Baseline));
            dc.DrawGeometry(bg, pen, BuildGeometry(main));

            for (int i = 1; i < cols.Count; i++)
            {
                var a = cols[i - 1];
                var b = cols[i];
                if (b.X != a.X + 1.0 || a.Baseline == b.Baseline)
                    continue;

                DrawGhost(dc, bg, pen, cols, i, a.Center, true);
                DrawGhost(dc, bg, pen, cols, i, b.Center, false);
            }
        }
        finally
        {
            if (pushedOpacity)
                dc.Pop();
        }

        return true;
    }

    private static void DrawGhost(DrawingContext dc, Brush? bg, Pen? pen, List<Column> cols, int boundaryIndex, double altCenter, bool forward)
    {
        double boundaryX = cols[boundaryIndex].X;
        var pts = new List<(double X, double Top, double Bottom, int Key)>();

        if (forward)
        {
            int baseline = cols[boundaryIndex].Baseline;
            double prevX = boundaryX - 1.0;
            for (int k = boundaryIndex; k < cols.Count; k++)
            {
                var c = cols[k];
                if (c.Baseline != baseline || c.X != prevX + 1.0 || c.X >= boundaryX + FadeWidth)
                    break;
                pts.Add((c.X, altCenter + c.DeltaTop, altCenter + c.DeltaBottom, 0));
                prevX = c.X;
            }
        }
        else
        {
            int baseline = cols[boundaryIndex - 1].Baseline;
            double nextX = boundaryX;
            for (int k = boundaryIndex - 1; k >= 0; k--)
            {
                var c = cols[k];
                if (c.Baseline != baseline || c.X != nextX - 1.0 || c.X <= boundaryX - 1.0 - FadeWidth)
                    break;
                pts.Add((c.X, altCenter + c.DeltaTop, altCenter + c.DeltaBottom, 0));
                nextX = c.X;
            }
            pts.Reverse();
        }

        if (pts.Count == 0)
            return;

        var geo = BuildGeometry(pts);
        var brush = MakeFadeBrush(forward);
        dc.PushOpacityMask(brush);
        try
        {
            dc.DrawGeometry(bg, pen, geo);
        }
        finally
        {
            dc.Pop();
        }
    }

    private static Brush MakeFadeBrush(bool fadeRight)
    {
        var opaque = Color.FromArgb(255, 255, 255, 255);
        var clear = Color.FromArgb(0, 255, 255, 255);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.0, 0.0),
            EndPoint = new Point(1.0, 0.0)
        };
        if (fadeRight)
        {
            brush.GradientStops.Add(new GradientStop(opaque, 0.0));
            brush.GradientStops.Add(new GradientStop(clear, 1.0));
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(clear, 0.0));
            brush.GradientStops.Add(new GradientStop(opaque, 1.0));
        }
        brush.Freeze();
        return brush;
    }

    private static StreamGeometry BuildGeometry(List<(double X, double Top, double Bottom, int Key)> pts)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                Line(ctx, p.X, p.Top, p.X, p.Bottom);
                if (i > 0)
                {
                    var q = pts[i - 1];
                    if (p.X == q.X + 1.0 && p.Key == q.Key)
                    {
                        Line(ctx, q.X, q.Top, p.X, p.Top);
                        Line(ctx, q.X, q.Bottom, p.X, p.Bottom);
                    }
                }
            }
        }
        geo.Freeze();
        return geo;
    }

    private static void Line(StreamGeometryContext ctx, double x0, double y0, double x1, double y1)
    {
        ctx.BeginFigure(new Point(x0, y0), false, false);
        ctx.LineTo(new Point(x1, y1), true, false);
    }
#endif
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
    private const double DownwardRows = 3.0;
    private const long MaxFrames = 4_000_000;

    public static bool Active { get; private set; }
    public static long CurrentFrame { get; set; }

    private static int[]? _baselineByFrame;

    public static bool HasBaselines => _baselineByFrame != null;

    public static void Activate() => Active = true;
    public static void Deactivate() => Active = false;

    public static void Clear() => _baselineByFrame = null;

    public static int BaselineAtFrame(long frame)
    {
        var arr = _baselineByFrame;
        if (arr == null || frame < 0 || frame >= arr.LongLength)
            return int.MinValue;
        return arr[frame];
    }

    public static double Center(double baselineTop, double oneKeyHeight)
        => baselineTop + (DownwardRows + 0.5) * oneKeyHeight;

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
