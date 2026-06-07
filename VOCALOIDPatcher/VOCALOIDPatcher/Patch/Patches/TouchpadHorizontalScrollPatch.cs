using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using HarmonyLib;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;

namespace VOCALOIDPatcher.Patch.Patches;

public class TouchpadHorizontalScrollPatch : PatchBase
{
    public override string PatchName        => "TouchpadHorizontalScrollPatch";
    public override Type   TargetClass      => typeof(ZoomScrollViewer);
    public override string TargetMethodName => "OnApplyTemplate";

    private const int WmMouseHWheel = 0x020E;

    private sealed class HookEntry
    {
        public HwndSource? Source;
        public HwndSourceHook? Hook;
    }

    private static readonly ConditionalWeakTable<ZoomScrollViewer, HookEntry> Entries = new();

    [HarmonyPostfix]
    private static void Postfix(ZoomScrollViewer __instance)
    {
        __instance.Loaded   -= OnLoaded;
        __instance.Loaded   += OnLoaded;
        __instance.Unloaded -= OnUnloaded;
        __instance.Unloaded += OnUnloaded;

        if (__instance.IsLoaded)
            OnLoaded(__instance, new RoutedEventArgs());
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ZoomScrollViewer zsv) return;
        if (PresentationSource.FromVisual(zsv) is not HwndSource source) return;

        var entry = Entries.GetOrCreateValue(zsv);
        if (ReferenceEquals(entry.Source, source)) return;

        if (entry.Source != null && entry.Hook != null)
            entry.Source.RemoveHook(entry.Hook);

        HwndSourceHook hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            WndProc(zsv, msg, wParam, ref handled);

        entry.Source = source;
        entry.Hook = hook;
        source.AddHook(hook);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ZoomScrollViewer zsv) return;
        if (!Entries.TryGetValue(zsv, out var entry)) return;

        if (entry.Source != null && entry.Hook != null)
            entry.Source.RemoveHook(entry.Hook);

        entry.Source = null;
        entry.Hook = null;
    }

    private static IntPtr WndProc(ZoomScrollViewer zsv, int msg, IntPtr wParam, ref bool handled)
    {
        if (handled || msg != WmMouseHWheel) return IntPtr.Zero;
        if (!zsv.IsMouseOver) return IntPtr.Zero;

        int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
        if (delta == 0) return IntPtr.Zero;

        int sign = UserSettings.Instance.ReverseHorizontalScrollDirectionWithMouseWheel ? -1 : 1;
        zsv.ScrollToHorizontalOffset(zsv.HorizontalOffset + delta * sign);
        handled = true;

        AudioPlayer? audioPlayer = App.AudioPlayer;
        if (audioPlayer != null && audioPlayer.IsPlaying)
            zsv.ShouldDisableAutoScroll = true;

        return IntPtr.Zero;
    }
}
