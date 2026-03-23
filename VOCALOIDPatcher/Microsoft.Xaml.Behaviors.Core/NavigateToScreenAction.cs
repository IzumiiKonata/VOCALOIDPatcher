using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.Xaml.Behaviors.Core;

public sealed class NavigateToScreenAction : PrototypingActionBase
{
	public static readonly DependencyProperty TargetScreenProperty = DependencyProperty.Register("TargetScreen", typeof(string), typeof(NavigateToScreenAction), new PropertyMetadata(null));

	public string TargetScreen
	{
		get
		{
			return GetValue(TargetScreenProperty) as string;
		}
		set
		{
			SetValue(TargetScreenProperty, value);
		}
	}

	protected override void Invoke(object parameter)
	{
		Assembly assembly = null;
		UserControl userControl = base.AssociatedObject.GetSelfAndAncestors().OfType<UserControl>().FirstOrDefault();
		if (userControl != null)
		{
			assembly = userControl.GetType().Assembly;
		}
		InteractionContext.GoToScreen(TargetScreen, assembly);
	}

	protected override Freezable CreateInstanceCore()
	{
		return new NavigateToScreenAction();
	}
}
