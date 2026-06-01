using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

// 任意轨道独奏状态改变时刷新钢琴窗。独奏会让其它非独奏轨道被有效静音 (AudioPlayer.IsMute),
// 因此开/关独奏后, "显示其他轨道的音符" 在勾选 "跳过静音轨道" 时需要重新求值。
// 与静音对称: net8 用 WIVSMTrack.SetSolo(bool) 方法, net6 用 IsSolo 属性 setter (set_IsSolo)。
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
