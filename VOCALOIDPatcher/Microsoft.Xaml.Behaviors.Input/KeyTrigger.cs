using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Microsoft.Xaml.Behaviors.Input;

public class KeyTrigger : EventTriggerBase<UIElement>
{
	public static readonly DependencyProperty KeyProperty = DependencyProperty.Register("Key", typeof(Key), typeof(KeyTrigger));

	public static readonly DependencyProperty ModifiersProperty = DependencyProperty.Register("Modifiers", typeof(ModifierKeys), typeof(KeyTrigger));

	public static readonly DependencyProperty ActiveOnFocusProperty = DependencyProperty.Register("ActiveOnFocus", typeof(bool), typeof(KeyTrigger));

	public static readonly DependencyProperty FiredOnProperty = DependencyProperty.Register("FiredOn", typeof(KeyTriggerFiredOn), typeof(KeyTrigger));

	private UIElement targetElement;

	public Key Key
	{
		get
		{
			return (Key)GetValue(KeyProperty);
		}
		set
		{
			SetValue(KeyProperty, value);
		}
	}

	public ModifierKeys Modifiers
	{
		get
		{
			return (ModifierKeys)GetValue(ModifiersProperty);
		}
		set
		{
			SetValue(ModifiersProperty, value);
		}
	}

	public bool ActiveOnFocus
	{
		get
		{
			return (bool)GetValue(ActiveOnFocusProperty);
		}
		set
		{
			SetValue(ActiveOnFocusProperty, value);
		}
	}

	public KeyTriggerFiredOn FiredOn
	{
		get
		{
			return (KeyTriggerFiredOn)GetValue(FiredOnProperty);
		}
		set
		{
			SetValue(FiredOnProperty, value);
		}
	}

	protected override string GetEventName()
	{
		return "Loaded";
	}

	private void OnKeyPress(object sender, KeyEventArgs e)
	{
		if (e.Key == Key && Keyboard.Modifiers == GetActualModifiers(e.Key, Modifiers))
		{
			InvokeActions(e);
		}
	}

	private static ModifierKeys GetActualModifiers(Key key, ModifierKeys modifiers)
	{
		switch (key)
		{
		case Key.LeftCtrl:
		case Key.RightCtrl:
			modifiers |= ModifierKeys.Control;
			break;
		case Key.LeftAlt:
		case Key.RightAlt:
		case Key.System:
			modifiers |= ModifierKeys.Alt;
			break;
		case Key.LeftShift:
		case Key.RightShift:
			modifiers |= ModifierKeys.Shift;
			break;
		}
		return modifiers;
	}

	protected override void OnEvent(EventArgs eventArgs)
	{
		if (ActiveOnFocus)
		{
			targetElement = base.Source;
		}
		else
		{
			targetElement = GetRoot(base.Source);
		}
		if (FiredOn == KeyTriggerFiredOn.KeyDown)
		{
			targetElement.KeyDown += OnKeyPress;
		}
		else
		{
			targetElement.KeyUp += OnKeyPress;
		}
	}

	protected override void OnDetaching()
	{
		if (targetElement != null)
		{
			if (FiredOn == KeyTriggerFiredOn.KeyDown)
			{
				targetElement.KeyDown -= OnKeyPress;
			}
			else
			{
				targetElement.KeyUp -= OnKeyPress;
			}
		}
		base.OnDetaching();
	}

	private static UIElement GetRoot(DependencyObject current)
	{
		UIElement result = null;
		while (current != null)
		{
			result = current as UIElement;
			current = VisualTreeHelper.GetParent(current);
		}
		return result;
	}
}
