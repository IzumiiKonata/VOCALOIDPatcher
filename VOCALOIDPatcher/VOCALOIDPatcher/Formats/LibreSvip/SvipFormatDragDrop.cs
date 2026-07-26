using System;
using System.IO;
using System.Linq;
using System.Windows;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.UI;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Formats.LibreSvip;

public static class SvipFormatDragDrop
{
    private static bool _installed;

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
            if (!FormatOptionDialog.Show(info, FormatOptionDirection.Import))
                return;
            var project = SvipProjectLoader.Load(info, paths);
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
        if (paths.Length == 0)
            return null;

        string ext = Path.GetExtension(paths[0]).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
            return null;
        var info = SvipFormatRegistry.FindImportableByExtension(ext);
        if (info == null)
            return null;
        if (paths.Length > 1 && (!info.MultipleFile || paths.Any(path =>
                !info.MatchesExtension(Path.GetExtension(path).TrimStart('.')))))
            return null;
        return info;
    }
}
