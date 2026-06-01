using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

public class TrackSoloRefreshPatch : PatchBase
{
    public override string  PatchName        => "TrackSoloRefreshPatch";
    public override Type    TargetClass      => typeof(WIVSMTrack);
    public override Type[]? ArgumentTypes    => new[] { typeof(bool) };

    public override string TargetMethodName =>
        AccessTools.Method(typeof(WIVSMTrack), "SetSolo", new[] { typeof(bool) }) != null
            ? "SetSolo"
            : "set_IsSolo";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Settings.ShowOtherTracksNotes || !Settings.ShowOtherTracksSkipMuted)
            return;

        ShowOtherTracksNotesPatch.RequestRefreshPianoroll();
    }
}
