using System.Windows;
using System.Windows.Controls;
using VOCALOIDPatcher.Patch.Patches;

namespace VOCALOIDPatcher.UI;

internal sealed class ConfirmChoiceDialog : Window
{
    private bool _confirmed;

    private ConfirmChoiceDialog(string title, string message, string primaryLabel, string secondaryLabel)
    {
        DarkTheme.Apply(this);
        Background = DarkTheme.WindowBackground();
        Foreground = DarkTheme.Foreground;
        FontSize = 13;
        Title = title;
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

        var text = new TextBlock
        {
            Text = message,
            Foreground = DarkTheme.Foreground,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 440,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var primary = new Button
        {
            Content = primaryLabel,
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 0)
        };
        primary.Click += (_, _) =>
        {
            _confirmed = true;
            DialogResult = true;
        };

        var secondary = new Button
        {
            Content = secondaryLabel,
            MinWidth = 96
        };
        secondary.Click += (_, _) =>
        {
            _confirmed = false;
            DialogResult = true;
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(primary);
        buttons.Children.Add(secondary);

        var root = new StackPanel { Margin = new Thickness(24, 20, 24, 18), MinWidth = 340 };
        root.Children.Add(text);
        root.Children.Add(buttons);
        Content = root;
    }

    internal static bool Show(string title, string message, string primaryLabel, string secondaryLabel)
    {
        var dialog = new ConfirmChoiceDialog(title, message, primaryLabel, secondaryLabel);
        dialog.ShowDialog();
        return dialog._confirmed;
    }
}
