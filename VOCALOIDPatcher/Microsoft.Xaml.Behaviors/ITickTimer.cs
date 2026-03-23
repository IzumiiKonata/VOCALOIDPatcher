using System;

namespace Microsoft.Xaml.Behaviors;

internal interface ITickTimer
{
	TimeSpan Interval { get; set; }

	event EventHandler Tick;

	void Start();

	void Stop();
}
