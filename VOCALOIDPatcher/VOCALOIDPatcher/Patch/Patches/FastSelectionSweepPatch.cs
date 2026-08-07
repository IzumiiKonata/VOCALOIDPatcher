using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

internal static class SelectionSweepState
{
    public static bool TimeSigsDirty = true;
    public static bool TemposDirty = true;
    public static bool BreakPointsDirty = true;

    public static void MarkAllDirty()
    {
        TimeSigsDirty = true;
        TemposDirty = true;
        BreakPointsDirty = true;
    }
}

public class TimeSigSelectionTrackPatch : PatchBase
{
    public override string PatchName        => "TimeSigSelectionTrackPatch";
    public override Type   TargetClass      => typeof(WIVSMTimeSig);
    public override string TargetMethodName => "set_IsSelected";

    public override Type[] ArgumentTypes => new[] { typeof(bool) };

    [HarmonyPostfix]
    private static void Postfix(bool value)
    {
        if (value)
            SelectionSweepState.TimeSigsDirty = true;
    }
}

public class TempoSelectionTrackPatch : PatchBase
{
    public override string PatchName        => "TempoSelectionTrackPatch";
    public override Type   TargetClass      => typeof(WIVSMTempo);
    public override string TargetMethodName => "set_IsSelected";

    public override Type[] ArgumentTypes => new[] { typeof(bool) };

    [HarmonyPostfix]
    private static void Postfix(bool value)
    {
        if (value)
            SelectionSweepState.TemposDirty = true;
    }
}

public class BreakPointSelectionTrackPatch : PatchBase
{
    public override string PatchName        => "BreakPointSelectionTrackPatch";
    public override Type   TargetClass      => typeof(WIVSMBreakPoint);
    public override string TargetMethodName => "set_IsSelected";

    public override Type[] ArgumentTypes => new[] { typeof(bool) };

    [HarmonyPostfix]
    private static void Postfix(bool value)
    {
        if (value)
            SelectionSweepState.BreakPointsDirty = true;
    }
}

public class FastSelectionSweepPatch : PatchBase
{
    public override string PatchName        => "FastSelectionSweepPatch";
    public override Type   TargetClass      => typeof(Sequence);
    public override string TargetMethodName => "SelectAllTrackEditorEvents";

    public override Type[] ArgumentTypes => new[] { typeof(bool) };

    [HarmonyPrefix]
    private static bool Prefix(Sequence __instance, bool selected)
    {
        if (!Settings.FastSelectionSweep || selected)
            return true;

        try
        {
            if (SelectionSweepState.TimeSigsDirty)
            {
                __instance.SelectAllTimeSigs(false);
                SelectionSweepState.TimeSigsDirty = false;
            }

            if (SelectionSweepState.TemposDirty)
            {
                __instance.SelectAllTempos(false);
                SelectionSweepState.TemposDirty = false;
            }

            bool breakPointsDirty = SelectionSweepState.BreakPointsDirty;
            if (breakPointsDirty)
                __instance.SelectAllMasterVolumes(false);

            __instance.SelectAllParts(false);

            var activePart = __instance.ActivePart;
            if (activePart != null && !activePart.IsSelected)
                __instance.ActivePart = null;

            if (breakPointsDirty)
            {
                __instance.SelectAllTrackVolumes(false);
                __instance.SelectAllTrackPanpots(false);
                SelectionSweepState.BreakPointsDirty = false;
            }

            return false;
        }
        catch (Exception e)
        {
            Debug.Print(TranslationManager.Tr("VOCALOIDPatcher_Debug_FastSelectionSweep_Failed", e.Message));
            return true;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(bool selected)
    {
        if (selected)
            SelectionSweepState.MarkAllDirty();
    }
}
