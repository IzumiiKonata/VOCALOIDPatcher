using System.Windows;
using System.Windows.Media.Animation;

namespace Microsoft.Xaml.Behaviors.Media;

public abstract class StoryboardTrigger : TriggerBase<DependencyObject>
{
	public static readonly DependencyProperty StoryboardProperty = DependencyProperty.Register("Storyboard", typeof(Storyboard), typeof(StoryboardTrigger), new FrameworkPropertyMetadata(OnStoryboardChanged));

	public Storyboard Storyboard
	{
		get
		{
			return (Storyboard)GetValue(StoryboardProperty);
		}
		set
		{
			SetValue(StoryboardProperty, value);
		}
	}

	private static void OnStoryboardChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
	{
		if (sender is StoryboardTrigger storyboardTrigger)
		{
			storyboardTrigger.OnStoryboardChanged(args);
		}
	}

	protected virtual void OnStoryboardChanged(DependencyPropertyChangedEventArgs args)
	{
	}
}
