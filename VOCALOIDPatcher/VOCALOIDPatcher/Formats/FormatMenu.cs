using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Formats;

public static class FormatMenu
{
    private const string ImportItemTag = "VOCALOIDPatcher_FormatImport";
    private const string ExportMenuTag = "VOCALOIDPatcher_FormatExport";
    private const string ExportHeaderKey = "VOCALOIDPatcher_Format_Export";
    private const string ExportHeaderFallback = "导出 (多格式)";

    // 这些格式 V6 编辑器原生支持, 不重复提供
    private static readonly HashSet<Format> NativeFormats = new()
    {
        Format.Vpr, Format.Vsqx, Format.VocaloidMid, Format.StandardMid,
    };

    private static MenuItem? _exportMenu;
    private static bool _languageHooked;

    public static void Install()
    {
        try
        {
            var menu = ReflectionUtils.GetMainMenu();
            var fileMenu = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Tag as string == "Menu_File");
            if (fileMenu == null)
            {
                Debug.Print("[FormatMenu] 未找到文件菜单");
                return;
            }

            var importMenu = fileMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Tag as string == "File_Import");
            if (importMenu == null)
            {
                Debug.Print("[FormatMenu] 未找到导入子菜单");
                return;
            }

            if (importMenu.Items.OfType<MenuItem>().Any(m => m.Tag as string == ImportItemTag))
                return;

            AddImportItems(importMenu);
            AddExportMenu(fileMenu, importMenu);
        }
        catch (Exception e)
        {
            Debug.Print($"[FormatMenu] 安装失败: {e.Message}");
        }
    }

    private static void AddImportItems(MenuItem importMenu)
    {
        var importable = FormatRegistry.Importable.Select(FormatRegistry.Get)
            .Where(i => i.Parser != null && !NativeFormats.Contains(i.Format)).ToList();
        if (importable.Count == 0)
            return;

        importMenu.Items.Add(new Separator());
        foreach (var info in importable)
            importMenu.Items.Add(BuildItem($"{info.DisplayName}…", ImportItemTag, () => OnImport(info)));
    }

    private static void AddExportMenu(MenuItem fileMenu, MenuItem importMenu)
    {
        var exportable = FormatRegistry.Exportable.Select(FormatRegistry.Get)
            .Where(i => i.Generator != null && !NativeFormats.Contains(i.Format)).ToList();
        if (exportable.Count == 0)
            return;

        var exportMenu = new MenuItem
        {
            Header = TranslationManager.Get(ExportHeaderKey) ?? ExportHeaderFallback,
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
            _exportMenu.Header = TranslationManager.Get(ExportHeaderKey) ?? ExportHeaderFallback;
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

    private static void OnImport(FormatInfo info)
    {
        var extensions = info.AllExtensions.Distinct().ToList();
        var pattern = string.Join(";", extensions.Select(e => "*." + e));

        var dialog = new OpenFileDialog
        {
            Filter = $"{info.DisplayName}|{pattern}|所有文件|*.*",
            Multiselect = info.MultipleFile,
        };
        if (dialog.ShowDialog() != true)
            return;

        var files = dialog.FileNames
            .Select(path => new ImportFile(Path.GetFileName(path), File.ReadAllBytes(path)))
            .ToList();

        if (info.Parser == null)
        {
            Debug.ShowErrorMessage($"{info.DisplayName} 格式的导入尚未实现。");
            return;
        }

        var project = info.Parser(files, new ImportParams());
        V6Bridge.Import(project);
    }

    private static void OnExport(FormatInfo info)
    {
        var project = V6Bridge.Export();
        var features = BuildFeatures(info, project);
        var result = info.Generator!(project, features);

        var save = new SaveFileDialog
        {
            FileName = result.FileName,
            Filter = $"{info.DisplayName}|*.{info.Extension}|所有文件|*.*",
        };
        if (save.ShowDialog() != true)
            return;

        File.WriteAllBytes(save.FileName, result.Content);
    }

    private static IReadOnlyList<FeatureConfig> BuildFeatures(FormatInfo info, Project project)
    {
        var list = new List<FeatureConfig>();
        foreach (var feature in info.AvailableFeaturesForGeneration)
        {
            if (!feature.IsAvailable(project))
                continue;
            if (feature == Feature.ConvertPitch)
                list.Add(new FeatureConfig.ConvertPitch());
            else if (feature == Feature.SplitProject)
                list.Add(FeatureConfig.SplitProject.GetDefault(info.Format));
        }

        return list;
    }
}
