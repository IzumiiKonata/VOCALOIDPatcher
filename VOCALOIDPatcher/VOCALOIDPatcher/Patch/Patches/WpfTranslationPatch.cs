using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

[SuppressMessage("ReSharper", "UnusedMember.Local")]
public class WpfTranslationPatch : PatchBase
{
    public override string PatchName        => "WPFTranslationPatch";
    public override Type   TargetClass      => typeof(MainWindow);
    public override string TargetMethodName => "InitializeCommandBindings";

    [HarmonyPrefix]
    private static void Prefix()
    {
        ReTranslate();
        FixFilepathSeparator();
    }

    private static bool _globalHandlersInstalled;

    /**
     * 注册类级事件处理器，让 WPF 自己在元素出现时通知我们翻译，
     * 省得给每个右键菜单/弹窗单独打补丁。只需调用一次。
     */
    public static void InstallGlobalHandlers()
    {
        if (_globalHandlersInstalled)
            return;
        _globalHandlersInstalled = true;

        // 元素一加载进可视树就翻译它自己，子元素各自的 Loaded 会接力覆盖
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => TranslateElement(sender)));

        // 右键菜单和工具提示是独立的弹出树，打开时整棵刷一遍
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));
    }

    private static readonly Dictionary<object, string> OriginalMapping = new();

    public static readonly HashSet<object> Untranslatable = new();

    private static string GetOriginal(object obj, string? translated)
    {
        if (Untranslatable.Contains(obj))
            return translated ?? "";

        if (translated is not null && !OriginalMapping.ContainsKey(obj))
        {
            OriginalMapping[obj] = translated;
        }

        if (translated is not null && TranslationManager.TranslatedToOriginalMap.TryGetValue(translated, out var res))
            OriginalMapping[obj] = res;

        return OriginalMapping[obj];
    }

    private static readonly HashSet<string> MissingKeyList = new();

    private static string GetTranslatedText(string value)
    {
        var resourceKey = TranslationManager.GetKeyByOriginal(value);
        if (!string.IsNullOrEmpty(resourceKey))
            return TranslationManager.Get(resourceKey) ?? value;

        if (Settings.TranslateHardcodedStrings && TranslationManager.HardcodedPropertyMapping.TryGetValue(value, out var hardcodedKey))
            return TranslationManager.Get(hardcodedKey) ?? value;

        if (TranslationManager.TranslatedToTranslationKeyMap.TryGetValue(value, out var translationKey))
            return TranslationManager.Get(translationKey) ?? value;

        if (MissingKeyList.Add(value))
            Debug.Print($"Key not found: {value}");

        return value;
    }

    public static bool TranslateTextBox;

    private static void TranslateElement(object element)
    {
        if (Untranslatable.Contains(element))
            return;

        switch (element)
        {
            case HeaderedItemsControl hic:
                if (hic.Header is string hicHeader)
                    hic.Header = GetTranslatedText(GetOriginal(hic, hicHeader));
                break;

            case HeaderedContentControl hcc:
                if (hcc.Header is string hccHeader)
                    hcc.Header = GetTranslatedText(GetOriginal(hcc, hccHeader));
                break;

            case ContentControl cc:
                if (cc.Content is string text)
                {
                    cc.Content = GetTranslatedText(GetOriginal(cc, text));
                }
                break;

            case TextBlock tb:
                if (TranslateTextBox)
                    tb.Text = GetTranslatedText(tb.Text);
                break;
        }

        if (element is FrameworkElement { ToolTip: string tip } fe)
        {
            fe.ToolTip = GetTranslatedText(GetOriginal(fe, tip));
        }
    }

    public static void RefreshAll(DependencyObject obj)
    {
        var visited = new HashSet<DependencyObject>();
        // visited.Clear();
        _RefreshAll(obj, visited);

        if (obj is FrameworkElement { IsLoaded: false } fe)
        {
            fe.Loaded += (_, _) =>
            {
                RefreshAll(fe);
            };
        }
    }

    private static void _RefreshAll(DependencyObject? root, HashSet<DependencyObject> visited)
    {
        if (root == null || !visited.Add(root)) return;

        visited.Add(root);

        TranslateElement(root);

        if (root is Visual or Visual3D)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                _RefreshAll(child, visited);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject dep)
                _RefreshAll(dep, visited);
        }

        if (root is ItemsControl ic)
        {
            foreach (var item in ic.Items)
            {
                var container = ic.ItemContainerGenerator.ContainerFromItem(item);
                if (container is not null)
                    _RefreshAll(container, visited);
            }
        }

        if (root is FrameworkElement fe)
        {
            if (fe.ContextMenu != null)
                _RefreshAll(fe.ContextMenu, visited);

            if (fe.ToolTip is DependencyObject tip)
                _RefreshAll(tip, visited);
        }
    }

    /**
     * 翻译所有控件。
     * 因为劫持的 dll 拉起要比窗口创建晚，所以还需要手动刷新一下。
     */
    public static void ReTranslate()
    {
        var mainMenu = ReflectionUtils.GetMainMenu();
        RefreshAll(mainMenu);

        foreach (Window window in Application.Current.Windows)
        {
            RefreshAll(window);
        }

        var mainWindow = ReflectionUtils.GetMainWindow();
        var audioEffectWindow = mainWindow.AudioEffectWindow;

        if (audioEffectWindow != null)
        {
            TranslateTextBox = true;
            RefreshAll(audioEffectWindow);
            TranslateTextBox = false;
        }

    }

    /**
     * 修复 "文件 -> 最近打开..." 中文件路径分隔符全都是 ￥ 的问题
     */
    private static void FixFilepathSeparator()
    {
        var xRecentFiles = ReflectionUtils.GetMainWindowField<MenuItem>("xRecentFiles");
        xRecentFiles.FontFamily = new FontFamily("Consolas");
    }
}
