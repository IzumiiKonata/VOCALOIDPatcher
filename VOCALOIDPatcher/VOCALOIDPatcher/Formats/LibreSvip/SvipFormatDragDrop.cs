using System;
using System.IO;
using System.Linq;
using System.Windows;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Formats.LibreSvip;

public static class SvipFormatDragDrop
{
    private static bool _installed;

    private static readonly System.Collections.Generic.HashSet<string> NativeDropExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "vsqx", "vpr", "ppsf" };

    public static void Install()
    {
        try
        {
            if (_installed)
                return;

            PluginRegistration.RegisterAll();

            var window = ReflectionUtils.GetMainWindow();
            window.AllowDrop = true;
            window.PreviewDragEnter += OnPreviewDragOver;
            window.PreviewDragOver += OnPreviewDragOver;
            window.PreviewDrop += OnPreviewDrop;
            _installed = true;
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_SvipDrop_InstallFailed", e.Message));
        }
    }

    private static void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (TryGetDropPaths(e, out var paths) && Detect(paths) != null)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private static void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (!TryGetDropPaths(e, out var paths))
            return;

        var info = Detect(paths);
        if (info == null)
            return;

        e.Handled = true;

        try
        {
            var bytes = File.ReadAllBytes(paths[0]);
            var project = info.Converter.Load(bytes);
            V6BridgeSvip.Import(project);
        }
        catch (Exception ex)
        {
            Debug.ShowErrorMessage(TranslationManager.Tr("VOCALOIDPatcher_Format_ImportFailed"), ex);
        }
    }

    private static bool TryGetDropPaths(DragEventArgs e, out string[] paths)
    {
        paths = Array.Empty<string>();
        if (!e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: true))
            return false;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] array || array.Length == 0)
            return false;
        paths = array;
        return true;
    }

    private static SvipFormatInfo? Detect(string[] paths)
    {
        if (paths.Length != 1)
            return null;

        string ext = Path.GetExtension(paths[0]).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || NativeDropExtensions.Contains(ext))
            return null;

        return SvipFormatRegistry.FindImportableByExtension(ext);
    }
}
