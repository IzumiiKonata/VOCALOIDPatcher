using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Formats.LibreSvip.Model;
using VOCALOIDPatcher.Formats.LibreSvip.Plugins;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Formats.LibreSvip;

public static class SvipFormatMenu
{
    private const string ImportItemTag = "VOCALOIDPatcher_FormatImport";
    private const string ExportMenuTag = "VOCALOIDPatcher_FormatExport";
    private const string ExportHeaderKey = "VOCALOIDPatcher_Format_Export";
    private const string AllFilesKey = "VOCALOIDPatcher_Format_AllFiles";
    private const string OperationFailedKey = "VOCALOIDPatcher_Format_OperationFailed";
    private const string EmptyProjectKey = "VOCALOIDPatcher_Export_EmptyProject";

    private static MenuItem? _exportMenu;
    private static bool _languageHooked;

    private static readonly System.Collections.Generic.List<(MenuItem Item, SvipFormatInfo Info)> _formatItems = new();

    private static string LocalizedName(SvipFormatInfo info) =>
        (info.NameKey != null ? TranslationManager.Get(info.NameKey) : null) ?? info.DisplayName;

    public static void Install()
    {
        try
        {
            PluginRegistration.RegisterAll();

            var menu = ReflectionUtils.GetMainMenu();
            var fileMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Tag as string == "Menu_File");
            if (fileMenu == null)
            {
                Debug.Print("[SvipFormatMenu] 未找到文件菜单");
                return;
            }

            var importMenu = fileMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Tag as string == "File_Import");
            if (importMenu == null)
            {
                Debug.Print("[SvipFormatMenu] 未找到导入子菜单");
                return;
            }

            if (importMenu.Items.OfType<MenuItem>().Any(m => m.Tag as string == ImportItemTag))
                return;

            AddImportItems(importMenu);
            AddExportMenu(fileMenu, importMenu);
        }
        catch (Exception e)
        {
            Debug.Print($"[SvipFormatMenu] 安装失败: {e.Message}");
        }
    }

    private static void AddImportItems(MenuItem importMenu)
    {
        var importable = SvipFormatRegistry.Importable.ToList();
        if (importable.Count == 0)
            return;

        importMenu.Items.Add(new Separator());
        foreach (var info in importable)
        {
            var item = BuildItem($"{LocalizedName(info)}…", ImportItemTag, () => OnImport(info));
            _formatItems.Add((item, info));
            importMenu.Items.Add(item);
        }
    }

    private static void AddExportMenu(MenuItem fileMenu, MenuItem importMenu)
    {
        var exportable = SvipFormatRegistry.Exportable.ToList();
        if (exportable.Count == 0)
            return;

        var exportMenu = new MenuItem
        {
            Header = TranslationManager.Get(ExportHeaderKey) ?? ExportHeaderKey,
            Tag = ExportMenuTag,
        };
        WpfTranslationPatch.MarkUntranslatable(exportMenu);
        foreach (var info in exportable)
        {
            var item = BuildItem($"{LocalizedName(info)}…", ExportMenuTag, () => OnExport(info));
            _formatItems.Add((item, info));
            exportMenu.Items.Add(item);
        }

        int importIndex = fileMenu.Items.IndexOf(importMenu);
        fileMenu.Items.Insert(importIndex + 1, exportMenu);

        _exportMenu = exportMenu;
        HookLanguage();
    }

    private static void HookLanguage()
    {
        if (_languageHooked)
            return;
        _languageHooked = true;
        TranslationManager.LanguageChanged += (_, _) => Application.Current?.Dispatcher.Invoke(RefreshHeaders);
    }

    private static void RefreshHeaders()
    {
        if (_exportMenu != null)
            _exportMenu.Header = TranslationManager.Get(ExportHeaderKey) ?? ExportHeaderKey;

        foreach (var (item, info) in _formatItems)
            item.Header = $"{LocalizedName(info)}…";
    }

    private static MenuItem BuildItem(string header, string tag, Action onClick)
    {
        var item = new MenuItem { Header = header, Tag = tag };
        item.Click += (_, _) =>
        {
            try
            {
                onClick();
            }
            catch (Exception e)
            {
                Debug.ShowErrorMessage(TranslationManager.Get(OperationFailedKey) ?? "Operation Failed", e);
            }
        };
        WpfTranslationPatch.MarkUntranslatable(item);
        return item;
    }

    private static void OnImport(SvipFormatInfo info)
    {
        var extensions = info.AllExtensions.Distinct().ToList();
        var pattern = string.Join(";", extensions.Select(e => "*." + e));
        var allFiles = TranslationManager.Get(AllFilesKey) ?? "All Files";

        var dialog = new OpenFileDialog
        {
            Filter = $"{LocalizedName(info)}|{pattern}|{allFiles}|*.*",
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true)
            return;

        var bytes = File.ReadAllBytes(dialog.FileName);
        var project = info.Converter.Load(bytes);
        V6BridgeSvip.Import(project);
    }

    private static void OnExport(SvipFormatInfo info)
    {
        var project = V6BridgeSvip.Export();

        if (project.TrackList.OfType<SingingTrack>().All(t => t.NoteList.Count == 0))
        {
            Debug.ShowMessageBox(
                TranslationManager.Get(EmptyProjectKey)
                ?? "The current project is empty; there are no notes to export.");
            return;
        }

        var bytes = info.Converter.Dump(project);

        var save = new SaveFileDialog
        {
            FileName = $"export.{info.Extension}",
            Filter = $"{LocalizedName(info)}|*.{info.Extension}|{TranslationManager.Get(AllFilesKey) ?? "All Files"}|*.*",
        };
        if (save.ShowDialog() != true)
            return;

        File.WriteAllBytes(save.FileName, bytes);
    }
}
