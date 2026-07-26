using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID.MusicalEditor;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

public class RendererPreviewThrottlePatch : PatchBase
{
    public override string PatchName        => "RendererPreviewThrottlePatch";
    public override Type   TargetClass      => typeof(PianorollView);
    public override string TargetMethodName => "OnRendererBlockRendered";

#if NET6_0
    public override Type[] ArgumentTypes => new[] { typeof(RendererObserverBlockRenderingEventArgs) };
#else
    public override Type[] ArgumentTypes => new[] { typeof(object), typeof(RendererObserverBlockRenderingEventArgs) };
#endif

    private const int FramesPerSecond = 30;
    private static readonly long IntervalTicks = Math.Max(Stopwatch.Frequency / FramesPerSecond, 1L);

    private sealed class State
    {
        public long LastUpdate;
    }

    private static readonly ConditionalWeakTable<PianorollView, State> States = new();

    [HarmonyPrefix]
    private static bool Prefix(PianorollView __instance)
    {
        if (!Settings.ThrottleRendererPreview)
            return true;

        var state = States.GetOrCreateValue(__instance);
        long now = Stopwatch.GetTimestamp();
        if (now - state.LastUpdate < IntervalTicks)
            return false;

        state.LastUpdate = now;
        return true;
    }
}
