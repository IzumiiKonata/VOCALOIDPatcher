using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Microsoft.Win32;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.UI;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Formats;

public static class FormatMenu
{
    private const string MarkerTag = "VOCALOIDPatcher_Format";

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

            if (fileMenu.Items.OfType<MenuItem>().Any(m => m.Tag as string == MarkerTag))
                return;

            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(BuildItem("导入工程 (多格式)…", OnImport));
            fileMenu.Items.Add(BuildItem("导出工程 (多格式)…", OnExport));
        }
        catch (Exception e)
        {
            Debug.Print($"[FormatMenu] 安装失败: {e.Message}");
        }
    }

    private static MenuItem BuildItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header, Tag = MarkerTag };
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

    private static void OnImport()
    {
        var importable = FormatRegistry.Importable.Select(FormatRegistry.Get).ToList();
        var extensions = importable.SelectMany(i => i.AllExtensions).Distinct().ToList();
        var pattern = string.Join(";", extensions.Select(e => "*." + e));

        var dialog = new OpenFileDialog
        {
            Filter = $"支持的工程文件|{pattern}|所有文件|*.*",
            Multiselect = true,
        };
        if (dialog.ShowDialog() != true)
            return;

        var files = dialog.FileNames
            .Select(path => new ImportFile(Path.GetFileName(path), File.ReadAllBytes(path)))
            .ToList();

        var info = importable.FirstOrDefault(i => i.Match(files));
        if (info == null)
        {
            Debug.ShowErrorMessage("无法识别所选文件的格式。");
            return;
        }

        if (info.Parser == null)
        {
            Debug.ShowErrorMessage($"{info.DisplayName} 格式的导入尚未实现。");
            return;
        }

        var project = info.Parser(files, new ImportParams());
        V6Bridge.Import(project);
    }

    private static void OnExport()
    {
        var exportable = FormatRegistry.Exportable.Select(FormatRegistry.Get).Where(i => i.Generator != null).ToList();
        if (exportable.Count == 0)
        {
            Debug.ShowErrorMessage("暂无可用的导出格式。");
            return;
        }

        var picker = new JobDialog("VOCALOIDPatcher_Format_Export", "导出工程");
        var combo = picker.AddCombo("VOCALOIDPatcher_Format_ExportFormat", "格式",
            exportable.Select(i => i.DisplayName).ToList(), 0);
        if (!picker.ShowForApply())
            return;

        var info = exportable[Math.Clamp(combo.SelectedIndex, 0, exportable.Count - 1)];
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
