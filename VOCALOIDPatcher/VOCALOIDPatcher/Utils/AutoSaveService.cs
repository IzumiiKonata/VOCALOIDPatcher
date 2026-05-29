using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using VOCALOIDPatcher.Config;
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
            Debug.Print($"定时保存已启用: 每 {minutes} 分钟");
        }
        else
        {
            _timer.Stop();
            Debug.Print("定时保存已关闭");
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

            Debug.Print(document.Save(dir, name) ? $"定时保存成功: {path}" : $"定时保存失败: {path}");
        }
        catch (Exception e)
        {
            Debug.Print($"定时保存异常: {e.Message}");
        }
    }
}
