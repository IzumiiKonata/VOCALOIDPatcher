using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

    public static event Action? ActiveVoiceBankChanged;

    [HarmonyPostfix]
    private static void Postfix(object __instance, UpdateViewTypeFlag typeFlags)
    {
        if (__instance is not PianorollView view)
            return;

        try
        {
            Apply(view, typeFlags);

            if (IsPartOrTrackChange(typeFlags))
                ActiveVoiceBankChanged?.Invoke();
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
            adorner?.SetContent(null);
            return;
        }

        if (adorner == null)
        {
            adorner = new CharacterArtAdorner(viewport);
            adornerLayer.Add(adorner);
            adorner.CurrentCompId = GetActiveCompId(view);
            adorner.SetContent(LoadActiveArt(view));
            Debug.Print($"[CharacterArt] 已创建 adorner，视口尺寸 {viewport.RenderSize.Width:0}x{viewport.RenderSize.Height:0}");
            return;
        }

        var compId = GetActiveCompId(view);
        var bankChanged = compId != adorner.CurrentCompId;

        if (IsPartOrTrackChange(typeFlags) || bankChanged || !adorner.HasContent)
        {
            adorner.CurrentCompId = compId;
            adorner.SetContent(LoadActiveArt(view));

            if (bankChanged)
                ActiveVoiceBankChanged?.Invoke();
        }
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
                adorner?.SetContent(Settings.ShowCharacterArt ? LoadActiveArt(view) : null);
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

    private static ArtContent? LoadActiveArt(PianorollView view)
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

            return LoadContent(path);
        }
        catch (Exception e)
        {
            Debug.Print($"[CharacterArt] 加载立绘异常: {e.Message}");
            return null;
        }
    }

    private static ArtContent? LoadContent(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".gif")
        {
            var gif = LoadGif(path);
            if (gif != null)
                return gif;
        }

        if (IsVideoExtension(ext))
            return new VideoArtContent(path);

        var image = LoadStaticImage(path);
        return image == null ? null : new StaticArtContent(image);
    }

    internal static bool IsVideoExtension(string ext)
        => ext is ".mp4" or ".m4v" or ".mov" or ".webm" or ".avi" or ".mkv";

    private static ImageSource LoadStaticImage(string path)
    {
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

    private static GifArtContent? LoadGif(string path)
    {
        GifBitmapDecoder decoder;
        using (var stream = File.OpenRead(path))
            decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        int count = decoder.Frames.Count;
        if (count <= 1)
            return null;

        var lefts = new int[count];
        var tops = new int[count];
        var delays = new int[count];
        var disposals = new int[count];
        int width = 0, height = 0;

        for (int i = 0; i < count; i++)
        {
            var frame = decoder.Frames[i];
            var md = frame.Metadata as BitmapMetadata;
            lefts[i] = GetQueryInt(md, "/imgdesc/Left", 0);
            tops[i] = GetQueryInt(md, "/imgdesc/Top", 0);
            var delay = GetQueryInt(md, "/grctlext/Delay", 0);
            delays[i] = delay <= 0 ? 100 : delay * 10;
            disposals[i] = GetQueryInt(md, "/grctlext/Disposal", 0);
            width = Math.Max(width, lefts[i] + frame.PixelWidth);
            height = Math.Max(height, tops[i] + frame.PixelHeight);
        }

        if (width <= 0 || height <= 0)
            return null;

        var frames = new ImageSource[count];
        ImageSource? previous = null;
        int prevDisposal = 0;

        for (int i = 0; i < count; i++)
        {
            var raw = decoder.Frames[i];
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (previous != null && prevDisposal != 2)
                    dc.DrawImage(previous, new Rect(0, 0, width, height));
                dc.DrawImage(raw, new Rect(lefts[i], tops[i], raw.PixelWidth, raw.PixelHeight));
            }

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            frames[i] = rtb;
            previous = rtb;
            prevDisposal = disposals[i];
        }

        Debug.Print($"[CharacterArt] 已加载 GIF: {path} ({width}x{height}, {count} 帧)");
        return new GifArtContent(frames, delays);
    }

    private static int GetQueryInt(BitmapMetadata? metadata, string query, int fallback)
    {
        try
        {
            if (metadata != null && metadata.ContainsQuery(query))
            {
                var value = metadata.GetQuery(query);
                if (value != null)
                    return Convert.ToInt32(value);
            }
        }
        catch
        {
            // ignored
        }
        return fallback;
    }

    private static string? GetActiveCompId(PianorollView view)
    {
        try
        {
            if (view.DataContext is not MusicalEditorViewModel vm)
                return null;

            var part = vm.ActivePart;
            if (part == null)
                return null;

            var voiceBank = GetVoiceBank(part);
            return voiceBank == null ? null : GetCompId(voiceBank);
        }
        catch
        {
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

internal abstract class ArtContent
{
    internal abstract double Width { get; }
    internal abstract double Height { get; }
    internal abstract void Render(DrawingContext drawingContext, Rect rect);
    internal virtual void Start(Action invalidate) { }
    internal virtual void Stop() { }
}

internal sealed class StaticArtContent : ArtContent
{
    private readonly ImageSource _image;

    internal StaticArtContent(ImageSource image) => _image = image;

    internal override double Width => _image.Width;
    internal override double Height => _image.Height;

    internal override void Render(DrawingContext drawingContext, Rect rect) => drawingContext.DrawImage(_image, rect);
}

internal sealed class GifArtContent : ArtContent
{
    private readonly ImageSource[] _frames;
    private readonly int[] _delaysMs;
    private readonly DispatcherTimer _timer;
    private int _index;
    private Action? _invalidate;

    internal GifArtContent(ImageSource[] frames, int[] delaysMs)
    {
        _frames = frames;
        _delaysMs = delaysMs;
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += OnTick;
    }

    internal override double Width => _frames[0].Width;
    internal override double Height => _frames[0].Height;

    internal override void Render(DrawingContext drawingContext, Rect rect)
        => drawingContext.DrawImage(_frames[_index], rect);

    internal override void Start(Action invalidate)
    {
        _invalidate = invalidate;
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, _delaysMs[_index]));
        _timer.Start();
    }

    internal override void Stop()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _invalidate = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _index = (_index + 1) % _frames.Length;
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, _delaysMs[_index]));
        _invalidate?.Invoke();
    }
}

internal sealed class VideoArtContent : ArtContent
{
    private readonly MediaPlayer _player;
    private double _width = 16;
    private double _height = 9;
    private Action? _invalidate;

    internal VideoArtContent(string path)
    {
        _player = new MediaPlayer { Volume = 0.0, ScrubbingEnabled = true };
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.Open(new Uri(path));
    }

    internal override double Width => _width;
    internal override double Height => _height;

    internal override void Render(DrawingContext drawingContext, Rect rect) => drawingContext.DrawVideo(_player, rect);

    internal override void Start(Action invalidate)
    {
        _invalidate = invalidate;
        _player.Play();
        invalidate();
    }

    internal override void Stop()
    {
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _invalidate = null;
        try
        {
            _player.Stop();
            _player.Close();
        }
        catch
        {
            // ignored
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (_player.NaturalVideoWidth > 0 && _player.NaturalVideoHeight > 0)
        {
            _width = _player.NaturalVideoWidth;
            _height = _player.NaturalVideoHeight;
        }
        _invalidate?.Invoke();
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        _player.Position = TimeSpan.Zero;
        _player.Play();
    }
}

internal sealed class CharacterArtAdorner : Adorner
{
    private ArtContent? _content;

    internal string? CurrentCompId { get; set; }

    internal CharacterArtAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        if (adornedElement is FrameworkElement fe)
        {
            fe.SizeChanged += (_, _) => InvalidateVisual();
            fe.Unloaded += (_, _) => SetContent(null);
        }
    }

    internal bool HasContent => _content != null;

    internal void SetContent(ArtContent? content)
    {
        if (ReferenceEquals(_content, content))
            return;

        _content?.Stop();
        _content = content;
        _content?.Start(InvalidateVisual);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var content = _content;
        if (content == null)
            return;

        var viewport = AdornedElement.RenderSize;
        if (viewport.Width <= 0 || viewport.Height <= 0 || content.Width <= 0 || content.Height <= 0)
            return;

        double targetWidth = Math.Min(Settings.CharacterArtSize, viewport.Width * 0.9);
        double targetHeight = targetWidth * content.Height / content.Width;

        var maxHeight = viewport.Height * 0.9;
        if (targetHeight > maxHeight)
        {
            targetHeight = maxHeight;
            targetWidth = targetHeight * content.Width / content.Height;
        }

        const double margin = 16.0;
        var rect = new Rect(
            viewport.Width - targetWidth - margin,
            viewport.Height - targetHeight - margin,
            targetWidth, targetHeight);

        drawingContext.PushOpacity(Math.Clamp(Settings.CharacterArtOpacity, 0.0, 1.0));
        content.Render(drawingContext, rect);
        drawingContext.Pop();
    }
}
