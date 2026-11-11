using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

public class TrackMuteRefreshPatch : PatchBase
{
    public override string PatchName     => "TrackMuteRefreshPatch";
    public override Type   TargetClass   => typeof(WIVSMTrack);
    public override string TargetMethodName => "ToggleMute";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Settings.ShowOtherTracksNotes || !Settings.ShowOtherTracksSkipMuted)
            return;

        ShowOtherTracksNotesPatch.RequestRefreshPianoroll();
    }
}
