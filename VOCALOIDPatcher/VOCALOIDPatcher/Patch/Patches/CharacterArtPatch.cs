using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;
using UpdateViewTypeFlag = Yamaha.VOCALOID.MusicalEditor.UpdateViewTypeFlag;

namespace VOCALOIDPatcher.Patch.Patches;

public class CharacterArtPatch : PatchBase
{
    public override string PatchName        => "CharacterArtPatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "UpdateView";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(object),
        typeof(UpdateViewTypeFlag),
        typeof(UpdateObserverNotifyEventArgs),
        typeof(object)
    };

    [HarmonyPostfix]
    private static void Postfix(object __instance, UpdateViewTypeFlag typeFlags)
    {
        if (__instance is not PianorollView view)
            return;

        try
        {
            Apply(view, typeFlags);
        }
        catch (Exception e)
        {
            Debug.Print($"[CharacterArt] 异常: {e.Message}");
        }
    }

    private static void Apply(PianorollView view, UpdateViewTypeFlag typeFlags)
    {
        var viewport = FindViewport(view);
        if (viewport == null)
        {
            Debug.Print("[CharacterArt] 未找到滚动视口 (ScrollContentPresenter/ScrollViewer)");
            return;
        }

        var adornerLayer = AdornerLayer.GetAdornerLayer(viewport);
        if (adornerLayer == null)
        {
            Debug.Print("[CharacterArt] 未找到 AdornerLayer");
            return;
        }

        var adorner = FindAdorner(adornerLayer, viewport);

        if (!Settings.ShowCharacterArt)
        {
            adorner?.SetImage(null);
            return;
        }

        if (adorner == null)
        {
            adorner = new CharacterArtAdorner(viewport);
            adornerLayer.Add(adorner);
            adorner.SetImage(LoadActiveArt(view));
            Debug.Print($"[CharacterArt] 已创建 adorner，视口尺寸 {viewport.RenderSize.Width:0}x{viewport.RenderSize.Height:0}");
            return;
        }

        if (IsPartOrTrackChange(typeFlags) || !adorner.HasImage)
            adorner.SetImage(LoadActiveArt(view));
    }

    public static void RefreshArt()
    {
        try
        {
            foreach (Window window in Application.Current.Windows)
            foreach (var view in ShowOtherTracksNotesPatch.FindVisualChildren<PianorollView>(window))
            {
                var viewport = FindViewport(view);
                if (viewport == null)
                    continue;

                var layer = AdornerLayer.GetAdornerLayer(viewport);
                if (layer != null)
                    FindAdorner(layer, viewport)?.InvalidateVisual();
            }
        }
        catch (Exception e)
        {
            Debug.Print($"[CharacterArt] 刷新失败: {e.Message}");
        }
    }

    public static void ReloadArt()
    {
        try
        {
            foreach (Window window in Application.Current.Windows)
            foreach (var view in ShowOtherTracksNotesPatch.FindVisualChildren<PianorollView>(window))
            {
                var viewport = FindViewport(view);
                if (viewport == null)
                    continue;

                var layer = AdornerLayer.GetAdornerLayer(viewport);
                var adorner = layer == null ? null : FindAdorner(layer, viewport);
                adorner?.SetImage(Settings.ShowCharacterArt ? LoadActiveArt(view) : null);
            }
        }
        catch (Exception e)
        {
            Debug.Print($"[CharacterArt] 重载失败: {e.Message}");
        }
    }

    public static (string CompId, string Name)? GetActiveVoiceBankInfo()
    {
        try
        {
            foreach (Window window in Application.Current.Windows)
            foreach (var view in ShowOtherTracksNotesPatch.FindVisualChildren<PianorollView>(window))
            {
                if (view.DataContext is not MusicalEditorViewModel vm)
                    continue;

                var part = vm.ActivePart;
                if (part == null)
                    continue;

                var voiceBank = GetVoiceBank(part);
                if (voiceBank == null)
                    continue;

                var compId = GetCompId(voiceBank);
                if (!string.IsNullOrEmpty(compId))
                    return (compId!, GetName(voiceBank) ?? compId!);
            }
        }
        catch (Exception e)
        {
            Debug.Print($"[CharacterArt] 取活动声库失败: {e.Message}");
        }
        return null;
    }

    public static string ArtStorageDir => Path.Combine(Patcher.ConfigDir, "CharacterArt");

    public static bool ImportArt(string compId, string sourcePath)
    {
        try
        {
            if (string.IsNullOrEmpty(compId) || !File.Exists(sourcePath))
                return false;

            Directory.CreateDirectory(ArtStorageDir);

            DeleteStoredArt(compId);

            var safeId = SanitizeFileName(compId);
            var extension = Path.GetExtension(sourcePath);
            var target = Path.Combine(ArtStorageDir, safeId + extension);
            File.Copy(sourcePath, target, true);

            Settings.SetCharacterArtPath(compId, target);
            ReloadArt();
            return true;
        }
        catch (Exception e)
        {
            Debug.Print($"[CharacterArt] 导入立绘失败: {e.Message}");
            return false;
        }
    }

    public static void ClearArt(string compId)
    {
        try
        {
            DeleteStoredArt(compId);
            Settings.SetCharacterArtPath(compId, null);
            ReloadArt();
        }
        catch (Exception e)
        {
            Debug.Print($"[CharacterArt] 清除立绘失败: {e.Message}");
        }
    }

    public static bool HasCustomArt(string compId)
    {
        var path = Settings.GetCharacterArtPath(compId);
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    private static void DeleteStoredArt(string compId)
    {
        var existing = Settings.GetCharacterArtPath(compId);
        if (!string.IsNullOrEmpty(existing) && File.Exists(existing) &&
            Path.GetFullPath(existing).StartsWith(Path.GetFullPath(ArtStorageDir), StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(existing); }
            catch (Exception e) { Debug.Print($"[CharacterArt] 删除旧立绘失败: {e.Message}"); }
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static bool IsPartOrTrackChange(UpdateViewTypeFlag f)
        => f is UpdateViewTypeFlag.ActivePartChanged
            or UpdateViewTypeFlag.ActiveTrackChanged
            or UpdateViewTypeFlag.ShowMusicalEditor
            or UpdateViewTypeFlag.SequenceChanged;

    private static UIElement? FindViewport(PianorollView view)
    {
        ScrollViewer? scrollViewer = null;
        for (DependencyObject? d = VisualTreeHelper.GetParent(view); d != null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is ScrollContentPresenter presenter)
                return presenter;
            if (scrollViewer == null && d is ScrollViewer sv)
                scrollViewer = sv;
        }
        return scrollViewer;
    }

    private static CharacterArtAdorner? FindAdorner(AdornerLayer layer, UIElement adorned)
    {
        var adorners = layer.GetAdorners(adorned);
        if (adorners == null)
            return null;

        foreach (var adorner in adorners)
            if (adorner is CharacterArtAdorner found)
                return found;

        return null;
    }

    private static ImageSource? LoadActiveArt(PianorollView view)
    {
        try
        {
            if (view.DataContext is not MusicalEditorViewModel vm)
            {
                Debug.Print("[CharacterArt] DataContext 不是 MusicalEditorViewModel");
                return null;
            }

            object? part = vm.ActivePart;
            if (part == null)
            {
                // Debug.Print("[CharacterArt] 当前无活动 Part");
                return null;
            }

            var voiceBank = GetVoiceBank(part);
            if (voiceBank == null)
            {
                Debug.Print("[CharacterArt] 取声库失败 (VoiceBank == null)");
                return null;
            }

            var path = Settings.GetCharacterArtPath(GetCompId(voiceBank) ?? string.Empty);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                path = GetImagePath(voiceBank);

            if (string.IsNullOrEmpty(path))
            {
                Debug.Print("[CharacterArt] 立绘路径为空");
                return null;
            }

            if (!File.Exists(path))
            {
                Debug.Print($"[CharacterArt] 立绘文件不存在: {path}");
                return null;
            }

            var bitmap = new BitmapImage();
            using (var stream = File.OpenRead(path))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            Debug.Print($"[CharacterArt] 已加载立绘: {path} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
            return bitmap;
        }
        catch (Exception e)
        {
            Debug.Print($"[CharacterArt] 加载立绘异常: {e.Message}");
            return null;
        }
    }

    private static object? GetVoiceBank(object part)
    {
        var prop = part.GetType().GetProperty("VoiceBank");
        if (prop != null && prop.GetIndexParameters().Length == 0)
            return prop.GetValue(part);

        return AccessTools.Method("Yamaha.VOCALOID.WIVSMMidiPartExtension:VoiceBank")
            ?.Invoke(null, new[] { part });
    }

    private static string? GetCompId(object voiceBank)
        => voiceBank.GetType().GetProperty("CompID")?.GetValue(voiceBank) as string;

    private static string? GetName(object voiceBank)
        => voiceBank.GetType().GetProperty("Name")?.GetValue(voiceBank) as string;

    private static string? GetImagePath(object voiceBank)
    {
        var extension = AccessTools.Method("Yamaha.VOCALOID.VoiceBankExtension:GetImagePath");
        if (extension != null)
            return extension.Invoke(null, new[] { voiceBank }) as string;

        return voiceBank.GetType().GetMethod("GetImagePath", Type.EmptyTypes)?.Invoke(voiceBank, null) as string;
    }
}

internal sealed class CharacterArtAdorner : Adorner
{
    private ImageSource? _image;

    internal CharacterArtAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        if (adornedElement is FrameworkElement fe)
            fe.SizeChanged += (_, _) => InvalidateVisual();
    }

    internal bool HasImage => _image != null;

    internal void SetImage(ImageSource? image)
    {
        _image = image;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var image = _image;
        if (image == null)
            return;

        var viewport = AdornedElement.RenderSize;
        if (viewport.Width <= 0 || viewport.Height <= 0 || image.Width <= 0 || image.Height <= 0)
            return;

        double targetWidth = Math.Min(Settings.CharacterArtSize, viewport.Width * 0.9);
        double targetHeight = targetWidth * image.Height / image.Width;

        var maxHeight = viewport.Height * 0.9;
        if (targetHeight > maxHeight)
        {
            targetHeight = maxHeight;
            targetWidth = targetHeight * image.Width / image.Height;
        }

        const double margin = 16.0;
        var rect = new Rect(
            viewport.Width - targetWidth - margin,
            viewport.Height - targetHeight - margin,
            targetWidth, targetHeight);

        drawingContext.PushOpacity(Math.Clamp(Settings.CharacterArtOpacity, 0.0, 1.0));
        drawingContext.DrawImage(image, rect);
        drawingContext.Pop();
    }
}
