// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.Windows;
using System.Windows.Threading;

namespace Microsoft.Xaml.Behaviors.Core;

/// <summary>
///     A trigger that is triggered by a specified event occurring on its source and fires after a delay when that event is
///     fired.
/// </summary>
public class TimerTrigger : EventTrigger
{
    public static readonly DependencyProperty MillisecondsPerTickProperty = DependencyProperty.Register(
        "MillisecondsPerTick",
        typeof(double),
        typeof(TimerTrigger),
        new FrameworkPropertyMetadata(1000.0)
    );

    public static readonly DependencyProperty TotalTicksProperty = DependencyProperty.Register("TotalTicks",
        typeof(int),
        typeof(TimerTrigger),
        new FrameworkPropertyMetadata(-1)
    );

    private readonly ITickTimer timer;
    private EventArgs eventArgs;
    private int tickCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TimerTrigger" /> class.
    /// </summary>
    public TimerTrigger() :
        this(new DispatcherTickTimer())
    {
    }

    internal TimerTrigger(ITickTimer timer)
    {
        this.timer = timer;
    }


    /// <summary>
    ///     Gets or sets the number of milliseconds to wait between ticks. This is a dependency property.
    /// </summary>
    public double MillisecondsPerTick
    {
        get => (double)GetValue(MillisecondsPerTickProperty);
        set => SetValue(MillisecondsPerTickProperty, value);
    }

    /// <summary>
    ///     Gets or sets the total number of ticks to be fired before the trigger is finished.  This is a dependency property.
    /// </summary>
    public int TotalTicks
    {
        get => (int)GetValue(TotalTicksProperty);
        set => SetValue(TotalTicksProperty, value);
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

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (TotalTicks > 0 && ++tickCount >= TotalTicks) StopTimer();

        InvokeActions(eventArgs);
    }

    internal class DispatcherTickTimer : ITickTimer
    {
        private readonly DispatcherTimer dispatcherTimer;

        public DispatcherTickTimer()
        {
            dispatcherTimer = new DispatcherTimer();
        }

        public event EventHandler Tick
        {
            add => dispatcherTimer.Tick += value;
            remove => dispatcherTimer.Tick -= value;
        }

        public TimeSpan Interval
        {
            get => dispatcherTimer.Interval;
            set => dispatcherTimer.Interval = value;
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
}
