using System;
using System.Windows;
using System.Windows.Controls;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.UI.Widgets;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Patch.Patches;

public static class SpectrumWidget
{
    private static SpectrumView? _current;

    public static void Install()
    {
        try
        {
            var toolbar = ReflectionUtils.GetMainWindowField<UserControl>("xTrackToolbarView");
            var panel = ReflectionUtils.GetField<Panel>(toolbar, "xToolbarPanel");
            var transport = ReflectionUtils.GetField<UIElement>(toolbar, "xTransportDisplay");

            foreach (UIElement child in panel.Children)
                if (child is SpectrumView)
                    return;

            var view = new SpectrumView
            {
                Margin = new Thickness(10, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Enabled = Settings.SpectrumVisualizer
            };

            var index = panel.Children.IndexOf(transport);
            if (index >= 0)
                panel.Children.Insert(index + 1, view);
            else
                panel.Children.Add(view);

            _current = view;
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_SpectrumWidget_InjectFailed", e.Message));
        }
    }

    public static void SetEnabled(bool enabled)
    {
        var view = _current;
        if (view == null) return;
        view.Dispatcher.Invoke(() => view.Enabled = enabled);
    }
}
