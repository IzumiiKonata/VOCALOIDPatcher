using System;
using System.Collections.Generic;
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

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PianorollView, PitchLabelLayer> Layers = new();

    private static readonly System.Reflection.FieldInfo? PanelField =
        AccessTools.Field(typeof(PianorollView), "xPanel");

    private static readonly System.Reflection.FieldInfo? NoteCanvasField =
        AccessTools.Field(typeof(PianorollView), "xNoteInsideActiveTrackCanvas");

    private static readonly System.Reflection.FieldInfo? ScaleTransformField =
        AccessTools.Field(typeof(PianorollView), "scaleTransform");

    private static readonly System.Reflection.FieldInfo? GuideCanvasField =
        AccessTools.Field(typeof(PianorollView), "xGuideCanvas");

    [HarmonyPostfix]
    private static void Postfix(object __instance, UpdateViewTypeFlag typeFlags)
    {
        if (__instance is not PianorollView view)
            return;

        try
        {
            if (!Settings.ShowNotePitch)
            {
                if (Layers.TryGetValue(view, out var existing))
                    existing.Visibility = Visibility.Collapsed;
                return;
            }

            var layer = EnsureLayer(view);
            if (layer == null)
                return;

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
        if (Layers.TryGetValue(view, out var cached))
            return cached;

        if (PanelField?.GetValue(view) is not Grid panel)
            return null;

        foreach (var child in panel.Children)
            if (child is PitchLabelLayer existing)
            {
                Layers.Add(view, existing);
                return existing;
            }

        if (NoteCanvasField?.GetValue(view) is not FastCanvas noteCanvas)
            return null;

        var layer = new PitchLabelLayer
        {
            Name = LayerName,
            NoteCanvas = noteCanvas,
            IsHitTestVisible = false,
            Focusable = false
        };

        if (ScaleTransformField?.GetValue(view) is Transform scale)
            layer.RenderTransform = scale;

        var insertAt = panel.Children.Count;
        if (GuideCanvasField?.GetValue(view) is UIElement guide)
        {
            var index = panel.Children.IndexOf(guide);
            if (index >= 0)
                insertAt = index;
        }
        panel.Children.Insert(insertAt, layer);

        Layers.Add(view, layer);
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

    private sealed class NoteLabel
    {
        public Drawing Drawing = null!;
        public double Width;
        public double Height;
    }

    private static readonly Dictionary<int, NoteLabel> NoteTextCache = new();
    private static double _noteTextCacheDpi;

    private static double _lyricHeight;
    private static double _lyricHeightDpi;

    internal FastCanvas? NoteCanvas;

    protected override void OnRender(DrawingContext drawingContext)
    {
        var canvas = NoteCanvas;
        if (canvas == null || !Settings.ShowNotePitch)
            return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        if (dpi != _noteTextCacheDpi)
        {
            NoteTextCache.Clear();
            _noteTextCacheDpi = dpi;
        }

        if (dpi != _lyricHeightDpi)
        {
            _lyricHeight = new FormattedText("M", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                Typeface, LyricFontSize, TextBrush, dpi).Height;
            _lyricHeightDpi = dpi;
        }

        foreach (UIElement child in canvas.Children)
        {
            if (child is not UINote note)
                continue;

            double w = note.Width;
            double h = note.Height;
            double x = Canvas.GetLeft(note);
            double y = Canvas.GetTop(note);
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(h) || h <= 0)
                continue;

            var label = GetNoteLabel(note.Number, dpi);

            if (!double.IsNaN(w) && label.Width + 6 > w)
                continue;

            double textX = x + 3;

            double textY = h >= _lyricHeight
                ? y + h
                : y + (h - label.Height) / 2.0;

            drawingContext.PushTransform(new TranslateTransform(textX, textY));
            drawingContext.DrawDrawing(label.Drawing);
            drawingContext.Pop();
        }
    }

    private static NoteLabel GetNoteLabel(int number, double dpi)
    {
        if (NoteTextCache.TryGetValue(number, out var label))
            return label;

        var text = new FormattedText(ShowNotePitchPatch.NoteName(number),
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, 9.0, TextBrush, dpi);

        var group = new DrawingGroup();
        using (var ctx = group.Open())
            ctx.DrawText(text, new Point(0.0, 0.0));
        group.Freeze();

        label = new NoteLabel { Drawing = group, Width = text.Width, Height = text.Height };
        NoteTextCache[number] = label;
        return label;
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
