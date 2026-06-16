using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    private static bool _globalHandlersInstalled;

    private static readonly ConditionalWeakTable<object, string> OriginalMapping = new();

    public static readonly ConditionalWeakTable<object, object> Untranslatable = new();

    private static readonly ConditionalWeakTable<object, object> PendingRefresh = new();

    public static readonly DependencyProperty UntranslatableProperty =
        DependencyProperty.RegisterAttached(
            "Untranslatable", typeof(bool), typeof(WpfTranslationPatch),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    private static readonly HashSet<string> MissingKeyList = new();
    public override string PatchName => "WPFTranslationPatch";
    public override Type TargetClass => typeof(MainWindow);
    public override string TargetMethodName => "InitializeCommandBindings";

    [HarmonyPrefix]
    private static void Prefix()
    {
        ReTranslate();
    }

    public static void InstallGlobalHandlers()
    {
        if (_globalHandlersInstalled)
            return;
        _globalHandlersInstalled = true;

        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => TranslateElement(sender)));

        EventManager.RegisterClassHandler(typeof(PushButton), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(PushToggleButton), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));
    }

    public static void MarkUntranslatable(DependencyObject obj)
    {
        obj.SetValue(UntranslatableProperty, true);
    }

    private static string GetOriginal(object obj, string? translated)
    {
        if (Untranslatable.TryGetValue(obj, out _))
            return translated ?? "";

        if (translated is not null)
        {
            if (TranslationManager.TranslatedToOriginalMap.TryGetValue(translated, out var res))
                OriginalMapping.AddOrUpdate(obj, res);
            else if (!OriginalMapping.TryGetValue(obj, out _))
                OriginalMapping.AddOrUpdate(obj, translated);
        }

        return OriginalMapping.TryGetValue(obj, out var original) ? original : translated ?? "";
    }

    private static string GetTranslatedText(string value)
    {
        var resourceKey = TranslationManager.GetKeyByOriginal(value);
        if (!string.IsNullOrEmpty(resourceKey))
            return TranslationManager.Get(resourceKey) ?? value;

        if (Settings.TranslateHardcodedStrings &&
            TranslationManager.HardcodedPropertyMapping.TryGetValue(value, out var hardcodedKey))
            return TranslationManager.Get(hardcodedKey) ?? value;

        if (TranslationManager.TranslatedToTranslationKeyMap.TryGetValue(value, out var translationKey))
            return TranslationManager.Get(translationKey) ?? value;

        if (MissingKeyList.Add(value))
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Wpf_KeyNotFound", value));

        return value;
    }

    private static bool IsDataBound(DependencyObject obj, DependencyProperty dp)
    {
        return BindingOperations.GetBindingExpressionBase(obj, dp) != null;
    }

    private static void TranslateElement(object element)
    {
        if (element is DependencyObject dep && (bool)dep.GetValue(UntranslatableProperty))
            return;

        if (Untranslatable.TryGetValue(element, out _))
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
                    cc.Content = GetTranslatedText(GetOriginal(cc, text));
                break;

            case TextBlock tb:
                if (!IsDataBound(tb, TextBlock.TextProperty))
                    tb.Text = GetTranslatedText(GetOriginal(tb, tb.Text));
                break;
        }

        if (element is FrameworkElement { ToolTip: string tip } fe)
            fe.ToolTip = GetTranslatedText(GetOriginal(fe, tip));
    }

    public static void RefreshAll(DependencyObject obj)
    {
        _RefreshAll(obj, new HashSet<DependencyObject>());

        if (obj is not FrameworkElement { IsLoaded: false } fe || PendingRefresh.TryGetValue(fe, out _))
            return;

        PendingRefresh.Add(fe, fe);
        RoutedEventHandler handler = null!;
        handler = (_, _) =>
        {
            fe.Loaded -= handler;
            PendingRefresh.Remove(fe);
            RefreshAll(fe);
        };
        fe.Loaded += handler;
    }

    private static void _RefreshAll(DependencyObject? root, HashSet<DependencyObject> visited)
    {
        if (root == null || !visited.Add(root)) return;

        TranslateElement(root);

        if (root is AccessText)
            return;

        if (root is Visual or Visual3D)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                _RefreshAll(child, visited);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject dep)
                _RefreshAll(dep, visited);

        if (root is ItemsControl ic)
            foreach (var item in ic.Items)
            {
                var container = ic.ItemContainerGenerator.ContainerFromItem(item);
                if (container is not null)
                    _RefreshAll(container, visited);
            }

        switch (root)
        {
            case PushButton pb:
                _RefreshAll(pb.NormalIcon as DependencyObject, visited);
                _RefreshAll(pb.PressedIcon as DependencyObject, visited);
                _RefreshAll(pb.DisabledIcon as DependencyObject, visited);
                break;
            case PushToggleButton ptb:
                _RefreshAll(ptb.NormalIcon as DependencyObject, visited);
                _RefreshAll(ptb.PressedIcon as DependencyObject, visited);
                _RefreshAll(ptb.DisabledIcon as DependencyObject, visited);
                break;
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

        foreach (Window window in Application.Current.Windows) RefreshAll(window);

        var mainWindow = ReflectionUtils.GetMainWindow();
        var audioEffectWindow = mainWindow.AudioEffectWindow;

        if (audioEffectWindow != null)
            RefreshAll(audioEffectWindow);
    }

}
