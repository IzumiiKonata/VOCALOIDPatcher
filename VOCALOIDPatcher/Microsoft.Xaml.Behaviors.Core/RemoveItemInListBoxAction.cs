using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.Xaml.Behaviors.Core;

public sealed class RemoveItemInListBoxAction : TriggerAction<FrameworkElement>
{
	private ListBoxItem ItemContainer => (ListBoxItem)base.AssociatedObject.GetSelfAndAncestors().FirstOrDefault((DependencyObject element) => element is ListBoxItem);

	private ItemsControl ItemsControl => (ItemsControl)base.AssociatedObject.GetSelfAndAncestors().FirstOrDefault((DependencyObject element) => element is ItemsControl);

	protected override void Invoke(object parameter)
	{
		ItemsControl itemsControl = ItemsControl;
		if (itemsControl == null)
		{
			return;
		}
		if (itemsControl.ItemsSource != null)
		{
			if (itemsControl.ItemsSource is IList { IsReadOnly: false } list && list.Contains(base.AssociatedObject.DataContext))
			{
				list.Remove(base.AssociatedObject.DataContext);
			}
		}
		else if (ItemsControl is ListBox listBox)
		{
			ListBoxItem itemContainer = ItemContainer;
			if (itemContainer != null)
			{
				listBox.Items.Remove(itemContainer.Content);
			}
		}
	}
}
