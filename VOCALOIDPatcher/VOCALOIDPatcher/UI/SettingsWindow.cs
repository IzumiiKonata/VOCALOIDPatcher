using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.UI;

public class SettingsWindow : Window
{
    private const string GitHubUrl = "https://github.com/IzumiiKonata/VOCALOIDPatcher";
    private const string AuthorUrl = "https://space.bilibili.com/357605683";

    private static readonly int[] AutoSaveIntervals = { 1, 3, 5, 10, 15, 30 };

    private static readonly Brush NavBrush        = Frozen(Color.FromRgb(0x2D, 0x2D, 0x30));
    private static readonly Brush ForegroundBrush = Frozen(Color.FromRgb(0xC8, 0xC8, 0xC8));
    private static readonly Brush MutedBrush      = Frozen(Color.FromRgb(0xA0, 0xA0, 0xA0));
    private static readonly Brush AccentBrush     = Frozen(Color.FromRgb(0x29, 0xAB, 0xE2));

    private static SettingsWindow? _instance;

    private readonly List<Action> _localizers = new();
    private readonly ContentControl _content = new();
    private readonly TranslateTransform _rootTransform = new(0, 14);

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
        Width = 680;
        Height = 460;
        MinWidth = 520;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = WindowBackground();
        Foreground = ForegroundBrush;
        FontSize = 13;
        Opacity = 0;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

        ApplyTheme();
        BuildUi();

        WpfTranslationPatch.MarkUntranslatable(this);
        TranslationManager.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => TranslationManager.LanguageChanged -= OnLanguageChanged;

        SourceInitialized += (_, _) => EnableDarkTitleBar();
        Loaded += (_, _) => PlayEntrance();

        ApplyLocalization();
    }

    private void BuildUi()
    {
        var root = new Grid { RenderTransform = _rootTransform };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var categories = new (string Key, string Fallback, UIElement Panel)[]
        {
            ("VOCALOIDPatcher_Settings_Category_General", "常规", BuildGeneralPanel()),
            ("VOCALOIDPatcher_Settings_Category_Pianoroll", "钢琴窗", BuildPianorollPanel()),
            ("VOCALOIDPatcher_Settings_Category_Other", "其它", BuildOtherPanel())
        };

        var nav = BuildNav(categories);
        Grid.SetColumn(nav, 0);
        root.Children.Add(nav);

        _content.Margin = new Thickness(28, 26, 28, 28);
        _content.RenderTransform = new TranslateTransform();
        _content.Content = categories[0].Panel;
        Grid.SetColumn(_content, 1);
        root.Children.Add(_content);

        var about = BuildAbout();
        Grid.SetColumn(about, 1);
        root.Children.Add(about);

        Content = root;
    }

    private ListBox BuildNav((string Key, string Fallback, UIElement Panel)[] categories)
    {
        var nav = new ListBox
        {
            Background = NavBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 18, 0, 0),
            Focusable = false
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(nav, ScrollBarVisibility.Disabled);

        foreach (var category in categories)
        {
            var item = new ListBoxItem();
            var captured = category;
            Localize(() => item.Content = T(captured.Key, captured.Fallback));
            nav.Items.Add(item);
        }

        nav.SelectedIndex = 0;
        nav.SelectionChanged += (_, _) =>
        {
            var index = nav.SelectedIndex;
            if (index < 0 || index >= categories.Length)
                return;

            _content.Content = categories[index].Panel;
            AnimateContentIn();
        };

        return nav;
    }

    private StackPanel BuildGeneralPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionTitle("VOCALOIDPatcher_Settings_Category_General", "常规"));

        var languageLabel = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 8),
            Foreground = MutedBrush,
            FontSize = 12
        };
        Localize(() => languageLabel.Text = T("VOCALOIDPatcher_Language_Header", "语言"));

        var languageCombo = new ComboBox
        {
            Width = 280,
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

        var translateHardcoded = Toggle("VOCALOIDPatcher_TranslateHardcodedStrings_Header", "翻译硬编码字符串",
            Settings.TranslateHardcodedStrings, new Thickness(0, 26, 0, 0), checkbox =>
            {
                var enabled = checkbox.IsChecked == true;
                Settings.TranslateHardcodedStrings = enabled;
                WpfTranslationPatch.ReTranslate();

                if (!enabled)
                    Debug.ShowMessageBox(
                        T("VOCALOIDPatcher_TranslateHardcodedStringsRestart", "重启编辑器以应用更改。"));
            });
        panel.Children.Add(translateHardcoded);

        return panel;
    }

    private StackPanel BuildPianorollPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionTitle("VOCALOIDPatcher_Settings_Category_Pianoroll", "钢琴窗"));

        var skipMuted = Toggle("VOCALOIDPatcher_SkipMutedTracks_Header", "跳过静音轨道",
            Settings.ShowOtherTracksSkipMuted, new Thickness(28, 16, 0, 0), checkbox =>
            {
                Settings.ShowOtherTracksSkipMuted = checkbox.IsChecked == true;
                ShowOtherTracksNotesPatch.RefreshPianoroll();
            });
        skipMuted.IsEnabled = Settings.ShowOtherTracksNotes;

        var showOtherTracks = Toggle("VOCALOIDPatcher_ShowOtherTracksNotes_Header", "显示其他轨道的音符",
            Settings.ShowOtherTracksNotes, new Thickness(0, 6, 0, 0), checkbox =>
            {
                var enabled = checkbox.IsChecked == true;
                Settings.ShowOtherTracksNotes = enabled;
                skipMuted.IsEnabled = enabled;
                ShowOtherTracksNotesPatch.RefreshPianoroll();
            });

        var showNotePitch = Toggle("VOCALOIDPatcher_ShowNotePitch_Header", "显示音符音高",
            Settings.ShowNotePitch, new Thickness(0, 18, 0, 0), checkbox =>
            {
                Settings.ShowNotePitch = checkbox.IsChecked == true;
                ShowOtherTracksNotesPatch.RefreshPianoroll();
            });

        var artOptions = new StackPanel
        {
            Margin = new Thickness(28, 12, 0, 0),
            IsEnabled = Settings.ShowCharacterArt,
            Opacity = Settings.ShowCharacterArt ? 1.0 : 0.4
        };
        artOptions.Children.Add(SliderRow("VOCALOIDPatcher_CharacterArtSize_Header", "封面大小",
            80, 480, Settings.CharacterArtSize,
            v => { Settings.CharacterArtSize = (int)v; CharacterArtPatch.RefreshArt(); }));
        artOptions.Children.Add(SliderRow("VOCALOIDPatcher_CharacterArtOpacity_Header", "不透明度",
            0.1, 1.0, Settings.CharacterArtOpacity,
            v => { Settings.CharacterArtOpacity = v; CharacterArtPatch.RefreshArt(); }));

        var showCharacterArt = Toggle("VOCALOIDPatcher_ShowCharacterArt_Header", "显示声库封面",
            Settings.ShowCharacterArt, new Thickness(0, 18, 0, 0), checkbox =>
            {
                var enabled = checkbox.IsChecked == true;
                Settings.ShowCharacterArt = enabled;
                artOptions.IsEnabled = enabled;
                artOptions.Opacity = enabled ? 1.0 : 0.4;
                ShowOtherTracksNotesPatch.RefreshPianoroll();
            });

        panel.Children.Add(showOtherTracks);
        panel.Children.Add(skipMuted);
        panel.Children.Add(showNotePitch);
        panel.Children.Add(showCharacterArt);
        panel.Children.Add(artOptions);

        return panel;
    }

    private FrameworkElement SliderRow(string key, string fallback, double min, double max, double value,
        Action<double> onChanged)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

        var label = new TextBlock
        {
            Width = 72,
            Foreground = MutedBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Localize(() => label.Text = T(key, fallback));

        var slider = new Slider
        {
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center,
            Minimum = min,
            Maximum = max,
            Value = value
        };
        slider.ValueChanged += (_, _) => onChanged(slider.Value);

        row.Children.Add(label);
        row.Children.Add(slider);
        return row;
    }

    private StackPanel BuildOtherPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionTitle("VOCALOIDPatcher_Settings_Category_Other", "其它"));

        var intervalRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(28, 16, 0, 0),
            IsEnabled = Settings.AutoSaveEnabled
        };

        var autoSave = Toggle("VOCALOIDPatcher_AutoSave_Header", "定时保存",
            Settings.AutoSaveEnabled, new Thickness(0, 6, 0, 0), checkbox =>
            {
                Settings.AutoSaveEnabled = checkbox.IsChecked == true;
                intervalRow.IsEnabled = Settings.AutoSaveEnabled;
                AutoSaveService.UpdateFromSettings();
            });

        var intervalLabel = new TextBlock
        {
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Localize(() => intervalLabel.Text = T("VOCALOIDPatcher_AutoSave_Interval_Header", "保存间隔"));

        var intervalCombo = new ComboBox
        {
            Width = 80,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = AutoSaveIntervals,
            SelectedItem = Settings.AutoSaveIntervalMinutes
        };
        intervalCombo.SelectionChanged += (_, _) =>
        {
            if (intervalCombo.SelectedItem is not int minutes)
                return;

            Settings.AutoSaveIntervalMinutes = minutes;
            AutoSaveService.UpdateFromSettings();
        };

        var minutesLabel = new TextBlock
        {
            Foreground = MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Localize(() => minutesLabel.Text = T("VOCALOIDPatcher_Minutes_Suffix", "分钟"));

        intervalRow.Children.Add(intervalLabel);
        intervalRow.Children.Add(intervalCombo);
        intervalRow.Children.Add(minutesLabel);

        panel.Children.Add(autoSave);
        panel.Children.Add(intervalRow);

        return panel;
    }

    private CheckBox Toggle(string key, string fallback, bool initial, Thickness margin, Action<CheckBox> onClick)
    {
        var checkbox = new CheckBox
        {
            Margin = margin,
            IsChecked = initial
        };
        Localize(() => checkbox.Content = T(key, fallback));
        checkbox.Click += (_, _) => onClick(checkbox);
        return checkbox;
    }

    private TextBlock SectionTitle(string key, string fallback)
    {
        var title = new TextBlock
        {
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = ForegroundBrush,
            Margin = new Thickness(0, 0, 0, 18)
        };
        Localize(() => title.Text = T(key, fallback));
        return title;
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
            Margin = new Thickness(0, 0, 24, 18),
            Opacity = 0.55,
            Foreground = MutedBrush,
            FontSize = 12,
            LineHeight = 18
        };
        Panel.SetZIndex(about, 10);

        about.Inlines.Add(new Run(versionText));
        about.Inlines.Add(new LineBreak());
        about.Inlines.Add(Link("GitHub", GitHubUrl));
        about.Inlines.Add(new Run("  ·  Made with ❤ by "));
        about.Inlines.Add(Link("IzumiiKonata", AuthorUrl));

        return about;
    }

    private Hyperlink Link(string text, string url)
    {
        var link = new Hyperlink(new Run(text))
        {
            NavigateUri = new Uri(url),
            Foreground = AccentBrush,
            TextDecorations = null
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

    private void PlayEntrance()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(280))) { EasingFunction = ease });
        _rootTransform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, new Duration(TimeSpan.FromMilliseconds(380))) { EasingFunction = ease });
    }

    private void AnimateContentIn()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _content.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(220))) { EasingFunction = ease });
        if (_content.RenderTransform is TranslateTransform t)
            t.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(10, 0, new Duration(TimeSpan.FromMilliseconds(260))) { EasingFunction = ease });
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private void EnableDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int enabled = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref enabled, sizeof(int));
        }
        catch (Exception e)
        {
            Debug.Print($"设置深色标题栏失败: {e.Message}");
        }
    }

    private static LinearGradientBrush WindowBackground()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x22, 0x22, 0x26), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x1A, 0x1A, 0x1C), 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void ApplyTheme()
    {
        AddImplicitStyle(typeof(ListBoxItem), NavItemStyle);
        AddImplicitStyle(typeof(CheckBox), ToggleSwitchStyle);
        AddImplicitStyle(typeof(ComboBox), ComboBoxStyle);
        AddImplicitStyle(typeof(ComboBoxItem), ComboBoxItemStyle);
        AddImplicitStyle(typeof(Slider), SliderStyle);
    }

    private void AddImplicitStyle(Type targetType, string xaml)
    {
        try
        {
            if (XamlReader.Parse(xaml) is Style style)
                Resources[targetType] = style;
        }
        catch (Exception e)
        {
            Debug.Print($"加载 {targetType.Name} 样式失败: {e.Message}");
        }
    }

    private const string Ns =
        "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";

    private static readonly string NavItemStyle = $@"
<Style {Ns} TargetType='ListBoxItem'>
  <Setter Property='Foreground' Value='#A0A0A0'/>
  <Setter Property='FontSize' Value='13'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ListBoxItem'>
        <Border Margin='6,1'>
          <Grid>
            <Border x:Name='selBg' CornerRadius='8' Background='#3277A0' Opacity='0'/>
            <Border x:Name='hoverBg' CornerRadius='8' Background='#35353A' Opacity='0'/>
            <ContentPresenter VerticalAlignment='Center' Margin='14,9'/>
          </Grid>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'>
            <Trigger.EnterActions>
              <BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='hoverBg' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.16'/>
              </Storyboard></BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
              <BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='hoverBg' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.16'/>
              </Storyboard></BeginStoryboard>
            </Trigger.ExitActions>
          </Trigger>
          <Trigger Property='IsSelected' Value='True'>
            <Setter Property='Foreground' Value='#FFFFFF'/>
            <Trigger.EnterActions>
              <BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='selBg' Storyboard.TargetProperty='Opacity' To='1' Duration='0:0:0.22'/>
              </Storyboard></BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
              <BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='selBg' Storyboard.TargetProperty='Opacity' To='0' Duration='0:0:0.18'/>
              </Storyboard></BeginStoryboard>
            </Trigger.ExitActions>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    private static readonly string ToggleSwitchStyle = $@"
<Style {Ns} TargetType='CheckBox'>
  <Setter Property='Foreground' Value='#C8C8C8'/>
  <Setter Property='FontSize' Value='13'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='CheckBox'>
        <StackPanel Orientation='Horizontal' Background='Transparent'>
          <Border Width='42' Height='22' CornerRadius='11' VerticalAlignment='Center'>
            <Border.Background><SolidColorBrush x:Name='trackBrush' Color='#45454D'/></Border.Background>
            <Border Width='16' Height='16' CornerRadius='8' HorizontalAlignment='Left' Margin='3,0,0,0'>
              <Border.Background><SolidColorBrush x:Name='thumbBrush' Color='#D0D0D6'/></Border.Background>
              <Border.RenderTransform><TranslateTransform x:Name='thumbT' X='0'/></Border.RenderTransform>
              <Border.Effect><DropShadowEffect BlurRadius='4' ShadowDepth='0' Opacity='0.35' Color='#000000'/></Border.Effect>
            </Border>
          </Border>
          <ContentPresenter VerticalAlignment='Center' Margin='12,0,0,0' RecognizesAccessKey='True'/>
        </StackPanel>
        <ControlTemplate.Triggers>
          <Trigger Property='IsChecked' Value='True'>
            <Trigger.EnterActions>
              <BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='thumbT' Storyboard.TargetProperty='X' To='20' Duration='0:0:0.22'>
                  <DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseInOut'/></DoubleAnimation.EasingFunction>
                </DoubleAnimation>
                <ColorAnimation Storyboard.TargetName='trackBrush' Storyboard.TargetProperty='Color' To='#29ABE2' Duration='0:0:0.22'/>
                <ColorAnimation Storyboard.TargetName='thumbBrush' Storyboard.TargetProperty='Color' To='#FFFFFF' Duration='0:0:0.22'/>
              </Storyboard></BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
              <BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='thumbT' Storyboard.TargetProperty='X' To='0' Duration='0:0:0.22'>
                  <DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseInOut'/></DoubleAnimation.EasingFunction>
                </DoubleAnimation>
                <ColorAnimation Storyboard.TargetName='trackBrush' Storyboard.TargetProperty='Color' To='#45454D' Duration='0:0:0.22'/>
                <ColorAnimation Storyboard.TargetName='thumbBrush' Storyboard.TargetProperty='Color' To='#D0D0D6' Duration='0:0:0.22'/>
              </Storyboard></BeginStoryboard>
            </Trigger.ExitActions>
          </Trigger>
          <Trigger Property='IsEnabled' Value='False'>
            <Setter Property='Opacity' Value='0.4'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    private static readonly string ComboBoxStyle = $@"
<Style {Ns} TargetType='ComboBox'>
  <Setter Property='Foreground' Value='#C8C8C8'/>
  <Setter Property='FontSize' Value='13'/>
  <Setter Property='Height' Value='34'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ComboBox'>
        <Grid>
          <ToggleButton Focusable='False' ClickMode='Press'
                        IsChecked='{{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={{RelativeSource TemplatedParent}}}}'>
            <ToggleButton.Template>
              <ControlTemplate TargetType='ToggleButton'>
                <Border CornerRadius='8' BorderThickness='1'>
                  <Border.Background><SolidColorBrush Color='#2A2A2E'/></Border.Background>
                  <Border.BorderBrush><SolidColorBrush x:Name='bb' Color='#3F3F46'/></Border.BorderBrush>
                  <Path HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,0'
                        Data='M0,0 L8,0 L4,5 Z' Fill='#A0A0A0'/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property='IsMouseOver' Value='True'>
                    <Trigger.EnterActions>
                      <BeginStoryboard><Storyboard>
                        <ColorAnimation Storyboard.TargetName='bb' Storyboard.TargetProperty='Color' To='#29ABE2' Duration='0:0:0.18'/>
                      </Storyboard></BeginStoryboard>
                    </Trigger.EnterActions>
                    <Trigger.ExitActions>
                      <BeginStoryboard><Storyboard>
                        <ColorAnimation Storyboard.TargetName='bb' Storyboard.TargetProperty='Color' To='#3F3F46' Duration='0:0:0.18'/>
                      </Storyboard></BeginStoryboard>
                    </Trigger.ExitActions>
                  </Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </ToggleButton.Template>
          </ToggleButton>
          <ContentPresenter IsHitTestVisible='False' Margin='12,0,30,0'
                            VerticalAlignment='Center' HorizontalAlignment='Left'
                            Content='{{TemplateBinding SelectionBoxItem}}'
                            ContentTemplate='{{TemplateBinding SelectionBoxItemTemplate}}'
                            ContentStringFormat='{{TemplateBinding SelectionBoxItemStringFormat}}'/>
          <Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' Focusable='False'
                 IsOpen='{{TemplateBinding IsDropDownOpen}}' PopupAnimation='Fade'>
            <Border MinWidth='{{TemplateBinding ActualWidth}}' MaxHeight='{{TemplateBinding MaxDropDownHeight}}'
                    CornerRadius='8' Background='#252528' BorderBrush='#3F3F46' BorderThickness='1'
                    Margin='0,4,0,6' Padding='0,5' SnapsToDevicePixels='True'>
              <Border.Effect><DropShadowEffect BlurRadius='14' ShadowDepth='2' Opacity='0.5' Color='#000000'/></Border.Effect>
              <ScrollViewer SnapsToDevicePixels='True'>
                <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/>
              </ScrollViewer>
            </Border>
          </Popup>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    private static readonly string ComboBoxItemStyle = $@"
<Style {Ns} TargetType='ComboBoxItem'>
  <Setter Property='Foreground' Value='#C8C8C8'/>
  <Setter Property='Padding' Value='12,8'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ComboBoxItem'>
        <Border CornerRadius='6' Margin='5,2' Padding='{{TemplateBinding Padding}}'>
          <Border.Background><SolidColorBrush x:Name='bg' Color='#003277A0'/></Border.Background>
          <ContentPresenter/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsHighlighted' Value='True'>
            <Setter Property='Foreground' Value='#FFFFFF'/>
            <Trigger.EnterActions>
              <BeginStoryboard><Storyboard>
                <ColorAnimation Storyboard.TargetName='bg' Storyboard.TargetProperty='Color' To='#FF3277A0' Duration='0:0:0.12'/>
              </Storyboard></BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
              <BeginStoryboard><Storyboard>
                <ColorAnimation Storyboard.TargetName='bg' Storyboard.TargetProperty='Color' To='#003277A0' Duration='0:0:0.12'/>
              </Storyboard></BeginStoryboard>
            </Trigger.ExitActions>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    private static readonly string SliderStyle = $@"
<Style {Ns} TargetType='Slider'>
  <Setter Property='Height' Value='24'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='Slider'>
        <Grid VerticalAlignment='Center'>
          <Border Height='4' CornerRadius='2' Background='#3F3F46' VerticalAlignment='Center'/>
          <Track x:Name='PART_Track'>
            <Track.DecreaseRepeatButton>
              <RepeatButton Focusable='False' Command='{{x:Static Slider.DecreaseLarge}}'>
                <RepeatButton.Template>
                  <ControlTemplate TargetType='RepeatButton'>
                    <Border Height='4' CornerRadius='2' Background='#29ABE2' VerticalAlignment='Center'/>
                  </ControlTemplate>
                </RepeatButton.Template>
              </RepeatButton>
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Focusable='False' Command='{{x:Static Slider.IncreaseLarge}}'>
                <RepeatButton.Template>
                  <ControlTemplate TargetType='RepeatButton'>
                    <Border Background='#00000000'/>
                  </ControlTemplate>
                </RepeatButton.Template>
              </RepeatButton>
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
              <Thumb Focusable='False'>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'>
                    <Ellipse Width='14' Height='14' Fill='#FFFFFF'>
                      <Ellipse.Effect><DropShadowEffect BlurRadius='4' ShadowDepth='0' Opacity='0.4' Color='#000000'/></Ellipse.Effect>
                    </Ellipse>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
}
