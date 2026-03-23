using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Microsoft.Xaml.Behaviors.Core;

[DefaultTrigger(typeof(ButtonBase), typeof(EventTrigger), "Click")]
[DefaultTrigger(typeof(TextBox), typeof(EventTrigger), "TextChanged")]
[DefaultTrigger(typeof(RichTextBox), typeof(EventTrigger), "TextChanged")]
[DefaultTrigger(typeof(ListBoxItem), typeof(EventTrigger), "Selected")]
[DefaultTrigger(typeof(TreeViewItem), typeof(EventTrigger), "Selected")]
[DefaultTrigger(typeof(Selector), typeof(EventTrigger), "SelectionChanged")]
[DefaultTrigger(typeof(TreeView), typeof(EventTrigger), "SelectedItemChanged")]
[DefaultTrigger(typeof(RangeBase), typeof(EventTrigger), "ValueChanged")]
public abstract class PrototypingActionBase : TriggerAction<DependencyObject>
{
	internal void TestInvoke(object parameter)
	{
		Invoke(parameter);
	}

	protected UserControl GetContainingScreen()
	{
		IEnumerable<UserControl> source = base.AssociatedObject.GetSelfAndAncestors().OfType<UserControl>();
		return source.FirstOrDefault((UserControl userControl) => InteractionContext.IsScreen(userControl.GetType().FullName)) ?? source.First();
	}
}
