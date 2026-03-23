using System;
using System.Windows.Input;

namespace Microsoft.Xaml.Behaviors.Core;

public sealed class ActionCommand : ICommand
{
	private Action action;

	private Action<object> objectAction;

	private event EventHandler CanExecuteChanged;

	event EventHandler ICommand.CanExecuteChanged
	{
		add
		{
			CanExecuteChanged += value;
		}
		remove
		{
			CanExecuteChanged -= value;
		}
	}

	public ActionCommand(Action action)
	{
		this.action = action;
	}

	public ActionCommand(Action<object> objectAction)
	{
		this.objectAction = objectAction;
	}

	bool ICommand.CanExecute(object parameter)
	{
		return true;
	}

	public void Execute(object parameter)
	{
		if (objectAction != null)
		{
			objectAction(parameter);
		}
		else
		{
			action();
		}
	}
}
