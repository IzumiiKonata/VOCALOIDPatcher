using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Microsoft.Xaml.Behaviors.Core;

[DefaultTrigger(typeof(FrameworkElement), typeof(EventTrigger), "Loaded")]
[DefaultTrigger(typeof(ButtonBase), typeof(EventTrigger), "Loaded")]
public sealed class NavigationMenuAction : TargetedTriggerAction<FrameworkElement>
{
	public static readonly DependencyProperty InactiveStateProperty = DependencyProperty.Register("InactiveState", typeof(string), typeof(NavigationMenuAction), new PropertyMetadata(null));

	public static readonly DependencyProperty TargetScreenProperty = DependencyProperty.Register("TargetScreen", typeof(string), typeof(NavigationMenuAction), new PropertyMetadata(null));

	public static readonly DependencyProperty ActiveStateProperty = DependencyProperty.Register("ActiveState", typeof(string), typeof(NavigationMenuAction), new PropertyMetadata(null));

	public string TargetScreen
	{
		get
		{
			return (string)GetValue(TargetScreenProperty);
		}
		set
		{
			SetValue(TargetScreenProperty, value);
		}
	}

	public string ActiveState
	{
		get
		{
			return (string)GetValue(ActiveStateProperty);
		}
		set
		{
			SetValue(ActiveStateProperty, value);
		}
	}

	public string InactiveState
	{
		get
		{
			return (string)GetValue(InactiveStateProperty);
		}
		set
		{
			SetValue(InactiveStateProperty, value);
		}
	}

	private bool IsTargetObjectSet => ReadLocalValue(TargetedTriggerAction.TargetObjectProperty) != DependencyProperty.UnsetValue;

	private FrameworkElement StateTarget { get; set; }

	protected override void OnTargetChanged(FrameworkElement oldTarget, FrameworkElement newTarget)
	{
		base.OnTargetChanged(oldTarget, newTarget);
		FrameworkElement resolvedControl = null;
		if (string.IsNullOrEmpty(base.TargetName) && !IsTargetObjectSet)
		{
			VisualStateUtilities.TryFindNearestStatefulControl(base.AssociatedObject as FrameworkElement, out resolvedControl);
		}
		else
		{
			resolvedControl = base.Target;
		}
		StateTarget = resolvedControl;
	}

	protected override void Invoke(object parameter)
	{
		if (base.AssociatedObject != null)
		{
			InvokeImpl(StateTarget);
		}
	}

	internal void InvokeImpl(FrameworkElement stateTarget)
	{
		if (stateTarget == null || string.IsNullOrEmpty(ActiveState) || string.IsNullOrEmpty(InactiveState) || string.IsNullOrEmpty(TargetScreen))
		{
			return;
		}
		UserControl? userControl = stateTarget.GetSelfAndAncestors().OfType<UserControl>().FirstOrDefault((UserControl control) => control.GetType().ToString() == TargetScreen);
		string text = InactiveState;
		if (userControl != null)
		{
			text = ActiveState;
		}
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (stateTarget is ToggleButton toggleButton)
		{
			if (text == "Checked")
			{
				toggleButton.IsChecked = true;
				return;
			}
			if (text == "Unchecked")
			{
				toggleButton.IsChecked = false;
				return;
			}
		}
		if (text == "Disabled")
		{
			stateTarget.IsEnabled = false;
		}
		else
		{
			VisualStateUtilities.GoToState(stateTarget, text, useTransitions: true);
		}
	}

	protected override Freezable CreateInstanceCore()
	{
		return new NavigationMenuAction();
	}
}
