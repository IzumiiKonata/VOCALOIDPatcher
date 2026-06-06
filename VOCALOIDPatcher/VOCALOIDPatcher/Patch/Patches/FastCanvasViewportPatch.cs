using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using HarmonyLib;
using Yamaha.VOCALOID;

namespace VOCALOIDPatcher.Patch.Patches;

internal static class ViewportScratch
{
    private static readonly HashSet<UIElement> Set = new();

    public static HashSet<UIElement> Snapshot(UIElementCollection children)
    {
        Set.Clear();
        foreach (UIElement child in children)
            Set.Add(child);
        return Set;
    }
}

public class FastCanvasViewportRectPatch : PatchBase
{
    public override string PatchName        => "FastCanvasViewportRectPatch";
    public override Type   TargetClass      => typeof(FastCanvas);
    public override string TargetMethodName => "UpdateViewportLinear";

    public override Type[] ArgumentTypes => new[] { typeof(Rect) };

    [HarmonyPrefix]
    private static bool Prefix(FastCanvas __instance, Rect rect)
    {
        var children = __instance.Children;
        var visible = ViewportScratch.Snapshot(children);

        foreach (var virtualChild in __instance.VirtualChildren)
        {
            var bounds = new Rect(Canvas.GetLeft(virtualChild), Canvas.GetTop(virtualChild),
                virtualChild.Width, virtualChild.Height);
            var isChild = visible.Contains(virtualChild);

            if (!rect.IntersectsWith(bounds))
            {
                if (isChild)
                    children.Remove(virtualChild);
            }
            else if (!isChild)
            {
                children.Add(virtualChild);
            }
        }

        return false;
    }
}

public class FastCanvasViewportRangePatch : PatchBase
{
    public override string PatchName        => "FastCanvasViewportRangePatch";
    public override Type   TargetClass      => typeof(FastCanvas);
    public override string TargetMethodName => "UpdateViewportLinear";

    public override Type[] ArgumentTypes => new[] { typeof(double), typeof(double) };

    [HarmonyPrefix]
    private static bool Prefix(FastCanvas __instance, double left, double right)
    {
        var children = __instance.Children;
        var visible = ViewportScratch.Snapshot(children);

        foreach (var virtualChild in __instance.VirtualChildren)
        {
            var childLeft = Canvas.GetLeft(virtualChild);
            var isChild = visible.Contains(virtualChild);

            if (Canvas.GetRight(virtualChild) < left || right < childLeft)
            {
                if (isChild)
                    children.Remove(virtualChild);
            }
            else if (!isChild)
            {
                children.Add(virtualChild);
            }
        }

        return false;
    }
}
