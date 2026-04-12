using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using VOCALOIDPatcher.Utils;

namespace VOCALOIDPatcher.Patch;

public abstract class PatchBase
{
    public abstract string PatchName { get; }
    public abstract Type TargetClass { get; }
    public abstract string TargetMethodName { get; }
    public virtual bool IsConstructor => false;
    public virtual Type[]? ArgumentTypes => null;

    public void Apply(Harmony harmony)
    {
        var original = GetTargetMethod();

        if (original == null)
        {
            MessageUtils.ShowErrorMessage($"{PatchName}: 未在 {TargetClass.FullName} 中找到方法 {TargetMethodName}");
            return;
        }

        var methods = GetType().GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        var prefix      = FindHarmonyMethod(methods, typeof(HarmonyPrefix));
        var postfix     = FindHarmonyMethod(methods, typeof(HarmonyPostfix));
        var transpiler  = FindHarmonyMethod(methods, typeof(HarmonyTranspiler));
        var finalizer   = FindHarmonyMethod(methods, typeof(HarmonyFinalizer));
        var reversePatch= FindHarmonyMethod(methods, typeof(HarmonyReversePatch));

        try
        {
            harmony.Patch(original, prefix, postfix, transpiler, finalizer);

            if (reversePatch != null)
            {
                harmony.CreateReversePatcher(original, reversePatch).Patch();
            }
        }
        catch (Exception e)
        {
            MessageUtils.ShowErrorMessage($"{PatchName}: patch 失败, \n{e.Message}\n{e.StackTrace}");
        }
    }

    private MethodBase? GetTargetMethod()
    {
        var targetClass = TargetClass;

        if (IsConstructor)
        {
            return AccessTools.Constructor(targetClass, ArgumentTypes ?? Type.EmptyTypes);
        }

        return AccessTools.Method(targetClass, TargetMethodName, ArgumentTypes ?? Type.EmptyTypes);
    }
    
    private static HarmonyMethod? FindHarmonyMethod(MethodInfo[] methods, Type attrType)
    {
        var method = methods.FirstOrDefault(m => m.GetCustomAttributes(attrType, false).Any());
        return method != null ? new HarmonyMethod(method) : null;
    }

}