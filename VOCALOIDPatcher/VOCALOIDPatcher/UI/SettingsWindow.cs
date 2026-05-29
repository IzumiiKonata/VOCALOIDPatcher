using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.UI;

public class SettingsWindow : Window
{
    private const string GitHubUrl = "https://github.com/IzumiiKonata/VOCALOIDPatcher";
    private const string AuthorUrl = "https://space.bilibili.com/357605683";

    private static readonly Brush BackgroundBrush = Frozen(Color.FromRgb(0x2D, 0x2D, 0x30));
    private static readonly Brush PanelBrush      = Frozen(Color.FromRgb(0x25, 0x25, 0x26));
    private static readonly Brush ForegroundBrush = Frozen(Color.FromRgb(0xF0, 0xF0, 0xF0));
    private static readonly Brush AccentBrush     = Frozen(Color.FromRgb(0x4E, 0xC9, 0xB0));

    private static SettingsWindow? _instance;

    private readonly List<Action> _localizers = new();
    private readonly ContentControl _content = new();

    public static void ShowSingleton()
    {
        if (_instance != null)
        {
            _instance.Activate();
            return;
        }

        var window = new SettingsWindow();
        _instance = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_instance, window))
                _instance = null;
        };

        var owner = Application.Current?.MainWindow;
        if (owner != null && !ReferenceEquals(owner, window))
            window.Owner = owner;

        window.Show();
    }

    private SettingsWindow()
    {
        Width = 660;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = BackgroundBrush;
        Foreground = ForegroundBrush;
        FontSize = 13;

        BuildUi();

        WpfTranslationPatch.MarkUntranslatable(this);
        TranslationManager.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => TranslationManager.LanguageChanged -= OnLanguageChanged;

        ApplyLocalization();
    }

    private void BuildUi()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var generalPanel = BuildGeneralPanel();
        var qolPanel = BuildQolPanel();

        var nav = BuildNav(generalPanel, qolPanel);
        Grid.SetColumn(nav, 0);
        root.Children.Add(nav);

        _content.Margin = new Thickness(20);
        _content.Content = generalPanel;
        Grid.SetColumn(_content, 1);
        root.Children.Add(_content);

        var about = BuildAbout();
        Grid.SetColumn(about, 1);
        root.Children.Add(about);

        Content = root;
    }

    private ListBox BuildNav(UIElement generalPanel, UIElement qolPanel)
    {
        var nav = new ListBox
        {
            Background = PanelBrush,
            Foreground = ForegroundBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 12, 0, 0)
        };

        var generalItem = new ListBoxItem { Padding = new Thickness(16, 10, 16, 10) };
        var qolItem = new ListBoxItem { Padding = new Thickness(16, 10, 16, 10) };

        Localize(() => generalItem.Content = T("VOCALOIDPatcher_Settings_Category_General", "常规"));
        Localize(() => qolItem.Content = T("VOCALOIDPatcher_Settings_Category_QoL", "QoL Improvements"));

        nav.Items.Add(generalItem);
        nav.Items.Add(qolItem);
        nav.SelectedIndex = 0;

        nav.SelectionChanged += (_, _) =>
        {
            _content.Content = nav.SelectedIndex == 1 ? qolPanel : generalPanel;
        };

        return nav;
    }

    private StackPanel BuildGeneralPanel()
    {
        var panel = new StackPanel();

        var languageLabel = new TextBlock { Margin = new Thickness(0, 0, 0, 6) };
        Localize(() => languageLabel.Text = T("VOCALOIDPatcher_Language_Header", "语言"));

        var languageCombo = new ComboBox
        {
            Width = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = TranslationManager.AvailableLanguages,
            SelectedItem = TranslationManager.CurrentLanguage
        };
        languageCombo.SelectionChanged += (_, _) =>
        {
            if (languageCombo.SelectedItem is not string lang || lang == TranslationManager.CurrentLanguage)
                return;

            Patcher.ConfigManager.Set("Language", lang);
            TranslationManager.LoadLanguage(lang);
            WpfTranslationPatch.ReTranslate();
        };

        panel.Children.Add(languageLabel);
        panel.Children.Add(languageCombo);

        var translateHardcoded = new CheckBox
        {
            Margin = new Thickness(0, 20, 0, 0),
            Foreground = ForegroundBrush,
            IsChecked = Settings.TranslateHardcodedStrings
        };
        Localize(() => translateHardcoded.Content =
            T("VOCALOIDPatcher_TranslateHardcodedStrings_Header", "翻译硬编码字符串"));
        translateHardcoded.Click += (_, _) =>
        {
            var enabled = translateHardcoded.IsChecked == true;
            Settings.TranslateHardcodedStrings = enabled;
            WpfTranslationPatch.ReTranslate();

            if (!enabled)
            {
                Debug.ShowMessageBox(
                    T("VOCALOIDPatcher_TranslateHardcodedStringsRestart", "重启编辑器以应用更改。"));
            }
        };
        panel.Children.Add(translateHardcoded);

        return panel;
    }

    private StackPanel BuildQolPanel()
    {
        var panel = new StackPanel();

        var showOtherTracks = new CheckBox
        {
            Foreground = ForegroundBrush,
            IsChecked = Settings.ShowOtherTracksNotes
        };
        Localize(() => showOtherTracks.Content =
            T("VOCALOIDPatcher_ShowOtherTracksNotes_Header", "显示其他轨道的音符"));

        var skipMuted = new CheckBox
        {
            Margin = new Thickness(24, 10, 0, 0),
            Foreground = ForegroundBrush,
            IsChecked = Settings.ShowOtherTracksSkipMuted,
            IsEnabled = Settings.ShowOtherTracksNotes
        };
        Localize(() => skipMuted.Content =
            T("VOCALOIDPatcher_SkipMutedTracks_Header", "跳过静音轨道"));

        showOtherTracks.Click += (_, _) =>
        {
            var enabled = showOtherTracks.IsChecked == true;
            Settings.ShowOtherTracksNotes = enabled;
            skipMuted.IsEnabled = enabled;
            ShowOtherTracksNotesPatch.RefreshPianoroll();
        };

        skipMuted.Click += (_, _) =>
        {
            Settings.ShowOtherTracksSkipMuted = skipMuted.IsChecked == true;
            ShowOtherTracksNotesPatch.RefreshPianoroll();
        };

        panel.Children.Add(showOtherTracks);
        panel.Children.Add(skipMuted);

        return panel;
    }

    private TextBlock BuildAbout()
    {
        var versionText = $"VOCALOID Patcher {Patcher.Version}" + (Patcher.VstPluginMode ? " (VSTi)" : "");
#if NET6_0
        versionText += " (.NET 6.0)";
#endif

        var about = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 20, 16),
            Opacity = 0.6,
            Foreground = ForegroundBrush
        };
        Panel.SetZIndex(about, 10);

        about.Inlines.Add(new Run(versionText));
        about.Inlines.Add(new LineBreak());
        about.Inlines.Add(Link("GitHub", GitHubUrl));
        about.Inlines.Add(new Run("  ·  "));
        about.Inlines.Add(Link("Made with ❤ by IzumiiKonata", AuthorUrl));

        return about;
    }

    private Hyperlink Link(string text, string url)
    {
        var link = new Hyperlink(new Run(text))
        {
            NavigateUri = new Uri(url),
            Foreground = AccentBrush
        };
        link.RequestNavigate += (_, e) => BrowseUtils.Browse(e.Uri.ToString());
        return link;
    }

    private void OnLanguageChanged(object? sender, string e) => Dispatcher.Invoke(ApplyLocalization);

    private void Localize(Action setter) => _localizers.Add(setter);

    private void ApplyLocalization()
    {
        Title = T("VOCALOIDPatcher_Settings_Title", "VOCALOID Patcher 设置");
        foreach (var localizer in _localizers)
            localizer();
    }

    private static string T(string key, string fallback) => TranslationManager.Get(key) ?? fallback;

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
