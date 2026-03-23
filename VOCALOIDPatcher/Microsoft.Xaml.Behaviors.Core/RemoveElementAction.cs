using System;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.Xaml.Behaviors.Core;

public class RemoveElementAction : TargetedTriggerAction<FrameworkElement>
{
	protected override void Invoke(object parameter)
	{
		if (base.AssociatedObject == null || base.Target == null)
		{
			return;
		}
		DependencyObject parent = base.Target.Parent;
		if (parent is Panel panel)
		{
			panel.Children.Remove(base.Target);
		}
		else if (parent is ContentControl contentControl)
		{
			if (contentControl.Content == base.Target)
			{
				contentControl.Content = null;
			}
		}
		else if (parent is ItemsControl itemsControl)
		{
			itemsControl.Items.Remove(base.Target);
		}
		else if (parent is Page page)
		{
			if (page.Content == base.Target)
			{
				page.Content = null;
			}
		}
		else if (parent is Decorator decorator)
		{
			if (decorator.Child == base.Target)
			{
				decorator.Child = null;
			}
		}
		else if (parent != null)
		{
			throw new InvalidOperationException(ExceptionStringTable.UnsupportedRemoveTargetExceptionMessage);
		}
	}
}
