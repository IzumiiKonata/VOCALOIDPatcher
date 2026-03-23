using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Microsoft.Xaml.Behaviors;

public sealed class InvokeCommandAction : TriggerAction<DependencyObject>
{
	private string commandName;

	public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(InvokeCommandAction), null);

	public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register("CommandParameter", typeof(object), typeof(InvokeCommandAction), null);

	public static readonly DependencyProperty EventArgsConverterProperty = DependencyProperty.Register("EventArgsConverter", typeof(IValueConverter), typeof(InvokeCommandAction), new PropertyMetadata(null));

	public static readonly DependencyProperty EventArgsConverterParameterProperty = DependencyProperty.Register("EventArgsConverterParameter", typeof(object), typeof(InvokeCommandAction), new PropertyMetadata(null));

	public static readonly DependencyProperty EventArgsParameterPathProperty = DependencyProperty.Register("EventArgsParameterPath", typeof(string), typeof(InvokeCommandAction), new PropertyMetadata(null));

	public string CommandName
	{
		get
		{
			ReadPreamble();
			return commandName;
		}
		set
		{
			if (CommandName != value)
			{
				WritePreamble();
				commandName = value;
				WritePostscript();
			}
		}
	}

	public ICommand Command
	{
		get
		{
			return (ICommand)GetValue(CommandProperty);
		}
		set
		{
			SetValue(CommandProperty, value);
		}
	}

	public object CommandParameter
	{
		get
		{
			return GetValue(CommandParameterProperty);
		}
		set
		{
			SetValue(CommandParameterProperty, value);
		}
	}

	public IValueConverter EventArgsConverter
	{
		get
		{
			return (IValueConverter)GetValue(EventArgsConverterProperty);
		}
		set
		{
			SetValue(EventArgsConverterProperty, value);
		}
	}

	public object EventArgsConverterParameter
	{
		get
		{
			return GetValue(EventArgsConverterParameterProperty);
		}
		set
		{
			SetValue(EventArgsConverterParameterProperty, value);
		}
	}

	public string EventArgsParameterPath
	{
		get
		{
			return (string)GetValue(EventArgsParameterPathProperty);
		}
		set
		{
			SetValue(EventArgsParameterPathProperty, value);
		}
	}

	public bool PassEventArgsToCommand { get; set; }

	protected override void Invoke(object parameter)
	{
		if (base.AssociatedObject == null)
		{
			return;
		}
		ICommand command = ResolveCommand();
		if (command != null)
		{
			object obj = CommandParameter;
			if (obj == null && !string.IsNullOrWhiteSpace(EventArgsParameterPath))
			{
				obj = GetEventArgsPropertyPathValue(parameter);
			}
			if (obj == null && EventArgsConverter != null)
			{
				obj = EventArgsConverter.Convert(parameter, typeof(object), EventArgsConverterParameter, CultureInfo.CurrentCulture);
			}
			if (obj == null && PassEventArgsToCommand)
			{
				obj = parameter;
			}
			if (command.CanExecute(obj))
			{
				command.Execute(obj);
			}
		}
	}

	private object GetEventArgsPropertyPathValue(object parameter)
	{
		object obj = parameter;
		string[] array = EventArgsParameterPath.Split('.');
		foreach (string name in array)
		{
			obj = obj.GetType().GetProperty(name).GetValue(obj, null);
		}
		return obj;
	}

	private ICommand ResolveCommand()
	{
		ICommand result = null;
		if (Command != null)
		{
			result = Command;
		}
		else if (base.AssociatedObject != null)
		{
			PropertyInfo[] properties = base.AssociatedObject.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				if (typeof(ICommand).IsAssignableFrom(propertyInfo.PropertyType) && string.Equals(propertyInfo.Name, CommandName, StringComparison.Ordinal))
				{
					result = (ICommand)propertyInfo.GetValue(base.AssociatedObject, null);
				}
			}
		}
		return result;
	}
}
