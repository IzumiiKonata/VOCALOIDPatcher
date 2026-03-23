using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Microsoft.Xaml.Behaviors;

public static class VisualStateUtilities
{
	public static bool GoToState(FrameworkElement element, string stateName, bool useTransitions)
	{
		bool result = false;
		if (!string.IsNullOrEmpty(stateName))
		{
			if (element is Control control)
			{
				control.ApplyTemplate();
				result = VisualStateManager.GoToState(control, stateName, useTransitions);
			}
			else
			{
				result = VisualStateManager.GoToElementState(element, stateName, useTransitions);
			}
		}
		return result;
	}

	public static IList GetVisualStateGroups(FrameworkElement targetObject)
	{
		IList list = new List<VisualStateGroup>();
		if (targetObject != null)
		{
			list = VisualStateManager.GetVisualStateGroups(targetObject);
			if (list.Count == 0 && VisualTreeHelper.GetChildrenCount(targetObject) > 0)
			{
				list = VisualStateManager.GetVisualStateGroups(VisualTreeHelper.GetChild(targetObject, 0) as FrameworkElement);
			}
			if (list.Count == 0 && targetObject is UserControl { Content: FrameworkElement content })
			{
				list = VisualStateManager.GetVisualStateGroups(content);
			}
		}
		return list;
	}

	public static bool TryFindNearestStatefulControl(FrameworkElement contextElement, out FrameworkElement resolvedControl)
	{
		FrameworkElement frameworkElement = contextElement;
		if (frameworkElement == null)
		{
			resolvedControl = null;
			return false;
		}
		FrameworkElement frameworkElement2 = frameworkElement.Parent as FrameworkElement;
		bool result = true;
		while (!HasVisualStateGroupsDefined(frameworkElement) && ShouldContinueTreeWalk(frameworkElement2))
		{
			frameworkElement = frameworkElement2;
			frameworkElement2 = frameworkElement2.Parent as FrameworkElement;
		}
		if (HasVisualStateGroupsDefined(frameworkElement))
		{
			if (frameworkElement.TemplatedParent != null && frameworkElement.TemplatedParent is Control)
			{
				frameworkElement = frameworkElement.TemplatedParent as FrameworkElement;
			}
			else if (frameworkElement2 != null && frameworkElement2 is UserControl)
			{
				frameworkElement = frameworkElement2;
			}
		}
		else
		{
			result = false;
		}
		resolvedControl = frameworkElement;
		return result;
	}

	private static bool HasVisualStateGroupsDefined(FrameworkElement frameworkElement)
	{
		if (frameworkElement != null)
		{
			return VisualStateManager.GetVisualStateGroups(frameworkElement).Count != 0;
		}
		return false;
	}

	internal static FrameworkElement FindNearestStatefulControl(FrameworkElement contextElement)
	{
		FrameworkElement resolvedControl = null;
		TryFindNearestStatefulControl(contextElement, out resolvedControl);
		return resolvedControl;
	}

	private static bool ShouldContinueTreeWalk(FrameworkElement element)
	{
		if (element == null)
		{
			return false;
		}
		if (element is UserControl)
		{
			return false;
		}
		if (element.Parent == null)
		{
			FrameworkElement frameworkElement = FindTemplatedParent(element);
			if (frameworkElement == null || (!(frameworkElement is Control) && !(frameworkElement is ContentPresenter)))
			{
				return false;
			}
		}
		return true;
	}

	private static FrameworkElement FindTemplatedParent(FrameworkElement parent)
	{
		return parent.TemplatedParent as FrameworkElement;
	}
}
