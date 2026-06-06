using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
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

public class AudioThumbFromCachePatch : PatchBase
{
    public override string PatchName        => "AudioThumbFromCachePatch";
    public override Type   TargetClass      => typeof(AugmentedAudioBuffer);
    public override string TargetMethodName => "ThumbWithRange";

    public override Type[] ArgumentTypes => new[] { typeof(long), typeof(long) };

    private const int ThumbUnit = 256;

    private static readonly FieldInfo? ThumbsMinField =
        AccessTools.Field(typeof(AugmentedAudioBuffer), "thumbsMin");

    private static readonly FieldInfo? ThumbsMaxField =
        AccessTools.Field(typeof(AugmentedAudioBuffer), "thumbsMax");

    [HarmonyPrefix]
    private static bool Prefix(AugmentedAudioBuffer __instance, long beginSample, long endSample, ref IList<VSMAudioThumb?> __result)
    {
        if (!Settings.FreeAudioPcmCache || ThumbsMinField == null || ThumbsMaxField == null)
            return true;

        if (ThumbsMinField.GetValue(__instance) is not List<List<short>> mins ||
            ThumbsMaxField.GetValue(__instance) is not List<List<short>> maxs)
            return true;

        long total = __instance.NumSamples;
        long begin = Math.Clamp(beginSample, 0L, total);
        long end = Math.Clamp(endSample, begin, total);
        int channels = __instance.ChannelCount;

        var list = new List<VSMAudioThumb?>(channels);
        for (int i = 0; i < channels; i++)
        {
            if (i >= mins.Count || i >= maxs.Count)
            {
                list.Add(null);
                continue;
            }

            var minList = mins[i];
            var maxList = maxs[i];
            int blocks = minList.Count;

            int first = (int)(begin / ThumbUnit);
            int last = (int)((end + ThumbUnit - 1) / ThumbUnit);
            first = Math.Clamp(first, 0, blocks);
            last = Math.Clamp(last, first, blocks);
            if (first == last && first < blocks && begin < end)
                last = first + 1;

            if (first >= last)
            {
                list.Add(null);
                continue;
            }

            var spanMin = CollectionsMarshal.AsSpan(minList).Slice(first, last - first);
            var spanMax = CollectionsMarshal.AsSpan(maxList).Slice(first, last - first);

            short mn = short.MaxValue;
            short mx = short.MinValue;
            for (int k = 0; k < spanMin.Length; k++)
                if (spanMin[k] < mn) mn = spanMin[k];
            for (int k = 0; k < spanMax.Length; k++)
                if (spanMax[k] > mx) mx = spanMax[k];

            list.Add(new VSMAudioThumb { Min = mn, Max = mx });
        }

        __result = list;
        return false;
    }
}
#endif
