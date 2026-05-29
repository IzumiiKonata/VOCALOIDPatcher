using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;
using UpdateViewTypeFlag = Yamaha.VOCALOID.MusicalEditor.UpdateViewTypeFlag;

namespace VOCALOIDPatcher.Patch.Patches;

public class ShowNotePitchPatch : PatchBase
{
    private const string LayerName = "VOCALOIDPatcher_PitchLayer";

    public override string PatchName        => "ShowNotePitchPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "UpdateView";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(object),
        typeof(UpdateViewTypeFlag),
        typeof(Yamaha.VOCALOID.VSM.UpdateObserverNotifyEventArgs),
        typeof(object)
    };

    [HarmonyPostfix]
    private static void Postfix(object __instance, UpdateViewTypeFlag typeFlags)
    {
        if (__instance is not PianorollView view)
            return;

        try
        {
            var layer = EnsureLayer(view);
            if (layer == null)
                return;

            if (!Settings.ShowNotePitch)
            {
                layer.Visibility = Visibility.Collapsed;
                return;
            }

            layer.Visibility = Visibility.Visible;
            if (IsLayoutChange(typeFlags))
                layer.InvalidateVisual();
        }
        catch (Exception e)
        {
            Debug.Print($"音高叠加失败: {e.Message}");
        }
    }

    private static bool IsLayoutChange(UpdateViewTypeFlag f)
        => f is UpdateViewTypeFlag.ActiveTrackChanged
            or UpdateViewTypeFlag.ActivePartChanged
            or UpdateViewTypeFlag.ShowMusicalEditor
            or UpdateViewTypeFlag.SequenceChanged
            or UpdateViewTypeFlag.ModelChanged
            or UpdateViewTypeFlag.NoteChanged
            or UpdateViewTypeFlag.NoteSelectionChanged
            or UpdateViewTypeFlag.HorizontalZoomed
            or UpdateViewTypeFlag.VerticalZoomed
            or UpdateViewTypeFlag.EditModeChanged;

    private static PitchLabelLayer? EnsureLayer(PianorollView view)
    {
        if (AccessTools.Field(typeof(PianorollView), "xPanel")?.GetValue(view) is not Grid panel)
            return null;

        foreach (var child in panel.Children)
            if (child is PitchLabelLayer existing)
                return existing;

        if (AccessTools.Field(typeof(PianorollView), "xNoteInsideActiveTrackCanvas")?.GetValue(view)
            is not FastCanvas noteCanvas)
            return null;

        var layer = new PitchLabelLayer
        {
            Name = LayerName,
            NoteCanvas = noteCanvas,
            IsHitTestVisible = false,
            Focusable = false
        };

        if (AccessTools.Field(typeof(PianorollView), "scaleTransform")?.GetValue(view) is Transform scale)
            layer.RenderTransform = scale;

        var insertAt = panel.Children.Count;
        if (AccessTools.Field(typeof(PianorollView), "xGuideCanvas")?.GetValue(view) is UIElement guide)
        {
            var index = panel.Children.IndexOf(guide);
            if (index >= 0)
                insertAt = index;
        }
        panel.Children.Insert(insertAt, layer);

        return layer;
    }

    internal static string NoteName(int noteNumber)
    {
        var octave = noteNumber / 12 - 2;
        return PitchLabelLayer.NoteNames[noteNumber % 12] + octave;
    }
}

internal sealed class PitchLabelLayer : FrameworkElement
{
    internal static readonly string[] NoteNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static readonly Typeface Typeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static readonly Brush TextBrush = CreateBrush();

    internal FastCanvas? NoteCanvas;

    protected override void OnRender(DrawingContext drawingContext)
    {
        var canvas = NoteCanvas;
        if (canvas == null || !Settings.ShowNotePitch)
            return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var lyricHeight = new FormattedText("M", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface, LyricFontSize, TextBrush, dpi).Height;

        foreach (var child in canvas.VirtualChildren)
        {
            if (child is not UINote note)
                continue;

            double w = note.Width;
            double h = note.Height;
            double x = Canvas.GetLeft(note);
            double y = Canvas.GetTop(note);
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(h) || h <= 0)
                continue;

            var text = new FormattedText(ShowNotePitchPatch.NoteName(note.Number),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, 9.0, TextBrush, dpi);

            if (!double.IsNaN(w) && text.Width + 6 > w)
                continue;

            double textX = x + 3;

            double textY = h >= lyricHeight
                ? y + h
                : y + (h - text.Height) / 2.0;

            drawingContext.DrawText(text, new Point(textX, textY));
        }
    }

    private static double? _lyricFontSize;

    private static double LyricFontSize
    {
        get
        {
            if (_lyricFontSize.HasValue)
                return _lyricFontSize.Value;

            try
            {
                var type = AccessTools.TypeByName("Yamaha.VOCALOID.Design.UI.Note");
                var field = type != null ? AccessTools.Field(type, "lyricFontSize") : null;
                _lyricFontSize = (field?.GetValue(null) as double? ?? 12.0) - 1.5;
            }
            catch
            {
                _lyricFontSize = 10.5;
            }

            return _lyricFontSize.Value;
        }
    }

    private static Brush CreateBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
        brush.Freeze();
        return brush;
    }
}
