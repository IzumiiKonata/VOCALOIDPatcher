using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Patch;

internal static class PatchRuntimeGuard
{
    private static readonly Dictionary<MethodBase, string?> Features = new();
    private static readonly HashSet<MethodBase> Guarded = new();

    private static HarmonyMethod? _finalizer;

    public static void Install(Harmony harmony, MethodBase original, string? featureKey)
    {
        if (!Features.ContainsKey(original))
            Features[original] = featureKey;

        if (!Guarded.Add(original))
            return;

        _finalizer ??= new HarmonyMethod(
            typeof(PatchRuntimeGuard).GetMethod(nameof(Finalizer), BindingFlags.Static | BindingFlags.NonPublic)!);

        try
        {
            harmony.Patch(original, finalizer: _finalizer);
        }
        catch
        {
            Guarded.Remove(original);
        }
    }

    private static Exception? Finalizer(Exception? __exception, MethodBase __originalMethod)
    {
        if (__exception is not (MissingMemberException or TypeLoadException))
            return __exception;

        var feature = Features.TryGetValue(__originalMethod, out var f) ? f : null;

        Debug.Print(TranslationManager.Tr(
            "VOCALOIDPatcher_Debug_Patcher_FeatureDisabledRuntime",
            feature ?? __originalMethod.Name, __exception.Message));

        if (feature != null)
            Settings.DisableFeature(feature);

        return null;
    }
}
