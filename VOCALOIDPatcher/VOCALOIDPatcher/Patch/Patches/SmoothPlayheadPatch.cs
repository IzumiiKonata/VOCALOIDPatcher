using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VAE;

namespace VOCALOIDPatcher.Patch.Patches;

public static class SmoothPlayhead
{
    private static readonly FieldInfo? FEvent =
        AccessTools.Field(typeof(AudioPlayer), "SongPositionProceeded");

    private static readonly MethodInfo? MTimerGetter =
        AccessTools.PropertyGetter(typeof(AudioPlayer), "songPositionUpdateTimer");

    private static readonly bool Resolved = FEvent != null && MTimerGetter != null;

    private static EventHandler? _handler;
    private static double _lastTime = double.NaN;

    internal static void Begin(AudioPlayer player)
    {
        if (!Settings.SmoothPlayhead || !Resolved) return;
        if (player.ConnectMode != VEConnectMode.StandAlone) return;

        Detach();

        if (MTimerGetter!.Invoke(player, null) is DispatcherTimer timer)
            timer.Stop();
        else
            return;

        _lastTime = double.NaN;
        _handler = (_, _) => OnRendering(player);
        CompositionTarget.Rendering += _handler;
    }

    internal static void End()
    {
        Detach();
    }

    private static void Detach()
    {
        if (_handler == null) return;
        CompositionTarget.Rendering -= _handler;
        _handler = null;
    }

    private static void OnRendering(AudioPlayer player)
    {
        try
        {
            if (player.MixdownMode != MixdownMode.NotMixdownMode ||
                player.ConnectMode != VEConnectMode.StandAlone ||
                !player.IsPlaying)
                return;

            var sequence = ((Application.Current?.MainWindow as MainWindow)?.DataContext as MainViewModel)?.VSMSequence;
            if (sequence == null) return;

            var time = VEAudioEngine.GetCurrentTime();
            if (time == _lastTime) return;
            _lastTime = time;

            var tick = sequence.GetTickFromTime(time);
            var handler = FEvent!.GetValue(player) as EventHandler<SongPositionProceedEventArgs>;
            handler?.Invoke(player, new SongPositionProceedEventArgs(tick));
        }
        catch (Exception e)
        {
            Detach();
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_SmoothPlayhead_Failed", e.Message));
        }
    }
}

public class SmoothPlayheadBeginPatch : PatchBase
{
    public override string PatchName        => "SmoothPlayheadBeginPatch";
    public override Type   TargetClass      => typeof(AudioPlayer);
    public override string TargetMethodName => "BeginAudioPlayObserving";

    [HarmonyPostfix]
    private static void Postfix(AudioPlayer __instance)
    {
        SmoothPlayhead.Begin(__instance);
    }
}

public class SmoothPlayheadEndPatch : PatchBase
{
    public override string PatchName        => "SmoothPlayheadEndPatch";
    public override Type   TargetClass      => typeof(AudioPlayer);
    public override string TargetMethodName => "EndAudioPlayObserving";

    [HarmonyPostfix]
    private static void Postfix()
    {
        SmoothPlayhead.End();
    }
}
