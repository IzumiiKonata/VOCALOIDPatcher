using System;
using System.Windows;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;

namespace VOCALOIDPatcher.Patch.Patches;

public class RoundedNotePatch : PatchBase
{
    private const double FillBrightness = 0.6;
    private const double SelectedLighten = 0.2;

    public override string PatchName        => "RoundedNotePatch";
    public override Type   TargetClass      => typeof(UINote);
    public override string TargetMethodName => "OnRender";

    public override Type[] ArgumentTypes => new[] { typeof(DrawingContext) };

    [HarmonyPrefix]
    private static bool Prefix(UINote __instance, DrawingContext drawingContext)
    {
        if (!Settings.RoundedNotes)
            return true;

        try
        {
            return !DrawRounded(__instance, drawingContext);
        }
        catch
        {
            return true;
        }
    }

    private static bool DrawRounded(UINote note, DrawingContext dc)
    {
        if (dc == null)
            return false;

        var model = note.Note;
        if (model == null)
            return false;
        var sequence = model.Sequence;
        if (sequence == null)
            return false;

#if NET6_0
        double x = 0.0;
        double timeFromTick = sequence.GetTimeFromTick(model.AbsPosition, model.AbsEnd);
#else
        double x = note.FramePen?.Thickness ?? 1.0;
        double timeFromTick = sequence.GetTimeFromTick(model.AbsPosTick, model.AbsEndTick);
#endif

        double width = note.ActualWidth;
        double height = note.ActualHeight;
        if (width <= 0.0)
            width = 1.0;

        var rect = new Rect(x, 0.0, width, height);
        double radius = Math.Min(height * 0.28, width / 2.0);
        if (radius < 0.0)
            radius = 0.0;

        RenderUtilites.AddGuidelines(dc, rect, 1.0);

        bool invalid = 16.383 < timeFromTick || !model.IsValidPhonemes;

#if NET6_0
        if (invalid && !model.IsSelected)
            dc.DrawRoundedRectangle(null, note.BrokenPen, rect, radius, radius);
        else
            dc.DrawRoundedRectangle(SolidFill(note, model.IsSelected), null, rect, radius, radius);
#else
        if (invalid)
        {
            var brokenPen = model.IsSelected ? note.BrokenSelectedPen : note.BrokenPen;
            dc.DrawRoundedRectangle(null, brokenPen, rect, radius, radius);
        }
        else
        {
            dc.DrawRoundedRectangle(SolidFill(note, model.IsSelected), null, rect, radius, radius);
        }
#endif

        if (model.IsProtected && rect.Width > 4.0)
        {
            var marker = new StreamGeometry();
            using (var ctx = marker.Open())
            {
                var start = new Point(rect.TopRight.X - 3.0, rect.TopRight.Y + 1.0);
                ctx.BeginFigure(start, true, true);
                ctx.PolyLineTo(new[]
                {
                    new Point(rect.TopRight.X - 1.0, rect.TopRight.Y + 1.0),
                    new Point(rect.TopRight.X - 1.0, rect.TopRight.Y + 3.0),
                    new Point(rect.TopRight.X - 3.0, rect.TopRight.Y + 3.0),
                }, true, false);
            }
            marker.Freeze();
            dc.DrawGeometry(note.Foreground, null, marker);
        }

        return true;
    }

    private static Brush? SolidFill(UINote note, bool selected)
    {
        if (!TryGetTrackColor(note, out var color))
            return note.Background;

        byte r, g, b;
        if (selected)
        {
            r = Lighten(color.R);
            g = Lighten(color.G);
            b = Lighten(color.B);
        }
        else
        {
            r = (byte)(color.R * FillBrightness);
            g = (byte)(color.G * FillBrightness);
            b = (byte)(color.B * FillBrightness);
        }

        var fill = new SolidColorBrush(Color.FromArgb((int) (.775f * 255), r, g, b));
        fill.Freeze();
        return fill;
    }

    private static bool TryGetTrackColor(UINote note, out Color color)
    {
        if (note.Foreground is SolidColorBrush stroke && stroke.Color.A != 0)
        {
            color = stroke.Color;
            return true;
        }
        if (note.Background is SolidColorBrush fill && fill.Color.A != 0)
        {
            color = fill.Color;
            return true;
        }
        color = default;
        return false;
    }

    private static byte Lighten(byte channel)
    {
        return (byte)(channel + (255 - channel) * SelectedLighten);
    }

    public static void RefreshNotes()
    {
        try
        {
            var app = Application.Current;
            if (app == null)
                return;

            foreach (Window window in app.Windows)
                foreach (var note in ShowOtherTracksNotesPatch.FindVisualChildren<UINote>(window))
                    note.InvalidateVisual();
        }
        catch (Exception e)
        {
            Debug.Print($"刷新圆角音符失败: {e.Message}");
        }
    }
}
