using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.TrackEditor;
using Yamaha.VOCALOID.VSM;
using UpdateViewTypeFlag = Yamaha.VOCALOID.TrackEditor.UpdateViewTypeFlag;

namespace VOCALOIDPatcher.Patch.Patches;

internal static class TrackSelectionRefresh
{
    internal sealed class State
    {
        public readonly Dictionary<nint, bool> Selected = new();
    }

    internal static readonly ConditionalWeakTable<TrackControlBase, State> States = new();

    internal static bool IsSelectionUpdate(UpdateViewTypeFlag type) =>
        type == UpdateViewTypeFlag.ActivePartChanged || type == UpdateViewTypeFlag.PartSelectionChanged;
}

public class MidiTrackSelectionRefreshPatch : PatchBase
{
    public override string PatchName        => "MidiTrackSelectionRefreshPatch";
    public override Type   TargetClass      => typeof(MidiTrackControl);
    public override string TargetMethodName => "UpdateView";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(object),
        typeof(UpdateViewTypeFlag),
        typeof(UpdateObserverNotifyEventArgs),
        typeof(object)
    };

    [HarmonyPrefix]
    private static bool Prefix(MidiTrackControl __instance, UpdateViewTypeFlag typeFlags)
    {
        if (!Settings.OptimizeTrackRendering || !TrackSelectionRefresh.IsSelectionUpdate(typeFlags))
            return true;

        var track = __instance.Track;
        if (track == null)
            return true;

        var state = TrackSelectionRefresh.States.GetOrCreateValue(__instance);
        var live = new HashSet<nint>();

        foreach (var part in track.Parts)
        {
            if (part is not WIVSMMidiPart midiPart)
                continue;

            nint key = (nint)midiPart;
            live.Add(key);
            bool selected = midiPart.IsSelected;
            if (!state.Selected.TryGetValue(key, out bool old) || old != selected)
                __instance.RedrawSelectChangedColorChangedPart(midiPart);
            state.Selected[key] = selected;
        }

        RemoveMissing(state.Selected, live);
        return false;
    }

    internal static void RemoveMissing(Dictionary<nint, bool> selected, HashSet<nint> live)
    {
        var removed = new List<nint>();
        foreach (var key in selected.Keys)
            if (!live.Contains(key))
                removed.Add(key);
        foreach (var key in removed)
            selected.Remove(key);
    }
}

public class AudioTrackSelectionRefreshPatch : PatchBase
{
    public override string PatchName        => "AudioTrackSelectionRefreshPatch";
    public override Type   TargetClass      => typeof(AudioTrackControl);
    public override string TargetMethodName => "UpdateView";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(object),
        typeof(UpdateViewTypeFlag),
        typeof(UpdateObserverNotifyEventArgs),
        typeof(object)
    };

    [HarmonyPrefix]
    private static bool Prefix(AudioTrackControl __instance, UpdateViewTypeFlag typeFlags)
    {
        if (!Settings.OptimizeTrackRendering || !TrackSelectionRefresh.IsSelectionUpdate(typeFlags))
            return true;

        var track = __instance.Track;
        if (track == null)
            return true;

        var state = TrackSelectionRefresh.States.GetOrCreateValue(__instance);
        var live = new HashSet<nint>();

        foreach (var part in track.Parts)
        {
            if (part is not WIVSMAudioPart audioPart)
                continue;

            nint key = (nint)audioPart;
            live.Add(key);
            bool selected = audioPart.IsSelected;
            if (!state.Selected.TryGetValue(key, out bool old) || old != selected)
                __instance.RedrawSelectChangedColorChangedPart(audioPart);
            state.Selected[key] = selected;
        }

        MidiTrackSelectionRefreshPatch.RemoveMissing(state.Selected, live);
        return false;
    }
}
