using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

public sealed class NavigateBackAction : PrototypingActionBase
{
	protected override void Invoke(object parameter)
	{
		InteractionContext.GoBack();
	}

	protected override Freezable CreateInstanceCore()
	{
		return new NavigateBackAction();
	}
}
