using System;

namespace Microsoft.Xaml.Behaviors;

public class PreviewInvokeEventArgs : EventArgs
{
	public bool Cancelling { get; set; }
}
