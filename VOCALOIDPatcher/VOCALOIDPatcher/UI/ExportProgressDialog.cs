using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VOCALOIDPatcher.Formats.LibreSvip;
using VOCALOIDPatcher.Patch.Patches;
using VOCALOIDPatcher.Translation;

namespace VOCALOIDPatcher.UI;

internal sealed class ExportProgressDialog : Window
{
    private readonly TextBlock _status;
    private bool _finished;

    private ExportProgressDialog()
    {
        DarkTheme.Apply(this);
        Background = DarkTheme.WindowBackground();
        Foreground = DarkTheme.Foreground;
        FontSize = 13;
        Title = T("VOCALOIDPatcher_Export_ProgressTitle", "Exporting");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        UseLayoutRounding = true;

        var owner = Application.Current?.MainWindow;
        if (owner != null && !ReferenceEquals(owner, this))
            Owner = owner;

        WpfTranslationPatch.MarkUntranslatable(this);
        SourceInitialized += (_, _) => DarkTheme.EnableDarkTitleBar(this);

        _status = new TextBlock
        {
            Text = T("VOCALOIDPatcher_Export_ProgressReading", "Reading project…"),
            Foreground = DarkTheme.Foreground,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
            Margin = new Thickness(0, 0, 0, 14)
        };

        var bar = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 6,
            Width = 360,
            Foreground = DarkTheme.Foreground,
            Background = DarkTheme.FieldBack,
            BorderThickness = new Thickness(0)
        };

        var root = new StackPanel { Margin = new Thickness(26, 24, 26, 24), MinWidth = 360 };
        root.Children.Add(_status);
        root.Children.Add(bar);
        Content = root;
    }

    private void Report(ExportProgress p)
    {
        _status.Text = p.Phase switch
        {
            ExportPhase.Reading => T("VOCALOIDPatcher_Export_ProgressReading", "Reading project…"),
            ExportPhase.Pitch => string.Format(
                T("VOCALOIDPatcher_Export_ProgressPitch", "Processing pitch ({0}/{1})…"), p.Current, p.Total),
            ExportPhase.Generating => string.Format(
                T("VOCALOIDPatcher_Export_ProgressGenerating", "Generating {0}…"), p.Arg ?? ""),
            ExportPhase.Writing => T("VOCALOIDPatcher_Export_ProgressWriting", "Writing file…"),
            _ => _status.Text
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_finished)
            e.Cancel = true;
        base.OnClosing(e);
    }

    internal static T Run<T>(Func<IProgress<ExportProgress>, T> work)
    {
        var dialog = new ExportProgressDialog();
        T result = default!;
        ExceptionDispatchInfo? captured = null;
        var progress = new Progress<ExportProgress>(dialog.Report);

        dialog.Loaded += async (_, _) =>
        {
            try
            {
                result = await Task.Run(() => work(progress));
            }
            catch (Exception ex)
            {
                captured = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                dialog._finished = true;
                dialog.Close();
            }
        };

        dialog.ShowDialog();
        captured?.Throw();
        return result;
    }

    private static string T(string key, string fallback) => TranslationManager.Get(key) ?? fallback;
}
