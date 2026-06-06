using System;
using System.Collections.Concurrent;
using System.Windows.Media;
using HarmonyLib;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

internal static class TrackBrushCache
{
    private static readonly ConcurrentDictionary<uint, SolidColorBrush> Cache = new();

    public static SolidColorBrush Get(Color color)
    {
        var key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

        if (Cache.TryGetValue(key, out var brush))
            return brush;

        brush = new SolidColorBrush(color);
        brush.Freeze();
        Cache[key] = brush;
        return brush;
    }
}

public class TrackSelectedFillBrushPatch : PatchBase
{
    public override string PatchName        => "TrackSelectedFillBrushPatch";
    public override Type   TargetClass      => typeof(WIVSMTrackExtension);
    public override string TargetMethodName => "GetSelectedFillBrush";
    public override Type[] ArgumentTypes    => new[] { typeof(WIVSMTrack) };

    [HarmonyPrefix]
    private static bool Prefix(WIVSMTrack track, ref Brush __result)
    {
        __result = TrackBrushCache.Get(WIVSMTrackExtension.GetSelectedFillColor(track));
        return false;
    }
}

public class TrackSelectedStrokeBrushPatch : PatchBase
{
    public override string PatchName        => "TrackSelectedStrokeBrushPatch";
    public override Type   TargetClass      => typeof(WIVSMTrackExtension);
    public override string TargetMethodName => "GetSelectedStrokeBrush";
    public override Type[] ArgumentTypes    => new[] { typeof(WIVSMTrack) };

    [HarmonyPrefix]
    private static bool Prefix(WIVSMTrack track, ref Brush __result)
    {
        __result = TrackBrushCache.Get(WIVSMTrackExtension.GetSelectedStrokeColor(track));
        return false;
    }
}

public class TrackUnselectedFillBrushPatch : PatchBase
{
    public override string PatchName        => "TrackUnselectedFillBrushPatch";
    public override Type   TargetClass      => typeof(WIVSMTrackExtension);
    public override string TargetMethodName => "GetUnselectedFillBrush";
    public override Type[] ArgumentTypes    => new[] { typeof(WIVSMTrack) };

    [HarmonyPrefix]
    private static bool Prefix(WIVSMTrack track, ref Brush __result)
    {
        __result = TrackBrushCache.Get(WIVSMTrackExtension.GetUnselectedFillColor(track));
        return false;
    }
}

public class TrackUnselectedStrokeBrushPatch : PatchBase
{
    public override string PatchName        => "TrackUnselectedStrokeBrushPatch";
    public override Type   TargetClass      => typeof(WIVSMTrackExtension);
    public override string TargetMethodName => "GetUnselectedStrokeBrush";
    public override Type[] ArgumentTypes    => new[] { typeof(WIVSMTrack) };

    [HarmonyPrefix]
    private static bool Prefix(WIVSMTrack track, ref Brush __result)
    {
        __result = TrackBrushCache.Get(WIVSMTrackExtension.GetUnselectedStrokeColor(track));
        return false;
    }
}

public class TrackOverlayFillBrushPatch : PatchBase
{
    public override string PatchName        => "TrackOverlayFillBrushPatch";
    public override Type   TargetClass      => typeof(WIVSMTrackExtension);
    public override string TargetMethodName => "GetOverlayFillBrush";
    public override Type[] ArgumentTypes    => new[] { typeof(WIVSMTrack) };

    [HarmonyPrefix]
    private static bool Prefix(WIVSMTrack track, ref Brush __result)
    {
        __result = TrackBrushCache.Get(WIVSMTrackExtension.GetOverlayFillColor(track));
        return false;
    }
}
