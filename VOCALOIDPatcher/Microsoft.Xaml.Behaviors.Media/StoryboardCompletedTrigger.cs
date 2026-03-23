using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Microsoft.Xaml.Behaviors.Media;

public class StoryboardCompletedTrigger : StoryboardTrigger
{
	protected override void OnDetaching()
	{
		base.OnDetaching();
		if (base.Storyboard != null)
		{
			base.Storyboard.Completed -= Storyboard_Completed;
		}
	}

	protected override void OnStoryboardChanged(DependencyPropertyChangedEventArgs args)
	{
		Storyboard storyboard = args.OldValue as Storyboard;
		Storyboard storyboard2 = args.NewValue as Storyboard;
		if (storyboard != storyboard2)
		{
			if (storyboard != null)
			{
				storyboard.Completed -= Storyboard_Completed;
			}
			if (storyboard2 != null)
			{
				storyboard2.Completed += Storyboard_Completed;
			}
		}
	}

	private void Storyboard_Completed(object sender, EventArgs e)
	{
		InvokeActions(e);
	}
}
