using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using HarmonyLib;
using Yamaha.VOCALOID;
using MusicalZoomScrollViewer = Yamaha.VOCALOID.MusicalEditor.ZoomScrollViewer;
using TrackZoomScrollViewer = Yamaha.VOCALOID.TrackEditor.ZoomScrollViewer;

namespace VOCALOIDPatcher.Patch.Patches;

internal static class TouchpadHorizontalScroll
{
    private const int WmMouseHWheel = 0x020E;

    private sealed class HookEntry
    {
        public HwndSource? Source;
        public HwndSourceHook? Hook;
        public Action<bool>? DisableAutoScroll;
    }

    private static readonly ConditionalWeakTable<ScrollViewer, HookEntry> Entries = new();

    public static void Attach(ScrollViewer sv, Action<bool> disableAutoScroll)
    {
        Entries.GetOrCreateValue(sv).DisableAutoScroll = disableAutoScroll;

        sv.Loaded   -= OnLoaded;
        sv.Loaded   += OnLoaded;
        sv.Unloaded -= OnUnloaded;
        sv.Unloaded += OnUnloaded;

        if (sv.IsLoaded)
            OnLoaded(sv, new RoutedEventArgs());
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        if (PresentationSource.FromVisual(sv) is not HwndSource source) return;

        var entry = Entries.GetOrCreateValue(sv);
        if (ReferenceEquals(entry.Source, source)) return;

        if (entry.Source != null && entry.Hook != null)
            entry.Source.RemoveHook(entry.Hook);

        HwndSourceHook hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            WndProc(sv, entry, msg, wParam, ref handled);

        entry.Source = source;
        entry.Hook = hook;
        source.AddHook(hook);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        if (!Entries.TryGetValue(sv, out var entry)) return;

        if (entry.Source != null && entry.Hook != null)
            entry.Source.RemoveHook(entry.Hook);

        entry.Source = null;
        entry.Hook = null;
    }

    private static IntPtr WndProc(ScrollViewer sv, HookEntry entry, int msg, IntPtr wParam, ref bool handled)
    {
        if (handled || msg != WmMouseHWheel) return IntPtr.Zero;
        if (!sv.IsMouseOver) return IntPtr.Zero;

        int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
        if (delta == 0) return IntPtr.Zero;

        int sign = UserSettings.Instance.ReverseHorizontalScrollDirectionWithMouseWheel ? -1 : 1;
        sv.ScrollToHorizontalOffset(sv.HorizontalOffset + delta * sign);
        handled = true;

        AudioPlayer? audioPlayer = App.AudioPlayer;
        if (audioPlayer != null && audioPlayer.IsPlaying)
            entry.DisableAutoScroll?.Invoke(true);

        return IntPtr.Zero;
    }
}

public class TouchpadHorizontalScrollPatch : PatchBase
{
    public override string PatchName        => "TouchpadHorizontalScrollPatch";
    public override Type   TargetClass      => typeof(MusicalZoomScrollViewer);
    public override string TargetMethodName => "OnApplyTemplate";

    [HarmonyPostfix]
    private static void Postfix(MusicalZoomScrollViewer __instance)
        => TouchpadHorizontalScroll.Attach(__instance, v => __instance.ShouldDisableAutoScroll = v);
}

public class TrackTouchpadHorizontalScrollPatch : PatchBase
{
    public override string PatchName        => "TrackTouchpadHorizontalScrollPatch";
    public override Type   TargetClass      => typeof(TrackZoomScrollViewer);
    public override string TargetMethodName => "OnApplyTemplate";

    [HarmonyPostfix]
    private static void Postfix(TrackZoomScrollViewer __instance)
        => TouchpadHorizontalScroll.Attach(__instance, v => __instance.ShouldDisableAutoScroll = v);
}
