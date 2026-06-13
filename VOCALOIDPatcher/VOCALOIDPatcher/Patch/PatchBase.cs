using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using VOCALOIDPatcher.Translation;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Patch;

public abstract class PatchBase
{
    public abstract string PatchName { get; }
    public abstract Type TargetClass { get; }
    public abstract string TargetMethodName { get; }
    public virtual bool IsConstructor => false;
    public virtual Type[]? ArgumentTypes => null;

    public bool Applied { get; private set; }
    public string? FailureReason { get; private set; }

    [CLSCompliant(false)]
    public bool Apply(Harmony harmony)
    {
        Applied = false;
        FailureReason = null;

        try
        {
            MethodBase? original;
            try
            {
                original = GetTargetMethod();
            }
            catch (Exception e)
            {
                return Fail(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patch_ApplyFailed", PatchName, e.Message, e.StackTrace));
            }

            if (original == null)
                return Fail(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patch_MethodNotFound", PatchName, TargetClass.FullName, TargetMethodName));

            var methods = GetType().GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            var prefix = FindHarmonyMethod(methods, typeof(HarmonyPrefix));
            var postfix = FindHarmonyMethod(methods, typeof(HarmonyPostfix));
            var transpiler = FindHarmonyMethod(methods, typeof(HarmonyTranspiler));
            var finalizer = FindHarmonyMethod(methods, typeof(HarmonyFinalizer));
            var reversePatch = FindHarmonyMethod(methods, typeof(HarmonyReversePatch));

            harmony.Patch(original, prefix, postfix, transpiler, finalizer);

            if (reversePatch != null) harmony.CreateReversePatcher(original, reversePatch).Patch();

            Applied = true;
            return true;
        }
        catch (Exception e)
        {
            return Fail(TranslationManager.Tr("VOCALOIDPatcher_Debug_Patch_ApplyFailed", PatchName, e.Message, e.StackTrace));
        }
    }

    private bool Fail(string reason)
    {
        FailureReason = reason;
        Debug.Print(reason);
        return false;
    }

    private MethodBase? GetTargetMethod()
    {
        var targetClass = TargetClass;
        if (targetClass == null) return null;

        if (IsConstructor) return AccessTools.Constructor(targetClass, ArgumentTypes ?? Type.EmptyTypes);

        return AccessTools.Method(targetClass, TargetMethodName, ArgumentTypes ?? Type.EmptyTypes);
    }

    private static HarmonyMethod? FindHarmonyMethod(MethodInfo[] methods, Type attrType)
    {
        var method = methods.FirstOrDefault(m => m.GetCustomAttributes(attrType, false).Any());
        return method != null ? new HarmonyMethod(method) : null;
    }
}
