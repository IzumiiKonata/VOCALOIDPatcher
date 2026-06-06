using System;
using System.Reflection;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
public class AudioPcmReleasePatch : PatchBase
{
    public override string PatchName        => "AudioPcmReleasePatch";
    public override Type   TargetClass      => typeof(AugmentedAudioBuffer);
    public override string TargetMethodName => "Load";

    public override Type[] ArgumentTypes => new[] { typeof(string) };

    private static readonly FieldInfo? WaveFileField =
        AccessTools.Field(typeof(AugmentedAudioBuffer), "waveFile");

    [HarmonyPostfix]
    private static void Postfix(object __instance, bool __result)
    {
        if (!__result || !Settings.FreeAudioPcmCache || WaveFileField == null)
            return;

        if (WaveFileField.GetValue(__instance) is WaveFile waveFile)
            waveFile.WaveData.Clear();
    }
}

public class WaveThumbGuardPatch : PatchBase
{
    public override string PatchName        => "WaveThumbGuardPatch";
    public override Type   TargetClass      => typeof(WaveFile);
    public override string TargetMethodName => "Thumb";

    public override Type[] ArgumentTypes => new[] { typeof(long), typeof(long), typeof(int) };

    [HarmonyPrefix]
    private static bool Prefix(WaveFile __instance, int channel, ref VSMAudioThumb? __result)
    {
        if (!Settings.FreeAudioPcmCache)
            return true;

        var data = __instance.WaveData;
        if (channel < data.Count && data[channel] is { Length: > 0 })
            return true;

        __result = null;
        return false;
    }
}
#endif
