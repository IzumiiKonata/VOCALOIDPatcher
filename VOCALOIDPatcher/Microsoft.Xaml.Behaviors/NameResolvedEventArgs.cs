using System;

namespace Microsoft.Xaml.Behaviors;

internal sealed class NameResolvedEventArgs : EventArgs
{
	private object oldObject;

	private object newObject;

	public object OldObject => oldObject;

	public object NewObject => newObject;

	public NameResolvedEventArgs(object oldObject, object newObject)
	{
		this.oldObject = oldObject;
		this.newObject = newObject;
	}
}
