using System;
using System.Collections.Generic;
using HarmonyLib;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.VSM;

namespace VOCALOIDPatcher.Patch.Patches;

#if !NET6_0
public class AudioBufferReleaseSafetyPatch : PatchBase
{
    public override string PatchName        => "AudioBufferReleaseSafetyPatch";
    public override Type   TargetClass      => typeof(AudioPlayer);
    public override string TargetMethodName => "NTNeedReleaseAudioBuffer";
    public override Type[] ArgumentTypes    => new[] { typeof(nint), typeof(nint) };

    private static readonly AccessTools.FieldRef<AudioPlayer, Dictionary<nint, HashSet<WIVSMAudioBuffer>>>?
        Buffers = CreateBuffersRef();

    private static readonly AccessTools.FieldRef<AudioPlayer, object>?
        SyncRoot = CreateSyncRootRef();

    [HarmonyPrefix]
    private static bool Prefix(AudioPlayer __instance, nint audioBufferHandle)
    {
        if (Buffers == null || SyncRoot == null)
            return true;

        WIVSMAudioBuffer? buffer = null;
        lock (SyncRoot(__instance))
        {
            var buffers = Buffers(__instance);
            if (buffers.TryGetValue(audioBufferHandle, out var candidates))
            {
                using var enumerator = candidates.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    buffer = enumerator.Current;
                    candidates.Remove(buffer);
                }

                if (candidates.Count == 0)
                    buffers.Remove(audioBufferHandle);
            }
        }

        buffer?.Dispose();
        return false;
    }

    private static AccessTools.FieldRef<AudioPlayer, Dictionary<nint, HashSet<WIVSMAudioBuffer>>>?
        CreateBuffersRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<AudioPlayer, Dictionary<nint, HashSet<WIVSMAudioBuffer>>>(
                "vsmAudioBuffersDictionary");
        }
        catch
        {
            return null;
        }
    }

    private static AccessTools.FieldRef<AudioPlayer, object>? CreateSyncRootRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<AudioPlayer, object>("vsmAudioBuffersLockObject");
        }
        catch
        {
            return null;
        }
    }
}
#endif
