using System;
using System.Collections.Generic;
using HarmonyLib;
using VOCALOIDPatcher.Config;
using Yamaha.VOCALOID;
using Yamaha.VOCALOID.MusicalEditor;

namespace VOCALOIDPatcher.Patch.Patches;

public class ParameterVisibleRangePatch : PatchBase
{
    public override string PatchName        => "ParameterVisibleRangePatch";
    public override Type   TargetClass      => typeof(UIControlParameter);
    public override string TargetMethodName => "Render";

    public override Type[] ArgumentTypes => new[]
    {
        typeof(System.Windows.Media.DrawingContext),
        typeof(ControlParameterTypeEnum),
        typeof(ZoomScrollViewer),
        typeof(int),
        typeof(int),
        typeof(double),
        typeof(double)
    };

    private sealed class RangeState
    {
        public List<ControlParameter>? Source;
        public bool Restored;
    }

    private static readonly AccessTools.FieldRef<UIControlParameterBase, List<ControlParameter>>?
        Controls = CreateControlsRef();

    [HarmonyPrefix]
    private static void Prefix(
        UIControlParameter __instance,
        ControlParameterTypeEnum type,
        ZoomScrollViewer zsv,
        double widthPerTick,
        out RangeState? __state)
    {
        __state = null;

        if (!Settings.DeferParameterViewUpdate || widthPerTick <= 0.0 || Controls == null)
            return;

        var controls = Controls(__instance);
        if (controls.Count < 256)
            return;

        double left = zsv.HorizontalOffset - 16.0;
        double right = zsv.HorizontalOffset + zsv.ViewportWidth + 16.0;
        int start = LowerBound(controls, left, widthPerTick);
        int end = UpperBound(controls, right, widthPerTick);

        if (type != ControlParameterTypeEnum.Velocity && type != ControlParameterTypeEnum.Mouth)
            start = Math.Max(0, start - 1);

        end = Math.Min(controls.Count, end + 1);
        if (start == 0 && end == controls.Count)
            return;

        var visible = new List<ControlParameter>(end - start);
        for (int i = start; i < end; i++)
            visible.Add(controls[i]);

        __state = new RangeState { Source = controls };
        Controls(__instance) = visible;
    }

    [HarmonyPostfix]
    private static void Postfix(UIControlParameter __instance, RangeState? __state)
    {
        Restore(__instance, __state);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(UIControlParameter __instance, RangeState? __state, Exception? __exception)
    {
        Restore(__instance, __state);
        return __exception;
    }

    private static void Restore(UIControlParameter instance, RangeState? state)
    {
        if (state?.Source == null || state.Restored)
            return;

        state.Restored = true;
        if (Controls != null)
            Controls(instance) = state.Source;
    }

    private static int LowerBound(List<ControlParameter> controls, double x, double widthPerTick)
    {
        int low = 0;
        int high = controls.Count;
        while (low < high)
        {
            int mid = low + ((high - low) >> 1);
            if (controls[mid].RelPosTick.Value * widthPerTick < x)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private static int UpperBound(List<ControlParameter> controls, double x, double widthPerTick)
    {
        int low = 0;
        int high = controls.Count;
        while (low < high)
        {
            int mid = low + ((high - low) >> 1);
            if (controls[mid].RelPosTick.Value * widthPerTick <= x)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private static AccessTools.FieldRef<UIControlParameterBase, List<ControlParameter>>? CreateControlsRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<UIControlParameterBase, List<ControlParameter>>(
                "<Ctrls>k__BackingField");
        }
        catch
        {
            return null;
        }
    }
}
