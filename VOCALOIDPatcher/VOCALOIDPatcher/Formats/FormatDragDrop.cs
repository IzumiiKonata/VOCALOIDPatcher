using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using VOCALOIDPatcher.Formats.Model;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Formats;

// 让主窗口接受拖入我们支持的非原生格式 (svp/s5p/ust/ustx/ccs/musicxml/dv/tssln/ufdata/vsq …)。
// 原生窗口在 Window 根上的 DragOver/Drop (bubbling) 会把非"原生序列扩展名"的文件判为禁止 (Effects=None)。
// 这里挂 Preview* (tunneling) 处理器: 隧道阶段先于冒泡运行, 命中我们的格式时设 Effects=Copy 并 Handled=true,
// 从而压制原生处理 (否则随后的冒泡会把 Effects 覆盖回 None, 显示禁止图标)。其它情况一概不碰, 保留原生行为。
public static class FormatDragDrop
{
    private static bool _installed;

    // 原生窗口拖放本就接受 (加载为文档) 的扩展名, 不接管以免改变现有行为。
    // net8 支持 vsqx/vpr/ppsf, net6 仅 vsqx/vpr; 取并集保守跳过 (这些格式仍可经导入菜单使用)。
    private static readonly HashSet<string> NativeDropExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "vsqx", "vpr", "ppsf",
    };

    public static void Install()
    {
        try
        {
            if (_installed)
                return;

            var window = ReflectionUtils.GetMainWindow();
            window.AllowDrop = true;
            window.PreviewDragEnter += OnPreviewDragOver;
            window.PreviewDragOver  += OnPreviewDragOver;
            window.PreviewDrop      += OnPreviewDrop;
            _installed = true;
        }
        catch (Exception e)
        {
            Debug.Print($"[FormatDragDrop] 安装失败: {e.Message}");
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

        // 命中我们的格式: 接管这次 drop, 阻止原生处理
        e.Handled = true;

        try
        {
            var files = paths
                .Select(p => new ImportFile(Path.GetFileName(p), File.ReadAllBytes(p)))
                .ToList();

            FormatMenu.RunImport(info, files);
        }
        catch (Exception ex)
        {
            Debug.ShowErrorMessage("导入失败", ex);
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

    // 仅按扩展名匹配 (不读文件内容, 供频繁触发的 DragOver 使用), 跳过原生已支持的格式
    private static FormatInfo? Detect(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return null;

        var stubs = paths.Select(p => new ImportFile(Path.GetFileName(p), Array.Empty<byte>())).ToList();

        // 原生拖放已支持的扩展名交给原生处理 (加载为文档), 不接管
        if (stubs.Any(f => NativeDropExtensions.Contains(f.ExtensionName)))
            return null;

        foreach (var format in FormatRegistry.Importable)
        {
            if (FormatMenu.NativeImportFormats.Contains(format))
                continue;

            var info = FormatRegistry.Get(format);
            if (info.Parser == null)
                continue;

            if (info.Match(stubs))
                return info;
        }

        return null;
    }
}
