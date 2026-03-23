using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

public sealed class ActivateStateAction : PrototypingActionBase
{
	public static readonly DependencyProperty TargetScreenProperty = DependencyProperty.Register("TargetScreen", typeof(string), typeof(ActivateStateAction), new PropertyMetadata(null));

	public static readonly DependencyProperty TargetStateProperty = DependencyProperty.Register("TargetState", typeof(string), typeof(ActivateStateAction), new PropertyMetadata(null));

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

	public string TargetState
	{
		get
		{
			return GetValue(TargetStateProperty) as string;
		}
		set
		{
			SetValue(TargetStateProperty, value);
		}
	}

	protected override void Invoke(object parameter)
	{
		string text = TargetScreen;
		if (string.IsNullOrEmpty(text))
		{
			text = GetContainingScreen().GetType().ToString();
		}
		InteractionContext.GoToState(text, TargetState);
	}

	protected override Freezable CreateInstanceCore()
	{
		return new ActivateStateAction();
	}
}
