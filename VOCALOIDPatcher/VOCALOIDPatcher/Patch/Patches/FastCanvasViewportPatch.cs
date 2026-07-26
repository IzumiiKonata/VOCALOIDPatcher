using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

    public static void Clear() => Set.Clear();
}

internal static class ViewportOrder
{
    private sealed class State
    {
        public int Count = -1;
        public UIControl? First;
        public UIControl? Last;
        public double FirstLeft;
        public double LastLeft;
        public bool Sorted;
    }

    private static readonly ConditionalWeakTable<FastCanvas, State> States = new();

    public static bool CanUseBinary(FastCanvas canvas)
    {
        var children = canvas.VirtualChildren;
        if (children.Count < 256)
            return false;

        var state = States.GetOrCreateValue(canvas);
        var first = children[0];
        var last = children[^1];
        double firstLeft = Canvas.GetLeft(first);
        double lastLeft = Canvas.GetLeft(last);

        if (state.Count == children.Count && ReferenceEquals(state.First, first)
            && ReferenceEquals(state.Last, last) && state.FirstLeft.Equals(firstLeft)
            && state.LastLeft.Equals(lastLeft))
            return state.Sorted;

        state.Count = children.Count;
        state.First = first;
        state.Last = last;
        state.FirstLeft = firstLeft;
        state.LastLeft = lastLeft;
        state.Sorted = true;

        double previous = firstLeft;
        for (int i = 1; i < children.Count; i++)
        {
            double current = Canvas.GetLeft(children[i]);
            if (current < previous)
            {
                state.Sorted = false;
                break;
            }
            previous = current;
        }

        return state.Sorted;
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
        var virtualChildren = __instance.VirtualChildren;

        try
        {
            if (!ViewportOrder.CanUseBinary(__instance))
            {
                foreach (var virtualChild in virtualChildren)
                {
                    var bounds = new Rect(Canvas.GetLeft(virtualChild), Canvas.GetTop(virtualChild),
                        GetWidth(virtualChild), GetHeight(virtualChild));
                    bool isVisible = visible.Contains(virtualChild);
                    if (!rect.IntersectsWith(bounds))
                    {
                        if (isVisible)
                            children.Remove(virtualChild);
                    }
                    else if (!isVisible)
                    {
                        children.Add(virtualChild);
                    }
                }

                return false;
            }

            foreach (var child in visible)
            {
                if (child is not FrameworkElement element)
                    continue;

                var bounds = new Rect(Canvas.GetLeft(element), Canvas.GetTop(element),
                    GetWidth(element), GetHeight(element));
                if (!rect.IntersectsWith(bounds))
                    children.Remove(element);
            }

            if (virtualChildren.Count == 0)
                return false;

            int start = LowerBound(virtualChildren, rect.Left);
            while (start > 0 && GetRight(virtualChildren[start - 1]) >= rect.Left)
                start--;

            for (int i = start; i < virtualChildren.Count; i++)
            {
                var child = virtualChildren[i];
                double childLeft = Canvas.GetLeft(child);
                if (childLeft > rect.Right)
                    break;

                var bounds = new Rect(childLeft, Canvas.GetTop(child), GetWidth(child), GetHeight(child));
                if (rect.IntersectsWith(bounds) && !visible.Contains(child))
                    children.Add(child);
            }
        }
        finally
        {
            ViewportScratch.Clear();
        }

        return false;
    }

    private static int LowerBound(List<UIControl> children, double left)
    {
        int low = 0;
        int high = children.Count;
        while (low < high)
        {
            int mid = low + ((high - low) >> 1);
            if (Canvas.GetLeft(children[mid]) < left)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private static double GetRight(FrameworkElement element)
    {
        double right = Canvas.GetRight(element);
        return double.IsNaN(right) ? Canvas.GetLeft(element) + GetWidth(element) : right;
    }

    private static double GetWidth(FrameworkElement element) =>
        double.IsNaN(element.Width) ? element.ActualWidth : element.Width;

    private static double GetHeight(FrameworkElement element) =>
        double.IsNaN(element.Height) ? element.ActualHeight : element.Height;
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
        var virtualChildren = __instance.VirtualChildren;

        try
        {
            if (!ViewportOrder.CanUseBinary(__instance))
            {
                foreach (var virtualChild in virtualChildren)
                {
                    double childLeft = Canvas.GetLeft(virtualChild);
                    bool isVisible = visible.Contains(virtualChild);
                    if (GetRight(virtualChild) < left || right < childLeft)
                    {
                        if (isVisible)
                            children.Remove(virtualChild);
                    }
                    else if (!isVisible)
                    {
                        children.Add(virtualChild);
                    }
                }

                return false;
            }

            foreach (var child in visible)
            {
                if (child is FrameworkElement element
                    && (GetRight(element) < left || right < Canvas.GetLeft(element)))
                    children.Remove(element);
            }

            if (virtualChildren.Count == 0)
                return false;

            int start = LowerBound(virtualChildren, left);
            while (start > 0 && GetRight(virtualChildren[start - 1]) >= left)
                start--;

            for (int i = start; i < virtualChildren.Count; i++)
            {
                var child = virtualChildren[i];
                double childLeft = Canvas.GetLeft(child);
                if (right < childLeft)
                    break;

                if (GetRight(child) >= left && !visible.Contains(child))
                    children.Add(child);
            }
        }
        finally
        {
            ViewportScratch.Clear();
        }

        return false;
    }

    private static int LowerBound(List<UIControl> children, double left)
    {
        int low = 0;
        int high = children.Count;
        while (low < high)
        {
            int mid = low + ((high - low) >> 1);
            if (Canvas.GetLeft(children[mid]) < left)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private static double GetRight(FrameworkElement element)
    {
        double right = Canvas.GetRight(element);
        if (!double.IsNaN(right))
            return right;

        double width = double.IsNaN(element.Width) ? element.ActualWidth : element.Width;
        return Canvas.GetLeft(element) + width;
    }
}
