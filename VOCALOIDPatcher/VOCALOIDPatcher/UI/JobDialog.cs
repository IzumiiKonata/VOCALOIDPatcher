using System.Collections;
using System.Windows;
using System.Windows.Controls;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;

namespace VOCALOIDPatcher.UI;

internal sealed class JobDialog : Window
{
    private readonly StackPanel _fields = new();

    internal JobDialog(string titleKey, string titleFallback)
    {
        DarkTheme.Apply(this);
        Background = DarkTheme.WindowBackground();
        Foreground = DarkTheme.Foreground;
        FontSize = 13;
        Title = T(titleKey, titleFallback);
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        UseLayoutRounding = true;

        var owner = Application.Current?.MainWindow;
        if (owner != null && !ReferenceEquals(owner, this))
            Owner = owner;

        WpfTranslationPatch.MarkUntranslatable(this);
        SourceInitialized += (_, _) => DarkTheme.EnableDarkTitleBar(this);

        var apply = new Button
        {
            Content = T("VOCALOIDPatcher_Job_Apply", "应用"),
            MinWidth = 84,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        apply.Click += (_, _) => DialogResult = true;

        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 18), MinWidth = 320 };
        root.Children.Add(_fields);
        root.Children.Add(apply);
        Content = root;
    }

    internal bool ShowForApply() => ShowDialog() == true;

    internal Slider AddSlider(string key, string fallback, double min, double max, double value)
    {
        var row = NewRow();
        row.Children.Add(NewLabel(key, fallback));

        var slider = new Slider
        {
            Width = 170,
            VerticalAlignment = VerticalAlignment.Center,
            Minimum = min,
            Maximum = max,
            Value = value
        };
        var valueText = new TextBlock
        {
            Width = 34,
            Foreground = DarkTheme.Muted,
            FontSize = 12,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Text = ((int)value).ToString()
        };
        slider.ValueChanged += (_, _) => valueText.Text = ((int)slider.Value).ToString();

        row.Children.Add(slider);
        row.Children.Add(valueText);
        _fields.Children.Add(row);
        return slider;
    }

    internal TextBox AddTextBox(string key, string fallback, string value)
    {
        var row = NewRow();
        row.Children.Add(NewLabel(key, fallback));

        var box = new TextBox
        {
            Width = 170,
            Text = value,
            VerticalAlignment = VerticalAlignment.Center,
            Background = DarkTheme.FieldBack,
            Foreground = DarkTheme.Foreground,
            CaretBrush = DarkTheme.Foreground,
            BorderBrush = DarkTheme.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4)
        };

        row.Children.Add(box);
        _fields.Children.Add(row);
        return box;
    }

    internal ComboBox AddCombo(string key, string fallback, IEnumerable items, int selectedIndex)
    {
        var row = NewRow();
        row.Children.Add(NewLabel(key, fallback));

        var combo = new ComboBox
        {
            Width = 170,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = items,
            SelectedIndex = selectedIndex
        };

        row.Children.Add(combo);
        _fields.Children.Add(row);
        return combo;
    }

    private static StackPanel NewRow()
        => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

    private static TextBlock NewLabel(string key, string fallback)
        => new()
        {
            Text = T(key, fallback),
            Width = 96,
            Foreground = DarkTheme.Muted,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

    private static string T(string key, string fallback) => TranslationManager.Get(key) ?? fallback;
}
