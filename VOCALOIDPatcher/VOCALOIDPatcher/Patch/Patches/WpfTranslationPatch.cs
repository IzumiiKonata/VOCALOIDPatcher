using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
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

    private static readonly ConditionalWeakTable<ComboBox, object> ComboBoxEvents = new();

    private static readonly ConditionalWeakTable<ComboBox, ComboBoxRefreshState> ComboBoxRefreshStates = new();

    [ThreadStatic] private static int _refreshComboBoxDepth;

    private sealed class ComboBoxRefreshState
    {
        public bool LayoutHookInstalled;
        public int LayoutPassesLeft;
    }

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
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is ComboBox combo)
                {
                    EnsureComboBoxHandlers(combo);
                    ScheduleComboBoxRefresh(combo);
                    return;
                }

                TranslateElement(sender);
            }));

        EventManager.RegisterClassHandler(typeof(PushButton), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(PushToggleButton), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent,
            new RoutedEventHandler((sender, _) => RefreshAll((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => EnsureComboBoxesInTree((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(UserControl), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => EnsureComboBoxesInTree((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(ComboBox), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => ScheduleComboBoxRefresh((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(ComboBox), Selector.SelectionChangedEvent,
            new System.Windows.Controls.SelectionChangedEventHandler((sender, _) => ScheduleComboBoxRefresh((DependencyObject)sender)));

        EventManager.RegisterClassHandler(typeof(ComboBoxItem), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => ScheduleItemRefresh((DependencyObject)sender)));
    }

    private static void ScheduleItemRefresh(DependencyObject obj)
    {
        RefreshAll(obj);

        if (obj is not FrameworkElement fe || fe.Dispatcher.HasShutdownStarted)
            return;

        fe.Dispatcher.BeginInvoke(new Action(() => RefreshAll(fe)), DispatcherPriority.Loaded);
    }

    public static void RefreshComboBox(ComboBox combo)
    {
        if (_refreshComboBoxDepth > 0)
            return;

        _refreshComboBoxDepth++;
        try
        {
            ScheduleComboBoxRefresh(combo);
        }
        finally
        {
            _refreshComboBoxDepth--;
        }
    }

    private static void EnsureComboBoxHandlers(ComboBox combo)
    {
        if (ComboBoxEvents.TryGetValue(combo, out _))
            return;

        ComboBoxEvents.Add(combo, combo);

        combo.Loaded += (_, _) => ScheduleComboBoxRefresh(combo);
        combo.DropDownOpened += (_, _) => ScheduleComboBoxRefresh(combo);
        combo.DropDownClosed += (_, _) => ScheduleComboBoxRefresh(combo);

        combo.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
                ScheduleComboBoxRefresh(combo);
        };
    }

    private static void EnsureComboBoxesInTree(DependencyObject root)
    {
        var visited = new HashSet<DependencyObject>();
        Walk(root, visited);
        return;

        void Walk(DependencyObject node, HashSet<DependencyObject> seen)
        {
            if (!seen.Add(node))
                return;

            if (node is ComboBox combo)
            {
                EnsureComboBoxHandlers(combo);

                if (combo.IsVisible)
                    ScheduleComboBoxRefresh(combo);
            }

            if (node is Visual or Visual3D)
            {
                var count = VisualTreeHelper.GetChildrenCount(node);
                for (var i = 0; i < count; i++)
                    Walk(VisualTreeHelper.GetChild(node, i), seen);
            }

            foreach (var child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject dep)
                    Walk(dep, seen);
        }
    }

    private static void ScheduleComboBoxRefresh(DependencyObject obj)
    {
        if (obj is ComboBox combo)
        {
            EnsureComboBoxHandlers(combo);

            var state = ComboBoxRefreshStates.GetValue(combo, _ => new ComboBoxRefreshState());
            state.LayoutPassesLeft = 6;

            if (!state.LayoutHookInstalled)
            {
                state.LayoutHookInstalled = true;
                combo.LayoutUpdated += (_, _) =>
                {
                    if (state.LayoutPassesLeft <= 0)
                        return;

                    state.LayoutPassesLeft--;
                    RefreshAll(combo);
                };
            }
        }

        RefreshAll(obj);

        if (obj is not FrameworkElement fe || fe.Dispatcher.HasShutdownStarted)
            return;

        fe.Dispatcher.BeginInvoke(new Action(() => RefreshAll(fe)), DispatcherPriority.Loaded);
        fe.Dispatcher.BeginInvoke(new Action(() => RefreshAll(fe)), DispatcherPriority.ApplicationIdle);
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

    private static string? TryGetTranslatedText(string value, bool preferHardcoded = false)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (preferHardcoded && TryGetHardcodedText(value) is { } hardcoded)
            return hardcoded;

        var resourceKey = TranslationManager.GetKeyByOriginal(value);
        if (!string.IsNullOrEmpty(resourceKey))
        {
            var translated = TranslationManager.Get(resourceKey);
            if (translated != null)
                return translated;
        }

        if (!preferHardcoded && TryGetHardcodedText(value) is { } fallbackHardcoded)
            return fallbackHardcoded;

        if (TranslationManager.TranslatedToTranslationKeyMap.TryGetValue(value, out var translationKey))
        {
            var translated = TranslationManager.Get(translationKey);
            if (translated != null)
                return translated;
        }

        return null;
    }

    private static string? TryGetHardcodedText(string value)
    {
        if (!Settings.TranslateHardcodedStrings ||
            !TranslationManager.HardcodedPropertyMapping.TryGetValue(value, out var hardcodedKey))
            return null;

        return TranslationManager.Get(hardcodedKey);
    }

    private static string GetTranslatedText(string value)
    {
        var translated = TryGetTranslatedText(value);
        if (translated != null)
            return translated;

        if (MissingKeyList.Add(value))
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_Wpf_KeyNotFound", value));

        return value;
    }

    private static string TranslateDisplayText(string value)
    {
        var translated = TryGetTranslatedText(value, preferHardcoded: true);
        if (translated != null)
            return translated;

        var separatorIndex = value.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex > 0)
            return TranslateComposite(value, separatorIndex, 3);

        separatorIndex = value.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex > 0)
            return TranslateComposite(value, separatorIndex, 2);

        return value;
    }

    private static string TranslateComposite(string value, int separatorIndex, int separatorLength)
    {
        var left = value[..separatorIndex];
        var right = value[(separatorIndex + separatorLength)..];

        var translatedLeft = TryGetTranslatedText(left, preferHardcoded: true);
        var translatedRight = TryGetTranslatedText(right, preferHardcoded: true);

        if (translatedLeft == null && translatedRight == null)
            return value;

        return (translatedLeft ?? left) + value.Substring(separatorIndex, separatorLength) + (translatedRight ?? right);
    }

    private static bool IsDataBound(DependencyObject obj, DependencyProperty dp)
    {
        return BindingOperations.GetBindingExpressionBase(obj, dp) != null;
    }

    private static void TranslateBoundText(TextBlock textBlock)
    {
        try
        {
            if (BindingOperations.GetBindingExpression(textBlock, TextBlock.TextProperty) is not { } expression
                || expression.ParentBinding is not { } binding)
                return;

            if (binding.Converter is TranslatingConverter)
            {
                expression.UpdateTarget();
                return;
            }

            if (binding.Mode is BindingMode.TwoWay or BindingMode.OneWayToSource)
                return;

            var translatedBinding = CloneBinding(binding);
            translatedBinding.Converter = new TranslatingConverter(binding.Converter);
            BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, translatedBinding);
        }
        catch (Exception e)
        {
            Debug.Print($"WPF combo box translation failed: {e.Message}");
        }
    }

    private static Binding CloneBinding(Binding source)
    {
        var clone = new Binding
        {
            Path = source.Path,
            XPath = source.XPath,
            Mode = source.Mode,
            UpdateSourceTrigger = source.UpdateSourceTrigger,
            NotifyOnSourceUpdated = source.NotifyOnSourceUpdated,
            NotifyOnTargetUpdated = source.NotifyOnTargetUpdated,
            NotifyOnValidationError = source.NotifyOnValidationError,
            BindsDirectlyToSource = source.BindsDirectlyToSource,
            IsAsync = source.IsAsync,
            AsyncState = source.AsyncState,
            ConverterParameter = source.ConverterParameter,
            ConverterCulture = source.ConverterCulture,
            FallbackValue = source.FallbackValue,
            StringFormat = source.StringFormat,
            TargetNullValue = source.TargetNullValue,
            BindingGroupName = source.BindingGroupName,
            Delay = source.Delay,
            UpdateSourceExceptionFilter = source.UpdateSourceExceptionFilter,
            ValidatesOnExceptions = source.ValidatesOnExceptions,
            ValidatesOnDataErrors = source.ValidatesOnDataErrors,
            ValidatesOnNotifyDataErrors = source.ValidatesOnNotifyDataErrors
        };

        foreach (var rule in source.ValidationRules)
            clone.ValidationRules.Add(rule);

        if (source.Source != null)
            clone.Source = source.Source;
        else if (source.RelativeSource != null)
            clone.RelativeSource = source.RelativeSource;
        else if (source.ElementName != null)
            clone.ElementName = source.ElementName;

        return clone;
    }

    private static void TranslateElement(object element, bool inComboBox = false)
    {
        if (element is ComboBox combo)
            EnsureComboBoxHandlers(combo);

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
                    cc.Content = inComboBox
                        ? TranslateDisplayText(GetOriginal(cc, text))
                        : GetTranslatedText(GetOriginal(cc, text));
                break;

            case TextBlock tb:
                if (!IsDataBound(tb, TextBlock.TextProperty))
                    tb.Text = inComboBox
                        ? TranslateDisplayText(GetOriginal(tb, tb.Text))
                        : GetTranslatedText(GetOriginal(tb, tb.Text));
                else if (inComboBox)
                    TranslateBoundText(tb);
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

    private static void _RefreshAll(DependencyObject? root, HashSet<DependencyObject> visited, bool inComboBox = false)
    {
        if (root == null || !visited.Add(root)) return;

        if (root is ComboBox combo)
        {
            EnsureComboBoxHandlers(combo);
            inComboBox = true;
        }
        else if (root is ComboBoxItem)
            inComboBox = true;

        TranslateElement(root, inComboBox);

        if (root is AccessText)
            return;

        if (root is Visual or Visual3D)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                _RefreshAll(child, visited, inComboBox);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject dep)
                _RefreshAll(dep, visited, inComboBox);

        if (root is ItemsControl ic)
            foreach (var item in ic.Items)
            {
                var container = ic.ItemContainerGenerator.ContainerFromItem(item);
                if (container is not null)
                    _RefreshAll(container, visited, inComboBox);
            }

        switch (root)
        {
            case PushButton pb:
                _RefreshAll(pb.NormalIcon as DependencyObject, visited, inComboBox);
                _RefreshAll(pb.PressedIcon as DependencyObject, visited, inComboBox);
                _RefreshAll(pb.DisabledIcon as DependencyObject, visited, inComboBox);
                break;
            case PushToggleButton ptb:
                _RefreshAll(ptb.NormalIcon as DependencyObject, visited, inComboBox);
                _RefreshAll(ptb.PressedIcon as DependencyObject, visited, inComboBox);
                _RefreshAll(ptb.DisabledIcon as DependencyObject, visited, inComboBox);
                break;
        }

        if (root is FrameworkElement fe)
        {
            if (fe.ContextMenu != null)
                _RefreshAll(fe.ContextMenu, visited, inComboBox);

            if (fe.ToolTip is DependencyObject tip)
                _RefreshAll(tip, visited, inComboBox);
        }
    }

    private sealed class TranslatingConverter : IValueConverter
    {
        private readonly IValueConverter? _inner;

        public TranslatingConverter(IValueConverter? inner)
        {
            _inner = inner;
        }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var converted = _inner != null
                ? _inner.Convert(value!, targetType, parameter!, culture)
                : value;

            return converted switch
            {
                string text => TranslateDisplayText(text),
                Enum enumValue => TranslateDisplayText(enumValue.ToString()),
                _ => converted!
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (_inner != null)
                return _inner.ConvertBack(value!, targetType, parameter!, culture)!;

            return value!;
        }
    }

    /**
     * 翻译所有控件
     * 因为劫持的 dll 拉起比窗口创建晚，所以手动刷新一下
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
