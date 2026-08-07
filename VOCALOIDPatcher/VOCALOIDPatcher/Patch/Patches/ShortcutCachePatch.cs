using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Input;
using HarmonyLib;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

public class ShortcutModifierCachePatch : PatchBase
{
    public override string PatchName        => "ShortcutModifierCachePatch";
    public override Type   TargetClass      => typeof(ShortcutKey);
    public override string TargetMethodName => "get_ModifierKeysDictionary";

    private static readonly Dictionary<string, ModifierKeys> Cache = new()
    {
        ["Alt"] = ModifierKeys.Alt,
        ["Ctrl"] = ModifierKeys.Control,
        ["Shift"] = ModifierKeys.Shift
    };

    [HarmonyPrefix]
    private static bool Prefix(ref Dictionary<string, ModifierKeys> __result)
    {
        __result = Cache;
        return false;
    }
}

public class ShortcutKeyMapCachePatch : PatchBase
{
    public override string PatchName        => "ShortcutKeyMapCachePatch";
    public override Type   TargetClass      => typeof(ShortcutKey);
    public override string TargetMethodName => "get_KeysDictionary";

    private static readonly object? Cache =
        AccessTools.PropertyGetter(typeof(ShortcutKey), "KeysDictionary")?.Invoke(null, null);

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        if (Cache == null || __originalMethod is not MethodInfo method)
            return instructions;

        return new[]
        {
            new CodeInstruction(OpCodes.Ldsfld,
                AccessTools.Field(typeof(ShortcutKeyMapCachePatch), nameof(Cache))),
            new CodeInstruction(OpCodes.Castclass, method.ReturnType),
            new CodeInstruction(OpCodes.Ret)
        };
    }
}

public class ShortcutJsonModifierCachePatch : PatchBase
{
    public override string PatchName        => "ShortcutJsonModifierCachePatch";
    public override Type   TargetClass      => typeof(ShortcutKeyJsonParser.ShortcutKeyData.KeyData);
    public override string TargetMethodName => "get_modifierKeysDictionary";

    private static readonly Dictionary<ModifierKeys, string> Cache = new()
    {
        [ModifierKeys.Alt] = "option",
        [ModifierKeys.Control] = "command",
        [ModifierKeys.Shift] = "shift"
    };

    [HarmonyPrefix]
    private static bool Prefix(ref Dictionary<ModifierKeys, string> __result)
    {
        __result = Cache;
        return false;
    }
}
