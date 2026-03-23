using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Microsoft.Xaml.Behaviors.Media;

[CLSCompliant(false)]
public class ControlStoryboardAction : StoryboardAction
{
	public static readonly DependencyProperty ControlStoryboardProperty = DependencyProperty.Register("ControlStoryboardOption", typeof(ControlStoryboardOption), typeof(ControlStoryboardAction));

	public ControlStoryboardOption ControlStoryboardOption
	{
		get
		{
			return (ControlStoryboardOption)GetValue(ControlStoryboardProperty);
		}
		set
		{
			SetValue(ControlStoryboardProperty, value);
		}
	}

	protected override void Invoke(object parameter)
	{
		if (base.AssociatedObject == null || base.Storyboard == null)
		{
			return;
		}
		switch (ControlStoryboardOption)
		{
		case ControlStoryboardOption.Play:
			base.Storyboard.Begin();
			break;
		case ControlStoryboardOption.Stop:
			base.Storyboard.Stop();
			break;
		case ControlStoryboardOption.TogglePlayPause:
		{
			ClockState clockState = ClockState.Stopped;
			bool flag = false;
			try
			{
				clockState = base.Storyboard.GetCurrentState();
				flag = base.Storyboard.GetIsPaused();
			}
			catch (InvalidOperationException)
			{
			}
			if (clockState == ClockState.Stopped)
			{
				base.Storyboard.Begin();
			}
			else if (flag)
			{
				base.Storyboard.Resume();
			}
			else
			{
				base.Storyboard.Pause();
			}
			break;
		}
		case ControlStoryboardOption.Pause:
			base.Storyboard.Pause();
			break;
		case ControlStoryboardOption.Resume:
			base.Storyboard.Resume();
			break;
		case ControlStoryboardOption.SkipToFill:
			base.Storyboard.SkipToFill();
			break;
		}
	}
}
