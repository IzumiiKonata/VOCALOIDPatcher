using System;
using System.Text.Json;
using HarmonyLib;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

internal static class UserSettingsJsonOptionsCache
{
    internal static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
}

public class CommonUserSettingsJsonOptionsPatch : PatchBase
{
    public override string PatchName        => "CommonUserSettingsJsonOptionsPatch";
    public override Type   TargetClass      => AccessTools.Inner(typeof(UserSettings), "UserSettings_Common");
    public override string TargetMethodName => "get_jsonSerializeOption";

    [HarmonyPrefix]
    private static bool Prefix(ref JsonSerializerOptions __result)
    {
        __result = UserSettingsJsonOptionsCache.Options;
        return false;
    }
}

public class UniqueUserSettingsJsonOptionsPatch : PatchBase
{
    public override string PatchName        => "UniqueUserSettingsJsonOptionsPatch";
    public override Type   TargetClass      => AccessTools.Inner(typeof(UserSettings), "UserSettings_Unique");
    public override string TargetMethodName => "get_jsonSerializeOption";

    [HarmonyPrefix]
    private static bool Prefix(ref JsonSerializerOptions __result)
    {
        __result = UserSettingsJsonOptionsCache.Options;
        return false;
    }
}
