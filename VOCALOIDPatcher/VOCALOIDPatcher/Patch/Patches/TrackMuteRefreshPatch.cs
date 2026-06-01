using System;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

// 任意轨道静音状态改变时刷新钢琴窗, 使 "显示其他轨道的音符" 在勾选 "跳过静音轨道" 时即时更新:
// 静音轨道的音符立即隐藏, 取消静音后立即重新显示。
// 所有静音路径 (ToggleMute / SetGlobalMute) 最终都汇聚到这个写入点。
// 两个 TFM 的 DLL 不一致: net8 用 WIVSMTrack.SetMute(bool) 方法, net6 用 IsMute 属性 setter (set_IsMute),
// 两者均为单 bool 参数, 运行时选择当前 DLL 中存在的那个。
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
        // 仅在 "显示其他轨道音符" + "跳过静音轨道" 都启用时, 静音状态才会影响显示
        if (!Settings.ShowOtherTracksNotes || !Settings.ShowOtherTracksSkipMuted)
            return;

        ShowOtherTracksNotesPatch.RequestRefreshPianoroll();
    }
}
