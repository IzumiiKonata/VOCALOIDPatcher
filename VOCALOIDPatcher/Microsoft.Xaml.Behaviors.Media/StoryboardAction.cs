using System.Windows;
using System.Windows.Media.Animation;

namespace Microsoft.Xaml.Behaviors.Media;

public abstract class StoryboardAction : TriggerAction<DependencyObject>
{
	public static readonly DependencyProperty StoryboardProperty = DependencyProperty.Register("Storyboard", typeof(Storyboard), typeof(StoryboardAction), new FrameworkPropertyMetadata(OnStoryboardChanged));

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
		if (sender is StoryboardAction storyboardAction)
		{
			storyboardAction.OnStoryboardChanged(args);
		}
	}

	protected virtual void OnStoryboardChanged(DependencyPropertyChangedEventArgs args)
	{
	}
}
