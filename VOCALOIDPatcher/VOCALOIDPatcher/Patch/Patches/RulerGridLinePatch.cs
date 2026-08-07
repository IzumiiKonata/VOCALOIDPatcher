using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarmonyLib;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

internal static class GridLineTextCache
{
    private const int MaxCacheEntries = 512;

    private static readonly Dictionary<(string Text, Brush Brush, double Dpi), Drawing> Cache = new();

    private static readonly ConditionalWeakTable<Control, Typeface> Typefaces = new();

    public static Typeface GetTypeface(Control control)
    {
        if (Typefaces.TryGetValue(control, out var typeface))
            return typeface;

        typeface = new Typeface(control.FontFamily, control.FontStyle, control.FontWeight, control.FontStretch);
        Typefaces.Add(control, typeface);
        return typeface;
    }

    public static Drawing? Get(string text, Typeface typeface, double emSize, Brush? brush, double dpi)
    {
        if (brush == null)
            return null;

        var key = (text, brush, dpi);
        if (Cache.TryGetValue(key, out var drawing))
            return drawing;

        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, emSize, brush, dpi);

        var group = new DrawingGroup();
        using (var ctx = group.Open())
            ctx.DrawText(formatted, new Point(0.0, 0.0));
        group.Freeze();

        if (Cache.Count >= MaxCacheEntries)
            Cache.Clear();

        Cache[key] = group;
        return group;
    }

    public static void DrawCached(DrawingContext dc, Drawing? drawing, double x, double y)
    {
        if (drawing == null)
            return;

        dc.PushTransform(new TranslateTransform(x, y));
        dc.DrawDrawing(drawing);
        dc.Pop();
    }
}

public class TrackRulerGridLinePatch : PatchBase
{
    public override string PatchName        => "TrackRulerGridLinePatch";
    public override Type   TargetClass      => typeof(Yamaha.VOCALOID.TrackEditor.UIRulerGridLine);
    public override string TargetMethodName => "OnRender";

    public override Type[] ArgumentTypes => new[] { typeof(DrawingContext) };

    [HarmonyPrefix]
    private static bool Prefix(Yamaha.VOCALOID.TrackEditor.UIRulerGridLine __instance, DrawingContext drawingContext)
    {
        if (!TrackAreaRenderState.Enabled)
            return true;

        if (drawingContext == null)
            return false;

        var seq = __instance.VSMSequence;
        var sv = __instance.SV;
        var mainVM = __instance.MainVM;
        var vm = __instance.TrackEditorVM;
        if (seq == null || sv == null || mainVM == null || vm == null || __instance.CanvasHeight == 0.0)
            return false;

        var view = new Rect(sv.HorizontalOffset, sv.VerticalOffset, sv.ViewportWidth, sv.ViewportHeight);
        var (first, last) = seq.GetBarIndex(
            vm.CalcViewPositionToTick(view.X, QuantizeStrategy.None),
            vm.CalcViewPositionToTick(view.X + view.Width, QuantizeStrategy.None));
        if (first == -1 || last == -1)
            return false;

        var timeSig = __instance.DefaultTimeSig;
        bool merging = false;
        double merged = 0.0;
        double wpt = __instance.WidthPerTick;
        long songEnd = __instance.SongEnd.Value;
        double dpi = VisualTreeHelper.GetDpi(__instance).PixelsPerDip;
        var typeface = GridLineTextCache.GetTypeface(__instance);

        long barTick = seq.GetTickFromBar(0).Value;
        for (int i = 0; i <= last; i++)
        {
            long nextTick = seq.GetTickFromBar(i + 1).Value;
            if (songEnd < barTick)
                break;

            double x0 = barTick * wpt;
            double x1 = nextTick * wpt;
            double barWidth = x1 - x0;
            barTick = nextTick;

            if (merging)
            {
                merged += barWidth;
                if (Yamaha.VOCALOID.Design.UI.Track.MinWidthToDisplayMeasureLine <= merged)
                {
                    merging = false;
                    merged = 0.0;
                }
                continue;
            }

            if (barWidth < Yamaha.VOCALOID.Design.UI.Track.MinWidthToDisplayMeasureLine)
            {
                merged = barWidth;
                merging = true;
            }

            if (i < first)
                continue;

            timeSig = mainVM.GetTimeSigBeforePosBar(i)?.Value ?? timeSig;
            double beatWidth = barWidth / timeSig.Numer;
            bool drawBeats = Yamaha.VOCALOID.Design.UI.Track.MinWidthToDisplayBeatLine <= beatWidth;

            var label = GridLineTextCache.Get((i + __instance.MeasureOffset + 1).ToString(CultureInfo.InvariantCulture),
                typeface, 10.0, __instance.BrushMeasureNumber, dpi);
            GridLineTextCache.DrawCached(drawingContext, label, x0 + 3.0, view.Bottom - 15.0);

            for (double bx = x0; bx < x1; bx += beatWidth)
            {
                if (!(view.Left <= bx) || !(bx <= view.Right))
                    continue;

                if (bx == x0)
                {
                    var rect = new Rect(x0, view.Bottom - 15.0, 2.0, 15.0);
                    RenderUtilites.AddGuidelines(drawingContext, rect);
                    drawingContext.DrawRectangle(__instance.BrushMeasure, null, rect);
                    if (merging)
                        break;
                }
                else if (drawBeats)
                {
                    var beatRect = new Rect(bx, view.Bottom - 6.0, 2.0, 6.0);
                    RenderUtilites.AddGuidelines(drawingContext, beatRect);
                    drawingContext.DrawRectangle(__instance.BrushBeat, null, beatRect);
                }
            }
        }

        return false;
    }
}

public class MusicalRulerGridLinePatch : PatchBase
{
    public override string PatchName        => "MusicalRulerGridLinePatch";
    public override Type   TargetClass      => typeof(Yamaha.VOCALOID.MusicalEditor.UIRulerGridLine);
    public override string TargetMethodName => "OnRender";

    public override Type[] ArgumentTypes => new[] { typeof(DrawingContext) };

    [HarmonyPrefix]
    private static bool Prefix(Yamaha.VOCALOID.MusicalEditor.UIRulerGridLine __instance, DrawingContext drawingContext)
    {
        if (!TrackAreaRenderState.Enabled)
            return true;

        if (drawingContext == null)
            return false;

        var seq = __instance.VSMSequence;
        var sv = __instance.SV;
        var mainVM = __instance.MainVM;
        var vm = __instance.MusicalEditorVM;
        if (seq == null || sv == null || mainVM == null || vm == null || __instance.CanvasHeight == 0.0)
            return false;

        var view = new Rect(sv.HorizontalOffset, sv.VerticalOffset, sv.ViewportWidth, sv.ViewportHeight);
        var (first, last) = seq.GetBarIndex(
            vm.CalcViewPositionToTick(view.X, QuantizeStrategy.None),
            vm.CalcViewPositionToTick(view.X + view.Width, QuantizeStrategy.None));
        if (first == -1 || last == -1)
            return false;

        var timeSig = __instance.DefaultTimeSig;
        bool merging = false;
        double merged = 0.0;
        double wpt = __instance.WidthPerTick;
        long songEnd = __instance.SongEndTick.Value;
        double dpi = VisualTreeHelper.GetDpi(__instance).PixelsPerDip;
        var typeface = GridLineTextCache.GetTypeface(__instance);

        long barTick = seq.GetTickFromBar(first).Value;
        for (int i = first; i <= last; i++)
        {
            long nextTick = seq.GetTickFromBar(i + 1).Value;
            if (songEnd < barTick)
                break;

            double x0 = barTick * wpt;
            double x1 = nextTick * wpt;
            double barWidth = x1 - x0;
            barTick = nextTick;

            timeSig = mainVM.GetTimeSigBeforePosBar(i)?.Value ?? timeSig;
            double beatWidth = barWidth / timeSig.Numer;
            bool drawBeats = Yamaha.VOCALOID.Design.UI.Musical.MinWidthToDisplayBeatLine <= beatWidth;

            if (merging)
            {
                merged += barWidth;
                if (Yamaha.VOCALOID.Design.UI.Musical.MinWidthToDisplayMeasureLine <= merged)
                {
                    merging = false;
                    merged = 0.0;
                }
                continue;
            }

            if (barWidth < Yamaha.VOCALOID.Design.UI.Musical.MinWidthToDisplayMeasureLine)
            {
                merged = barWidth;
                merging = true;
            }

            var label = GridLineTextCache.Get((i + __instance.MeasureOffset + 1).ToString(CultureInfo.InvariantCulture),
                typeface, 10.0, __instance.BrushMeasureNumber, dpi);
            GridLineTextCache.DrawCached(drawingContext, label, x0 + 3.0, view.Bottom - 13.0);

            for (double bx = x0; bx < x1; bx += beatWidth)
            {
                if (!(view.Left <= bx) || !(bx <= view.Right))
                    continue;

                if (bx == x0)
                {
                    var rect = new Rect(bx, view.Bottom - 13.0, 2.0, 13.0);
                    RenderUtilites.AddGuidelines(drawingContext, rect);
                    drawingContext.DrawRectangle(__instance.BrushMeasure, null, rect);
                    if (merging)
                        break;
                }
                else if (drawBeats)
                {
                    var beatRect = new Rect(bx, view.Bottom - 6.0, 2.0, 6.0);
                    RenderUtilites.AddGuidelines(drawingContext, beatRect);
                    drawingContext.DrawRectangle(__instance.BrushBeat, null, beatRect);
                }
            }
        }

        return false;
    }
}
