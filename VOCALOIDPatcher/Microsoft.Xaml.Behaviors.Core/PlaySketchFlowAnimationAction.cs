using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

public sealed class PlaySketchFlowAnimationAction : PrototypingActionBase
{
	public static readonly DependencyProperty TargetScreenProperty = DependencyProperty.Register("TargetScreen", typeof(string), typeof(PlaySketchFlowAnimationAction), new PropertyMetadata(null));

	public static readonly DependencyProperty SketchFlowAnimationProperty = DependencyProperty.Register("StateAnimation", typeof(string), typeof(PlaySketchFlowAnimationAction), new PropertyMetadata(null));

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

	public string SketchFlowAnimation
	{
		get
		{
			return GetValue(SketchFlowAnimationProperty) as string;
		}
		set
		{
			SetValue(SketchFlowAnimationProperty, value);
		}
	}

	protected override void Invoke(object parameter)
	{
		string text = TargetScreen;
		if (string.IsNullOrEmpty(text))
		{
			text = GetContainingScreen().GetType().ToString();
		}
		InteractionContext.PlaySketchFlowAnimation(SketchFlowAnimation, text);
	}

	protected override Freezable CreateInstanceCore()
	{
		return new PlaySketchFlowAnimationAction();
	}
}
