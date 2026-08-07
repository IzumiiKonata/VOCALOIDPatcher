using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

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

    private static readonly AccessTools.FieldRef<AugmentedAudioBuffer, List<List<short>>>? ThumbsMinRef =
        CreateFieldRef("thumbsMin");

    private static readonly AccessTools.FieldRef<AugmentedAudioBuffer, List<List<short>>>? ThumbsMaxRef =
        CreateFieldRef("thumbsMax");

    private static AccessTools.FieldRef<AugmentedAudioBuffer, List<List<short>>>? CreateFieldRef(string name)
    {
        try
        {
            return AccessTools.FieldRefAccess<AugmentedAudioBuffer, List<List<short>>>(name);
        }
        catch
        {
            return null;
        }
    }

    [HarmonyPrefix]
    private static bool Prefix(AugmentedAudioBuffer __instance, long beginSample, long endSample, ref IList<VSMAudioThumb?> __result)
    {
        if (!Settings.FreeAudioPcmCache || ThumbsMinRef == null || ThumbsMaxRef == null)
            return true;

        var mins = ThumbsMinRef(__instance);
        var maxs = ThumbsMaxRef(__instance);
        if (mins == null || maxs == null)
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

            list.Add(new VSMAudioThumb { Min = SimdMin(spanMin), Max = SimdMax(spanMax) });
        }

        __result = list;
        return false;
    }

    private static short SimdMin(ReadOnlySpan<short> span)
    {
        short result = short.MaxValue;
        int i = 0;

        if (System.Numerics.Vector.IsHardwareAccelerated && span.Length >= System.Numerics.Vector<short>.Count)
        {
            var acc = new System.Numerics.Vector<short>(short.MaxValue);
            for (; i <= span.Length - System.Numerics.Vector<short>.Count; i += System.Numerics.Vector<short>.Count)
                acc = System.Numerics.Vector.Min(acc, new System.Numerics.Vector<short>(span.Slice(i, System.Numerics.Vector<short>.Count)));

            for (int k = 0; k < System.Numerics.Vector<short>.Count; k++)
                if (acc[k] < result)
                    result = acc[k];
        }

        for (; i < span.Length; i++)
            if (span[i] < result)
                result = span[i];

        return result;
    }

    private static short SimdMax(ReadOnlySpan<short> span)
    {
        short result = short.MinValue;
        int i = 0;

        if (System.Numerics.Vector.IsHardwareAccelerated && span.Length >= System.Numerics.Vector<short>.Count)
        {
            var acc = new System.Numerics.Vector<short>(short.MinValue);
            for (; i <= span.Length - System.Numerics.Vector<short>.Count; i += System.Numerics.Vector<short>.Count)
                acc = System.Numerics.Vector.Max(acc, new System.Numerics.Vector<short>(span.Slice(i, System.Numerics.Vector<short>.Count)));

            for (int k = 0; k < System.Numerics.Vector<short>.Count; k++)
                if (acc[k] > result)
                    result = acc[k];
        }

        for (; i < span.Length; i++)
            if (span[i] > result)
                result = span[i];

        return result;
    }
}
