using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using VOCALOIDPatcher.Config;

namespace VOCALOIDPatcher.Utils;

public static class WorkingSetTrimmer
{
    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    private static DispatcherTimer? _trimTimer;
    private static string _pendingReason = "";
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;

        if (Application.Current?.MainWindow is not { } window)
            return;

        _installed = true;
        window.StateChanged += (_, _) =>
        {
            if (window.WindowState == WindowState.Minimized)
                Trim("窗口最小化");
        };
    }

    public static void ScheduleTrim(string reason)
    {
        if (!Settings.TrimWorkingSet)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        dispatcher.BeginInvoke(new Action(() =>
        {
            _pendingReason = reason;
            _trimTimer ??= CreateTrimTimer();
            _trimTimer.Stop();
            _trimTimer.Start();
        }), DispatcherPriority.Background);
    }

    private static DispatcherTimer CreateTrimTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Trim(_pendingReason);
        };
        return timer;
    }

    public static void Trim(string reason)
    {
        if (!Settings.TrimWorkingSet)
            return;

        try
        {
            if (EmptyWorkingSet(GetCurrentProcess()))
                Debug.Print($"已修剪工作集: {reason}");
        }
        catch (Exception e)
        {
            Debug.Print($"修剪工作集失败: {e.Message}");
        }
    }
}
