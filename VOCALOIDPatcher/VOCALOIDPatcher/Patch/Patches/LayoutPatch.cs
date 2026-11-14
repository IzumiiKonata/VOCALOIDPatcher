namespace VOCALOIDPatcher.Patch.Patches;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;

public static class LayoutPatch
{
    private sealed class ResourceBackup
    {
        public bool Exists;
        public object? Value;
    }

    private static readonly (string Key, Color Color)[] V6MenuBrushes =
    {
        ("ThemeMenuBackgroundBrush", Color.FromRgb(0x17, 0x18, 0x19)),
        ("ThemeMenuTextBrush", Color.FromRgb(0xFF, 0xFF, 0xFF)),
        ("ThemeMenuHighlightBrush", Color.FromRgb(0x3F, 0x3F, 0x46)),
        ("ThemeMenuHighlightBorderBrush", Color.FromRgb(0x3F, 0x3F, 0x46)),
        ("ThemeMenuHighlightTextBrush", Color.FromRgb(0xFF, 0xFF, 0xFF)),
        ("ThemeMenuBorderBrush", Color.FromRgb(0x1A, 0x1A, 0x1A)),
        ("ThemeMenuSeparatorBrush", Color.FromRgb(0x78, 0x78, 0x78)),
        ("ThemeMenuDisabledTextBrush", Color.FromRgb(0x80, 0x80, 0x80)),
        ("ThemeCheckBoxBackgroundBrush", Color.FromRgb(0x00, 0x00, 0x00)),
        ("ThemeCheckBoxBorderBrush", Color.FromRgb(0x3F, 0x3F, 0x46))
    };

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmColorDefault = unchecked((int)0xFFFFFFFF);

    private static readonly Dictionary<string, ResourceBackup> ToolbarBackups = new();
    private static readonly Dictionary<int, int> TitleBarBackups = new();

    private static bool _toolbarStyleApplied;
    private static bool _inspectorOnRightApplied;
    private static bool _installed;
    private static IntPtr _titleBarHandle;

    private static Grid? _mainGrid;
    private static ColumnDefinition? _leftZoneColumn;
    private static ColumnDefinition? _placeholderColumn;
    private static GridSplitter? _leftSplitter;
    private static FrameworkElement? _leftZone;
    private static Grid? _leftZoneHost;

    private static Panel? _inspectorButtonSource;
    private static Panel? _inspectorButtonTarget;
    private static int _inspectorButtonIndex;
    private static bool _inspectorButtonsMoved;
    private static bool _inspectorIconsChanged;
    private static object? _inspectorNormalIcon;
    private static object? _inspectorPressedIcon;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    public static void Install()
    {
        if (_installed)
            return;

        _installed = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is MainWindow window)
                    ApplySettings(window);
            }));

        if (Application.Current?.MainWindow is MainWindow { IsLoaded: true } mainWindow)
            ApplySettings(mainWindow);
    }

    public static void ApplyFromSettings()
    {
        var window = GetMainWindow();
        if (window == null)
            return;

        ApplySettings(window);
    }

    private static void ApplySettings(MainWindow window)
    {
        try
        {
            ApplyInspectorOnRightCore(window, Settings.InspectorOnRight);
            ApplyV6ToolbarStyleCore(window, Settings.V6ToolbarStyle);

            if (Settings.V6ToolbarStyle || _titleBarHandle != IntPtr.Zero)
                ApplyV6TitleBar(window, Settings.V6ToolbarStyle);
        }
        catch (Exception e)
        {
            Debug.Print($"LayoutPatch: failed to apply layout settings: {e.Message}");
        }
    }

    public static void ApplyV6ToolbarStyle(bool enabled)
    {
        var window = GetMainWindow();
        if (window == null)
            return;

        if (window.Dispatcher.CheckAccess())
            ApplyV6ToolbarStyleCore(window, enabled);
        else
            window.Dispatcher.Invoke(() => ApplyV6ToolbarStyleCore(window, enabled));
    }

    private static void ApplyV6ToolbarStyleCore(MainWindow window, bool enabled)
    {
        if (enabled == _toolbarStyleApplied)
            return;

        if (enabled)
        {
            foreach (var (key, color) in V6MenuBrushes)
            {
                if (!ToolbarBackups.ContainsKey(key))
                {
                    ToolbarBackups[key] = new ResourceBackup
                    {
                        Exists = window.Resources.Contains(key),
                        Value = window.Resources.Contains(key) ? window.Resources[key] : null
                    };
                }

                var brush = new SolidColorBrush(color);
                brush.Freeze();
                window.Resources[key] = brush;
            }
        }
        else
        {
            foreach (var (key, _) in V6MenuBrushes)
            {
                if (!ToolbarBackups.TryGetValue(key, out var backup))
                    continue;

                if (backup.Exists)
                    window.Resources[key] = backup.Value;
                else
                    window.Resources.Remove(key);
            }

            ToolbarBackups.Clear();
        }

        ApplyV6TitleBar(window, enabled);
        _toolbarStyleApplied = enabled;
    }

    private static void ApplyV6TitleBar(MainWindow window, bool enabled)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var handleChanged = _titleBarHandle != hwnd;
            _titleBarHandle = hwnd;

            if (enabled)
            {
                if (handleChanged
                    || (!TitleBarBackups.ContainsKey(DwmwaUseImmersiveDarkMode)
                        && !TitleBarBackups.ContainsKey(DwmwaUseImmersiveDarkModeLegacy)))
                {
                    TitleBarBackups.Clear();
                    BackupTitleBarAttribute(DwmwaUseImmersiveDarkMode);
                    BackupTitleBarAttribute(DwmwaUseImmersiveDarkModeLegacy);
                    BackupTitleBarAttribute(DwmwaBorderColor);
                    BackupTitleBarAttribute(DwmwaCaptionColor);
                    BackupTitleBarAttribute(DwmwaTextColor);
                }

                if (!SetTitleBarAttribute(DwmwaUseImmersiveDarkMode, 1))
                    SetTitleBarAttribute(DwmwaUseImmersiveDarkModeLegacy, 1);

                SetTitleBarAttribute(DwmwaCaptionColor, ColorRef(0x2D, 0x2D, 0x30));
                SetTitleBarAttribute(DwmwaTextColor, ColorRef(0xC8, 0xC8, 0xC8));
                SetTitleBarAttribute(DwmwaBorderColor, ColorRef(0x1A, 0x1A, 0x1A));
            }
            else
            {
                var dark = TitleBarBackups.TryGetValue(DwmwaUseImmersiveDarkMode, out var value)
                    ? value
                    : TitleBarBackups.TryGetValue(DwmwaUseImmersiveDarkModeLegacy, out value)
                        ? value
                        : 0;

                if (!SetTitleBarAttribute(DwmwaUseImmersiveDarkMode, dark))
                    SetTitleBarAttribute(DwmwaUseImmersiveDarkModeLegacy, dark);

                RestoreTitleBarColor(DwmwaCaptionColor);
                RestoreTitleBarColor(DwmwaTextColor);
                RestoreTitleBarColor(DwmwaBorderColor);

                _titleBarHandle = IntPtr.Zero;
                TitleBarBackups.Clear();
            }
        }
        catch (Exception e)
        {
            Debug.Print($"LayoutPatch: failed to apply V6 title bar: {e.Message}");
        }
    }

    private static void BackupTitleBarAttribute(int attribute)
    {
        if (DwmGetWindowAttribute(_titleBarHandle, attribute, out var value, sizeof(int)) == 0)
            TitleBarBackups[attribute] = value;
    }

    private static bool SetTitleBarAttribute(int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(_titleBarHandle, attribute, ref value, sizeof(int)) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RestoreTitleBarColor(int attribute)
    {
        var value = TitleBarBackups.TryGetValue(attribute, out var backup) ? backup : DwmColorDefault;
        SetTitleBarAttribute(attribute, value);
    }

    private static int ColorRef(int r, int g, int b) => r | (g << 8) | (b << 16);

    public static void ApplyInspectorOnRight(bool enabled)
    {
        var window = GetMainWindow();
        if (window == null)
            return;

        if (window.Dispatcher.CheckAccess())
            ApplyInspectorOnRightCore(window, enabled);
        else
            window.Dispatcher.Invoke(() => ApplyInspectorOnRightCore(window, enabled));
    }

    private static void ApplyInspectorOnRightCore(MainWindow window, bool enabled)
    {
        ApplyInspectorToolbarButtons(enabled);

        if (enabled == _inspectorOnRightApplied)
            return;

        if (!ResolveElements())
            return;

        var definitions = _mainGrid!.ColumnDefinitions;

        if (enabled)
        {
            var index = definitions.IndexOf(_leftZoneColumn!);
            if (index < 0 || _leftZoneHost == null)
                return;

            _placeholderColumn = new ColumnDefinition { Width = new GridLength(0) };
            definitions.RemoveAt(index);
            definitions.Insert(index, _placeholderColumn);
            definitions.Add(_leftZoneColumn);

            Grid.SetColumn(_leftZoneHost, definitions.Count - 1);
            Grid.SetRowSpan(_leftZoneHost, 3);

            _mainGrid.Children.Remove(_leftSplitter!);
            _inspectorOnRightApplied = true;

            EnsureInspectorWidth();
        }
        else
        {
            definitions.Remove(_leftZoneColumn!);
            if (_placeholderColumn != null)
                definitions.Remove(_placeholderColumn);
            definitions.Insert(0, _leftZoneColumn!);

            if (_leftZoneHost != null)
            {
                Grid.SetColumn(_leftZoneHost, 0);
                Grid.SetRowSpan(_leftZoneHost, 3);
            }

            if (_leftSplitter != null && !_mainGrid.Children.Contains(_leftSplitter))
            {
                _mainGrid.Children.Add(_leftSplitter);
                Grid.SetColumn(_leftSplitter, 1);
                Grid.SetRowSpan(_leftSplitter, 3);
            }

            _placeholderColumn = null;
            _inspectorOnRightApplied = false;

            EnsureInspectorWidth();
        }
    }

    private static void ApplyInspectorToolbarButtons(bool enabled)
    {
        if (enabled == _inspectorButtonsMoved)
            return;

        try
        {
            var toolbar = ReflectionUtils.GetMainWindowField<TrackToolbarView>("xTrackToolbarView");
            var inspector = FindToolbarElement(toolbar, "xShowInspectorButton");
            var help = FindToolbarElement(toolbar, "xShowInspectorHelpButton");

            if (inspector == null || help == null)
                return;

            if (enabled)
            {
                if (inspector.Parent is not Panel source || help.Parent != source)
                    return;

                var target = FindToolbarElement(toolbar, "xShowEditButton")?.Parent as Panel;
                if (target == null || ReferenceEquals(target, source))
                    return;

                _inspectorButtonSource = source;
                _inspectorButtonTarget = target;
                _inspectorButtonIndex = source.Children.IndexOf(inspector);

                if (inspector is PushToggleButton button)
                {
                    _inspectorNormalIcon = button.NormalIcon;
                    _inspectorPressedIcon = button.PressedIcon;
                    _inspectorIconsChanged = true;

                    button.NormalIcon = BuildV6InspectorIcon(source, false);
                    button.PressedIcon = BuildV6InspectorIcon(source, true);
                }

                source.Children.Remove(inspector);
                source.Children.Remove(help);

                var mixer = FindToolbarElement(toolbar, "xShowMixerButton");
                var insertIndex = mixer?.Parent == target
                    ? target.Children.IndexOf(mixer) + 1
                    : target.Children.Count;

                target.Children.Insert(insertIndex, inspector);
                target.Children.Insert(insertIndex + 1, help);
            }
            else
            {
                if (_inspectorButtonSource == null || _inspectorButtonTarget == null)
                    return;

                _inspectorButtonTarget.Children.Remove(inspector);
                _inspectorButtonTarget.Children.Remove(help);

                var index = Math.Min(_inspectorButtonIndex, _inspectorButtonSource.Children.Count);
                _inspectorButtonSource.Children.Insert(index, inspector);
                _inspectorButtonSource.Children.Insert(
                    Math.Min(index + 1, _inspectorButtonSource.Children.Count), help);

                if (_inspectorIconsChanged && inspector is PushToggleButton button)
                {
                    button.NormalIcon = _inspectorNormalIcon!;
                    button.PressedIcon = _inspectorPressedIcon!;
                    _inspectorIconsChanged = false;
                    _inspectorNormalIcon = null;
                    _inspectorPressedIcon = null;
                }

                _inspectorButtonSource = null;
                _inspectorButtonTarget = null;
            }

            _inspectorButtonsMoved = enabled;
        }
        catch (Exception e)
        {
            Debug.Print($"LayoutPatch: failed to move inspector buttons: {e.Message}");
        }
    }

    private static void EnsureInspectorWidth()
    {
        if (_leftZone == null || _leftZoneColumn == null)
            return;

        if (_leftZone.Visibility == Visibility.Visible)
        {
            if (_leftZoneColumn.Width.Value <= 0.0)
            {
                var width = UserSettings.Instance.LeftZoneWidth;
                if (double.IsNaN(width) || width <= 0.0)
                    width = 241.0;

                _leftZoneColumn.Width = new GridLength(width);
            }
        }
        else if (!_inspectorOnRightApplied)
        {
            _leftZoneColumn.Width = new GridLength(0);
        }
    }

    private static bool ResolveElements()
    {
        if (_mainGrid != null && _leftZoneColumn != null && _leftZone != null && _leftSplitter != null)
            return _leftZoneHost != null;

        try
        {
            _mainGrid = ReflectionUtils.GetMainWindowField<Grid>("xMainGrid");
            _leftZoneColumn = ReflectionUtils.GetMainWindowField<ColumnDefinition>("xLeftZoneColumn");
            _leftSplitter = ReflectionUtils.GetMainWindowField<GridSplitter>("xMainGridLeftSplitter");
            _leftZone = ReflectionUtils.GetMainWindowField<FrameworkElement>("xLeftZone");

            _leftZoneHost = _leftZone.Parent as Grid;
            if (_leftZoneHost == null)
            {
                _mainGrid = null;
                _leftZoneColumn = null;
                _leftSplitter = null;
                _leftZone = null;
                Debug.Print("LayoutPatch: xLeftZone parent is not a Grid.");
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            _mainGrid = null;
            _leftZoneColumn = null;
            _leftSplitter = null;
            _leftZone = null;
            Debug.Print($"LayoutPatch: failed to resolve main window elements: {e.Message}");
            return false;
        }
    }

    private static Viewbox BuildV6InspectorIcon(FrameworkElement resourceSource, bool pressed)
    {
        var grid = new Grid { Width = 38, Height = 26 };

        grid.Children.Add(new ContentControl
        {
            Content = ".",
            ContentTemplate = resourceSource.TryFindResource(
                pressed ? "Button_Background_Pressed" : "Button_Background_Normal") as DataTemplate
        });

        var canvas = new Canvas();
        grid.Children.Add(canvas);

        var bracket = new Canvas { Width = 2, Height = 12 };
        Canvas.SetLeft(bracket, 18);
        Canvas.SetTop(bracket, 7);
        canvas.Children.Add(bracket);

        var lower = new Path
        {
            Data = Geometry.Parse("M2,8 L0,8 L0,0 L2,0"),
            Width = 2,
            Height = 8
        };
        lower.SetResourceReference(Shape.FillProperty, "Brush_Button_Foreground");
        Canvas.SetLeft(lower, 0);
        Canvas.SetTop(lower, 4);
        bracket.Children.Add(lower);

        var upper = new Path
        {
            Data = Geometry.Parse("M2,2 L0,2 L0,0 L2,0"),
            Width = 2,
            Height = 2
        };
        upper.SetResourceReference(Shape.FillProperty, "Brush_Button_Foreground");
        Canvas.SetLeft(upper, 0);
        Canvas.SetTop(upper, 0);
        bracket.Children.Add(upper);

        return new Viewbox { Stretch = Stretch.Uniform, Child = grid };
    }

    private static FrameworkElement? FindToolbarElement(TrackToolbarView toolbar, string name)
    {
        if (toolbar.FindName(name) is FrameworkElement element)
            return element;

        return AccessTools.Field(typeof(TrackToolbarView), name)?.GetValue(toolbar) as FrameworkElement;
    }

    private static MainWindow? GetMainWindow()
    {
        try
        {
            return Application.Current?.MainWindow as MainWindow;
        }
        catch
        {
            return null;
        }
    }
}
