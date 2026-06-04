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

    private static MenuItem? _exportMenu;
    private static bool _languageHooked;

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
            importMenu.Items.Add(BuildItem($"{info.DisplayName}…", ImportItemTag, () => OnImport(info)));
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
            exportMenu.Items.Add(BuildItem($"{info.DisplayName}…", ExportMenuTag, () => OnExport(info)));

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
                Debug.ShowErrorMessage("操作失败", e);
            }
        };
        WpfTranslationPatch.MarkUntranslatable(item);
        return item;
    }

    private static void OnImport(SvipFormatInfo info)
    {
        var extensions = info.AllExtensions.Distinct().ToList();
        var pattern = string.Join(";", extensions.Select(e => "*." + e));

        var dialog = new OpenFileDialog
        {
            Filter = $"{info.DisplayName}|{pattern}|所有文件|*.*",
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
                TranslationManager.Get("VOCALOIDPatcher_Export_EmptyProject")
                ?? "当前工程为空, 没有可导出的音符。");
            return;
        }

        var bytes = info.Converter.Dump(project);

        var save = new SaveFileDialog
        {
            FileName = $"export.{info.Extension}",
            Filter = $"{info.DisplayName}|*.{info.Extension}|所有文件|*.*",
        };
        if (save.ShowDialog() != true)
            return;

        File.WriteAllBytes(save.FileName, bytes);
    }
}
