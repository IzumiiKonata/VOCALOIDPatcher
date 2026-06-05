using System;
using System.Windows;
using System.Windows.Media;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.MusicalEditor;

namespace VOCALOIDPatcher.Patch.Patches;

public class CenteredLyricPatch : PatchBase
{
    public override string PatchName        => "CenteredLyricPatch";
    public override Type   TargetClass      => typeof(UILyricAndPhoneme);
    public override string TargetMethodName => "OnRender";

    public override Type[] ArgumentTypes => new[] { typeof(DrawingContext) };

    [HarmonyPrefix]
    private static bool Prefix(UILyricAndPhoneme __instance, DrawingContext drawingContext)
    {
        if (!Settings.CenteredLyrics)
            return true;

        try
        {
            return !CenteredLyricRenderer.DrawWithPhoneme(__instance, drawingContext);
        }
        catch
        {
            return true;
        }
    }

    public static void RefreshLyrics() => CenteredLyricRenderer.RefreshLyrics();
}

public class CenteredLyricPlainPatch : PatchBase
{
    public override string PatchName        => "CenteredLyricPlainPatch";
    public override Type   TargetClass      => typeof(UILyric);
    public override string TargetMethodName => "OnRender";

    public override Type[] ArgumentTypes => new[] { typeof(DrawingContext) };

    [HarmonyPrefix]
    private static bool Prefix(UILyric __instance, DrawingContext drawingContext)
    {
        if (!Settings.CenteredLyrics)
            return true;

        try
        {
            return !CenteredLyricRenderer.DrawLyricOnly(__instance, drawingContext);
        }
        catch
        {
            return true;
        }
    }
}

internal static class CenteredLyricRenderer
{
    private const double MinLeftMargin = 4.0;
    private const double LeftMarginRatio = 0.22;

    private static double LeftMargin(double height) => Math.Max(MinLeftMargin, height * LeftMarginRatio);

    public static bool DrawWithPhoneme(UILyricAndPhoneme lyric, DrawingContext dc)
    {
        if (dc == null || lyric.VM == null || string.IsNullOrEmpty(lyric.Lyrics) || string.IsNullOrEmpty(lyric.Phoneme))
            return false;

        var first = lyric.FirstFormattedTexts;
        if (first == null)
            return false;

        double height = lyric.ActualHeight;
        if (first.Height > height)
            return false;

        double margin = LeftMargin(height);
        double y = (height - first.Height) / 2.0;
        double firstEnd = margin + first.Width;
        if (firstEnd > lyric.ActualWidth)
            return true;

        dc.DrawText(first, new Point(margin, y));

        var second = lyric.SecondFormattedTexts;
        if (second != null && firstEnd + second.Width <= lyric.ActualWidth)
            dc.DrawText(second, new Point(firstEnd, y));

        return true;
    }

    public static bool DrawLyricOnly(UILyric lyric, DrawingContext dc)
    {
        if (dc == null || lyric.VM == null || string.IsNullOrEmpty(lyric.Lyrics))
            return false;

        var texts = lyric.FormattedTexts;
        if (texts == null)
            return false;

        var formatted = texts.GetLyricFormattedText(lyric.VM.LyricInputMode);
        if (formatted == null)
            return false;

        double height = lyric.ActualHeight;
        if (formatted.Height > height)
            return false;

        double margin = LeftMargin(height);
        double y = (height - formatted.Height) / 2.0;
        if (margin + formatted.Width <= lyric.ActualWidth)
            dc.DrawText(formatted, new Point(margin, y));

        return true;
    }

    public static void RefreshLyrics()
    {
        try
        {
            var app = Application.Current;
            if (app == null)
                return;

            foreach (Window window in app.Windows)
                foreach (var item in ShowOtherTracksNotesPatch.FindVisualChildren<UILyricBase>(window))
                    item.InvalidateVisual();
        }
        catch (Exception e)
        {
            Debug.Print($"刷新居中歌词失败: {e.Message}");
        }
    }
}
