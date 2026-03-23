using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

public sealed class NavigateForwardAction : PrototypingActionBase
{
	protected override void Invoke(object parameter)
	{
		InteractionContext.GoForward();
	}

	protected override Freezable CreateInstanceCore()
	{
		return new NavigateForwardAction();
	}
}
