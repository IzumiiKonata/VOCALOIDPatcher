using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.MusicalEditor;

namespace VOCALOIDPatcher.Patch.Patches;

public class ShowNotePitchPatch : PatchBase
{
    private static readonly string[] NoteNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static readonly Typeface Typeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static readonly Brush TextBrush = Frozen(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));

    public override string PatchName        => "ShowNotePitchPatch";
    public override Type   TargetClass      => typeof(UINote);
    public override string TargetMethodName => "OnRender";
    public override Type[] ArgumentTypes    => new[] { typeof(DrawingContext) };

    [HarmonyPostfix]
    private static void Postfix(UINote __instance, DrawingContext drawingContext)
    {
        if (!Settings.ShowNotePitch)
            return;

        try
        {
            double w = __instance.ActualWidth;
            double h = __instance.ActualHeight;
            if (w <= 0 || h <= 0)
                return;

            var name = NoteName(__instance.Number);
            var dpi = VisualTreeHelper.GetDpi(__instance).PixelsPerDip;
            var text = new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                Typeface, 9.0, TextBrush, dpi);

            const double padX = 3.0;
            if (text.Width + padX * 2 > w || text.Height > h)
                return;

            drawingContext.DrawText(text, new Point(padX, (h - text.Height) / 2.0));
        }
        catch (Exception e)
        {
            Debug.Print($"绘制音高失败: {e.Message}");
        }
    }

    private static string NoteName(int noteNumber)
    {
        var octave = noteNumber / 12 - 2;
        return NoteNames[noteNumber % 12] + octave;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
