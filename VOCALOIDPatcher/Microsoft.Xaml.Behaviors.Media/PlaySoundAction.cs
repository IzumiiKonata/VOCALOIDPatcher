using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Microsoft.Xaml.Behaviors.Media;

public class PlaySoundAction : TriggerAction<DependencyObject>
{
	public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(Uri), typeof(PlaySoundAction), null);

	public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register("Volume", typeof(double), typeof(PlaySoundAction), new PropertyMetadata(0.5));

	public Uri Source
	{
		get
		{
			return (Uri)GetValue(SourceProperty);
		}
		set
		{
			SetValue(SourceProperty, value);
		}
	}

	public double Volume
	{
		get
		{
			return (double)GetValue(VolumeProperty);
		}
		set
		{
			SetValue(VolumeProperty, value);
		}
	}

	protected virtual void SetMediaElementProperties(MediaElement mediaElement)
	{
		if (mediaElement != null)
		{
			mediaElement.Source = Source;
			mediaElement.Volume = Volume;
		}
	}

	protected override void Invoke(object parameter)
	{
		if (!(Source == null) && base.AssociatedObject != null)
		{
			Popup popup = new Popup();
			MediaElement mediaElement = new MediaElement();
			popup.Child = mediaElement;
			mediaElement.Visibility = Visibility.Collapsed;
			SetMediaElementProperties(mediaElement);
			mediaElement.MediaEnded += delegate
			{
				popup.Child = null;
				popup.IsOpen = false;
			};
			mediaElement.MediaFailed += delegate
			{
				popup.Child = null;
				popup.IsOpen = false;
			};
			popup.IsOpen = true;
		}
	}
}
