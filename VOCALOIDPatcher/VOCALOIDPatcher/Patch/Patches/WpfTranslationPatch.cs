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

    private sealed class TranslationState
    {
        public string? Original;
        public string? LastTranslated;
    }

    private static readonly ConditionalWeakTable<object, TranslationState> OriginalMapping = new();

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
            EnsureComboBoxHandlers(combo);

            if (combo.Dispatcher.HasShutdownStarted)
                return;

            combo.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    ScheduleComboBoxRefresh(combo);
                }
                catch (Exception e)
                {
                    Debug.Print($"WPF combo box refresh failed: {e.Message}");
                }
            }), DispatcherPriority.Loaded);
        }
        catch (Exception e)
        {
            Debug.Print($"WPF combo box refresh failed: {e.Message}");
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
        try
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
                        try
                        {
                            RefreshAll(combo);
                        }
                        catch (Exception e)
                        {
                            Debug.Print($"WPF combo box layout refresh failed: {e.Message}");
                        }
                    };
                }
            }

            if (obj is not FrameworkElement fe || fe.Dispatcher.HasShutdownStarted)
                return;

            fe.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    RefreshAll(fe);
                }
                catch (Exception e)
                {
                    Debug.Print($"WPF combo box refresh failed: {e.Message}");
                }
            }), DispatcherPriority.Loaded);
            fe.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    RefreshAll(fe);
                }
                catch (Exception e)
                {
                    Debug.Print($"WPF combo box refresh failed: {e.Message}");
                }
            }), DispatcherPriority.ApplicationIdle);
        }
        catch (Exception e)
        {
            Debug.Print($"WPF combo box refresh failed: {e.Message}");
        }
    }

    public static void MarkUntranslatable(DependencyObject obj)
    {
        obj.SetValue(UntranslatableProperty, true);
    }

    private static string GetOriginal(object obj, string? translated)
    {
        if (Untranslatable.TryGetValue(obj, out _))
            return translated ?? "";

        var state = OriginalMapping.GetValue(obj, _ => new TranslationState());

        if (string.IsNullOrEmpty(translated))
            return state.Original ?? translated ?? "";

        if (state.Original != null
            && (translated == state.Original || translated == state.LastTranslated))
            return state.Original;

        state.Original =
            TranslationManager.TranslatedToOriginalMap.TryGetValue(translated, out var original)
            && !string.IsNullOrEmpty(original)
                ? original
                : translated;
        state.LastTranslated = null;
        return state.Original;
    }

    private static void RememberTranslated(object obj, string translated)
    {
        if (Untranslatable.TryGetValue(obj, out _))
            return;

        var state = OriginalMapping.GetValue(obj, _ => new TranslationState());
        state.LastTranslated = translated;
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
        if (string.IsNullOrEmpty(value))
            return value ?? "";

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

        if (separatorLength == 2 && TryGetControlParameterName(right, out var parameterName))
            return left + value.Substring(separatorIndex, separatorLength) + (parameterName ?? right);

        var translatedLeft = TryGetTranslatedText(left, preferHardcoded: true);
        var translatedRight = TryGetTranslatedText(right, preferHardcoded: true);

        if (translatedLeft == null && translatedRight == null)
            return value;

        return (translatedLeft ?? left) + value.Substring(separatorIndex, separatorLength) + (translatedRight ?? right);
    }

    private static bool TryGetControlParameterName(string value, out string? translated)
    {
        translated = null;

        if (TranslationManager.HardcodedPropertyMapping.TryGetValue(value, out var key) &&
            key.StartsWith("Enum_ControlParameterTypeEnum_", StringComparison.Ordinal))
        {
            if (Settings.TranslateHardcodedStrings)
                translated = TranslationManager.Get(key);

            return true;
        }

        if (TranslationManager.TranslatedToTranslationKeyMap.TryGetValue(value, out var translatedKey) &&
            translatedKey.StartsWith("Enum_ControlParameterTypeEnum_", StringComparison.Ordinal))
        {
            translated = value;
            return true;
        }

        return false;
    }

    private static bool IsDataBound(DependencyObject obj, DependencyProperty dp)
    {
        return BindingOperations.GetBindingExpressionBase(obj, dp) != null;
    }

    private static bool IsInsideFlatMenuComboBox(DependencyObject element)
    {
        DependencyObject? current = element;

        while (current != null)
        {
            if (current is FlatMenuComboBox)
                return true;

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return false;
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

    private static void TranslateElement(object element, bool inComboBox = false,
        ContentPresenter? selectionPresenter = null)
    {
        if (element is ComboBox combo)
            EnsureComboBoxHandlers(combo);

        if (element is DependencyObject dep && (bool)dep.GetValue(UntranslatableProperty))
            return;

        if (element is DependencyObject flatMenuElement && IsInsideFlatMenuComboBox(flatMenuElement))
            return;

        if (Untranslatable.TryGetValue(element, out _))
            return;

        switch (element)
        {
            case HeaderedItemsControl hic:
                if (hic.Header is string hicHeader)
                {
                    var translated = GetTranslatedText(GetOriginal(hic, hicHeader));
                    hic.Header = translated;
                    RememberTranslated(hic, translated);
                }
                break;

            case HeaderedContentControl hcc:
                if (hcc.Header is string hccHeader)
                {
                    var translated = GetTranslatedText(GetOriginal(hcc, hccHeader));
                    hcc.Header = translated;
                    RememberTranslated(hcc, translated);
                }
                break;

            case ContentControl cc:
                if (cc.Content is string text)
                {
                    var original = GetOriginal(cc, text);
                    var translated = inComboBox
                        ? TranslateDisplayText(original)
                        : GetTranslatedText(original);
                    cc.Content = translated;
                    RememberTranslated(cc, translated);
                }
                break;

            case TextBlock tb:
                if (!IsDataBound(tb, TextBlock.TextProperty))
                {
                    if (selectionPresenter != null)
                    {
                        BindSelectionText(tb, selectionPresenter);
                    }
                    else
                    {
                        var original = GetOriginal(tb, tb.Text);
                        var translated = inComboBox
                            ? TranslateDisplayText(original)
                            : GetTranslatedText(original);
                        tb.Text = translated;
                        RememberTranslated(tb, translated);
                    }
                }
                else if (inComboBox)
                {
                    TranslateBoundText(tb);
                }
                break;
        }

        if (element is FrameworkElement { ToolTip: string tip } fe)
        {
            var translated = GetTranslatedText(GetOriginal(fe, tip));
            fe.ToolTip = translated;
            RememberTranslated(fe, translated);
        }
    }

    private static void BindSelectionText(TextBlock textBlock, ContentPresenter selectionPresenter)
    {
        try
        {
            var binding = new Binding
            {
                Source = selectionPresenter,
                Path = new PropertyPath(ContentPresenter.ContentProperty),
                Mode = BindingMode.OneWay,
                Converter = new TranslatingConverter(null)
            };

            BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, binding);
        }
        catch (Exception e)
        {
            Debug.Print($"WPF combo box selection text binding failed: {e.Message}");
        }
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

    private static void _RefreshAll(DependencyObject? root, HashSet<DependencyObject> visited,
        bool inComboBox = false, ContentPresenter? selectionPresenter = null)
    {
        if (root == null || !visited.Add(root)) return;

        if (root is ComboBox combo)
        {
            EnsureComboBoxHandlers(combo);
            inComboBox = true;
        }
        else if (root is ComboBoxItem)
            inComboBox = true;

        if (root is ContentPresenter presenter
            && presenter.TemplatedParent is ComboBox owner
            && owner.SelectionBoxItem != null
            && ReferenceEquals(presenter.Content, owner.SelectionBoxItem))
            selectionPresenter = presenter;

        TranslateElement(root, inComboBox, selectionPresenter);

        if (root is AccessText)
            return;

        if (root is Visual or Visual3D)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                _RefreshAll(child, visited, inComboBox, selectionPresenter);
            }
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject dep)
                _RefreshAll(dep, visited, inComboBox, selectionPresenter);

        if (root is ItemsControl ic)
            foreach (var item in ic.Items)
            {
                var container = ic.ItemContainerGenerator.ContainerFromItem(item);
                if (container is not null)
                    _RefreshAll(container, visited, inComboBox, selectionPresenter);
            }

        switch (root)
        {
            case PushButton pb:
                _RefreshAll(pb.NormalIcon as DependencyObject, visited, inComboBox, selectionPresenter);
                _RefreshAll(pb.PressedIcon as DependencyObject, visited, inComboBox, selectionPresenter);
                _RefreshAll(pb.DisabledIcon as DependencyObject, visited, inComboBox, selectionPresenter);
                break;
            case PushToggleButton ptb:
                _RefreshAll(ptb.NormalIcon as DependencyObject, visited, inComboBox, selectionPresenter);
                _RefreshAll(ptb.PressedIcon as DependencyObject, visited, inComboBox, selectionPresenter);
                _RefreshAll(ptb.DisabledIcon as DependencyObject, visited, inComboBox, selectionPresenter);
                break;
        }

        if (root is FrameworkElement fe)
        {
            if (fe.ContextMenu != null)
                _RefreshAll(fe.ContextMenu, visited, inComboBox, selectionPresenter);

            if (fe.ToolTip is DependencyObject tip)
                _RefreshAll(tip, visited, inComboBox, selectionPresenter);
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
            if (_inner is Yamaha.VOCALOID.MusicalEditor.ControlParameterTypeEnumShortToStringConverter)
            {
                if (value is not ControlParameterTypeEnum)
                    return string.Empty;

                try
                {
                    return _inner.Convert(value, targetType, parameter!, culture)!;
                }
                catch (Exception e)
                {
                    Debug.Print($"WPF converter failed: {e.Message}");
                    return string.Empty;
                }
            }

            object? converted;
            try
            {
                converted = _inner != null
                    ? _inner.Convert(value!, targetType, parameter!, culture)
                    : value;
            }
            catch (Exception e)
            {
                Debug.Print($"WPF converter failed: {e.Message}");
                converted = value;
            }

            return converted switch
            {
                string text => TranslateDisplayText(text),
                Enum enumValue => TranslateDisplayText(enumValue.ToString()),
                _ => converted!
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (_inner == null)
                return value!;

            try
            {
                return _inner.ConvertBack(value!, targetType, parameter!, culture)!;
            }
            catch (Exception e)
            {
                Debug.Print($"WPF converter failed: {e.Message}");
                return value!;
            }
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
