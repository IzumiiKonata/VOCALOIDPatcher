using System;
using System.Windows;
using System.Windows.Threading;

namespace Microsoft.Xaml.Behaviors.Core;

public class TimerTrigger : EventTrigger
{
	internal class DispatcherTickTimer : ITickTimer
	{
		private DispatcherTimer dispatcherTimer;

		public TimeSpan Interval
		{
			get
			{
				return dispatcherTimer.Interval;
			}
			set
			{
				dispatcherTimer.Interval = value;
			}
		}

		public event EventHandler Tick
		{
			add
			{
				dispatcherTimer.Tick += value;
			}
			remove
			{
				dispatcherTimer.Tick -= value;
			}
		}

		public DispatcherTickTimer()
		{
			dispatcherTimer = new DispatcherTimer();
		}

		public void Start()
		{
			dispatcherTimer.Start();
		}

		public void Stop()
		{
			dispatcherTimer.Stop();
		}
	}

	public static readonly DependencyProperty MillisecondsPerTickProperty = DependencyProperty.Register("MillisecondsPerTick", typeof(double), typeof(TimerTrigger), new FrameworkPropertyMetadata(1000.0));

	public static readonly DependencyProperty TotalTicksProperty = DependencyProperty.Register("TotalTicks", typeof(int), typeof(TimerTrigger), new FrameworkPropertyMetadata(-1));

	private ITickTimer timer;

	private EventArgs eventArgs;

	private int tickCount;

	public double MillisecondsPerTick
	{
		get
		{
			return (double)GetValue(MillisecondsPerTickProperty);
		}
		set
		{
			SetValue(MillisecondsPerTickProperty, value);
		}
	}

	public int TotalTicks
	{
		get
		{
			return (int)GetValue(TotalTicksProperty);
		}
		set
		{
			SetValue(TotalTicksProperty, value);
		}
	}

	public TimerTrigger()
		: this(new DispatcherTickTimer())
	{
	}

	internal TimerTrigger(ITickTimer timer)
	{
		this.timer = timer;
	}

	protected override void OnEvent(EventArgs eventArgs)
	{
		StopTimer();
		this.eventArgs = eventArgs;
		tickCount = 0;
		StartTimer();
	}

	protected override void OnDetaching()
	{
		StopTimer();
		base.OnDetaching();
	}

	internal void StartTimer()
	{
		if (timer != null)
		{
			timer.Interval = TimeSpan.FromMilliseconds(MillisecondsPerTick);
			timer.Tick += OnTimerTick;
			timer.Start();
		}
	}

	internal void StopTimer()
	{
		if (timer != null)
		{
			timer.Stop();
			timer.Tick -= OnTimerTick;
		}
	}

	private void OnTimerTick(object sender, EventArgs e)
	{
		if (TotalTicks > 0 && ++tickCount >= TotalTicks)
		{
			StopTimer();
		}
		InvokeActions(eventArgs);
	}
}
