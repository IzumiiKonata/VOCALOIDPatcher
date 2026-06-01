using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

public class TrackMuteRefreshPatch : PatchBase
{
    public override string  PatchName        => "TrackMuteRefreshPatch";
    public override Type    TargetClass      => typeof(WIVSMTrack);
    public override Type[]? ArgumentTypes    => new[] { typeof(bool) };

    public override string TargetMethodName =>
        AccessTools.Method(typeof(WIVSMTrack), "SetMute", new[] { typeof(bool) }) != null
            ? "SetMute"
            : "set_IsMute";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Settings.ShowOtherTracksNotes || !Settings.ShowOtherTracksSkipMuted)
            return;

        ShowOtherTracksNotesPatch.RequestRefreshPianoroll();
    }
}
