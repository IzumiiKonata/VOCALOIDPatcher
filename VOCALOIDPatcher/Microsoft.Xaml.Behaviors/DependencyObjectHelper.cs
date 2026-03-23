using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Microsoft.Xaml.Behaviors;

public static class DependencyObjectHelper
{
	public static IEnumerable<DependencyObject> GetSelfAndAncestors(this DependencyObject dependencyObject)
	{
		while (dependencyObject != null)
		{
			yield return dependencyObject;
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
		}
	}
}
