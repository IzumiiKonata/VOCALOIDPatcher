using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

public class TrackSoloRefreshPatch : PatchBase
{
    public override string PatchName       => "TrackSoloRefreshPatch";
    public override Type   TargetClass     => typeof(WIVSMTrack);
    public override string TargetMethodName => "ToggleSolo";

    [HarmonyPostfix]
    private static void Postfix()
    {
        Refresh();
    }

    internal static void Refresh()
    {
        if (!Settings.ShowOtherTracksNotes || !Settings.ShowOtherTracksSkipMuted)
            return;

        ShowOtherTracksNotesPatch.RequestRefreshPianoroll();
    }
}

public class TrackSoloDefeatRefreshPatch : PatchBase
{
    public override string PatchName       => "TrackSoloDefeatRefreshPatch";
    public override Type   TargetClass     => typeof(WIVSMTrack);
    public override string TargetMethodName => "ToggleSoloDefeat";

    [HarmonyPostfix]
    private static void Postfix()
    {
        TrackSoloRefreshPatch.Refresh();
    }
}

public class TrackExclusiveSoloRefreshPatch : PatchBase
{
    public override string PatchName       => "TrackExclusiveSoloRefreshPatch";
    public override Type   TargetClass     => typeof(WIVSMTrack);
    public override string TargetMethodName => "SetExclusiveSolo";

    [HarmonyPostfix]
    private static void Postfix()
    {
        TrackSoloRefreshPatch.Refresh();
    }
}
