using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Utils;

public static class AutoSaveService
{
    private static DispatcherTimer? _timer;

    public static void UpdateFromSettings()
    {
        var dispatcher = Application.Current?.Dispatcher;
        dispatcher?.Invoke(Configure);
    }

    private static void Configure()
    {
        _timer ??= CreateTimer();

        if (Settings.AutoSaveEnabled)
        {
            var minutes = Math.Max(1, Settings.AutoSaveIntervalMinutes);
            _timer.Interval = TimeSpan.FromMinutes(minutes);
            _timer.Start();
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_AutoSave_Enabled", minutes));
        }
        else
        {
            _timer.Stop();
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_AutoSave_Disabled"));
        }
    }

    private static DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
        timer.Tick += (_, _) => TrySave();
        return timer;
    }

    private static void TrySave()
    {
        try
        {
            var document = App.Shared?.Document;
            var sequence = document?.Sequence;
            if (document == null || sequence == null)
                return;

            if (!sequence.Overwritable)
                return;

            var savingProp = sequence.GetType().GetProperty("IsSavingBackupFile");
            if (savingProp?.GetValue(sequence) is true)
                return;

            var path = document.DocumentUri?.LocalPath;
            if (string.IsNullOrEmpty(path)
                || !string.Equals(Path.GetExtension(path), ".vpr", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(path))
                return;

            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name))
                return;

            Debug.Print(document.Save(dir, name)
                ? TranslationManager.Tr("VOCALOIDPatcher_Debug_AutoSave_Succeeded", path)
                : TranslationManager.Tr("VOCALOIDPatcher_Debug_AutoSave_Failed", path));
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_AutoSave_Exception", e.Message));
        }
    }
}
