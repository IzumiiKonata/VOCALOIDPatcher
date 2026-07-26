using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using VOCALOIDPatcher.Formats.LibreSvip.Framework;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;

namespace VOCALOIDPatcher.UI;

internal sealed class FormatOptionDialog : Window
{
    private readonly object _target;
    private readonly List<Func<Action?>> _read = new();

    private FormatOptionDialog(
        SvipFormatInfo info,
        FormatOptionDirection direction,
        IReadOnlyList<FormatOption> options)
    {
        _target = info.Converter;
        DarkTheme.Apply(this);
        Background = DarkTheme.WindowBackground();
        Foreground = DarkTheme.Foreground;
        FontSize = 13;
        Title = TranslationManager.Tr(
            direction == FormatOptionDirection.Import
                ? "VOCALOIDPatcher_FormatOption_ImportTitle"
                : "VOCALOIDPatcher_FormatOption_ExportTitle",
            TranslationManager.Tr(info.NameKey ?? info.DisplayName));
        Width = 460;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 680;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        UseLayoutRounding = true;

        var owner = Application.Current?.MainWindow;
        if (owner != null && !ReferenceEquals(owner, this))
            Owner = owner;

        WpfTranslationPatch.MarkUntranslatable(this);
        SourceInitialized += (_, _) => DarkTheme.EnableDarkTitleBar(this);

        var fields = new StackPanel();
        foreach (var option in options)
            AddOption(fields, option);

        var cancel = new Button
        {
            Content = TranslationManager.Tr("Button_Cancel"),
            MinWidth = 84,
            Margin = new Thickness(0, 0, 10, 0),
        };
        cancel.Click += (_, _) => DialogResult = false;

        var apply = new Button
        {
            Content = TranslationManager.Tr("Button_OK"),
            MinWidth = 84,
            IsDefault = true,
        };
        apply.Click += (_, _) =>
        {
            var setters = new List<Action>();
            foreach (var read in _read)
            {
                var setter = read();
                if (setter == null)
                    return;
                setters.Add(setter);
            }
            foreach (var setter in setters)
                setter();
            DialogResult = true;
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);

        var content = new StackPanel { Margin = new Thickness(24, 20, 24, 18) };
        content.Children.Add(fields);
        content.Children.Add(buttons);
        Content = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    internal static bool Show(SvipFormatInfo info, FormatOptionDirection direction)
    {
        var options = FormatOptionCatalog.Get(info, direction);
        if (options.Count == 0)
            return true;
        return new FormatOptionDialog(info, direction, options).ShowDialog() == true;
    }

    private void AddOption(Panel fields, FormatOption option)
    {
        var property = option.Property;
        var value = property.GetValue(_target);
        if (property.PropertyType == typeof(bool))
        {
            var check = new CheckBox
            {
                Content = TranslationManager.Tr(option.LabelKey),
                IsChecked = value is true,
                Foreground = DarkTheme.Foreground,
                Margin = new Thickness(0, 0, 0, 10),
            };
            fields.Children.Add(check);
            _read.Add(() => () => property.SetValue(_target, check.IsChecked == true));
            return;
        }

        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var label = new TextBlock
        {
            Text = TranslationManager.Tr(option.LabelKey),
            Foreground = DarkTheme.Muted,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        row.Children.Add(label);

        if (property.PropertyType.IsEnum)
            AddEnumEditor(row, property, value);
        else
            AddTextEditor(row, property, value);

        fields.Children.Add(row);
    }

    private void AddEnumEditor(Grid row, PropertyInfo property, object? value)
    {
        var values = Enum.GetValues(property.PropertyType).Cast<object>().ToList();
        var items = values.Select(enumValue => new ComboBoxItem
        {
            Content = TranslationManager.Get($"VOCALOIDPatcher_FormatOption_Value_{enumValue}")
                ?? enumValue.ToString()
                ?? "",
            Tag = enumValue,
        }).ToList();
        var combo = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = Math.Max(0, values.FindIndex(item => Equals(item, value))),
            MinWidth = 190,
        };
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);
        _read.Add(() =>
        {
            if (combo.SelectedItem is ComboBoxItem item)
                return () => property.SetValue(_target, item.Tag);
            return () => { };
        });
    }

    private void AddTextEditor(Grid row, PropertyInfo property, object? value)
    {
        var text = new TextBox
        {
            Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
            MinWidth = 190,
            Background = DarkTheme.FieldBack,
            Foreground = DarkTheme.Foreground,
            CaretBrush = DarkTheme.Foreground,
            BorderBrush = DarkTheme.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
        };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        _read.Add(() =>
        {
            if (!TryConvert(text.Text, property.PropertyType, out var converted))
            {
                MessageBox.Show(
                    TranslationManager.Tr("VOCALOIDPatcher_FormatOption_InvalidValue", TranslationManager.Tr($"VOCALOIDPatcher_FormatOption_{property.Name}")),
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                text.Focus();
                text.SelectAll();
                return null;
            }
            return () => property.SetValue(_target, converted);
        });
    }

    private static bool TryConvert(string text, Type type, out object? value)
    {
        value = null;
        if (type == typeof(string))
        {
            value = text;
            return true;
        }
        if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
        {
            value = integer;
            return true;
        }
        if (type == typeof(double)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            && double.IsFinite(number))
        {
            value = number;
            return true;
        }
        return false;
    }
}
