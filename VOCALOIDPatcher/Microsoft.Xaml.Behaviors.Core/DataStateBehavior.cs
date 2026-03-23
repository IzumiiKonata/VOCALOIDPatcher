using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

public class DataStateBehavior : Behavior<FrameworkElement>
{
	public static readonly DependencyProperty BindingProperty = DependencyProperty.Register("Binding", typeof(object), typeof(DataStateBehavior), new PropertyMetadata(OnBindingChanged));

	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(object), typeof(DataStateBehavior), new PropertyMetadata(OnValueChanged));

	public static readonly DependencyProperty TrueStateProperty = DependencyProperty.Register("TrueState", typeof(string), typeof(DataStateBehavior), new PropertyMetadata(OnTrueStateChanged));

	public static readonly DependencyProperty FalseStateProperty = DependencyProperty.Register("FalseState", typeof(string), typeof(DataStateBehavior), new PropertyMetadata(OnFalseStateChanged));

	public object Binding
	{
		get
		{
			return GetValue(BindingProperty);
		}
		set
		{
			SetValue(BindingProperty, value);
		}
	}

	public object Value
	{
		get
		{
			return GetValue(ValueProperty);
		}
		set
		{
			SetValue(ValueProperty, value);
		}
	}

	public string TrueState
	{
		get
		{
			return (string)GetValue(TrueStateProperty);
		}
		set
		{
			SetValue(TrueStateProperty, value);
		}
	}

	public string FalseState
	{
		get
		{
			return (string)GetValue(FalseStateProperty);
		}
		set
		{
			SetValue(FalseStateProperty, value);
		}
	}

	private FrameworkElement TargetObject => VisualStateUtilities.FindNearestStatefulControl(base.AssociatedObject);

	private IEnumerable<VisualState> TargetedVisualStates
	{
		get
		{
			List<VisualState> list = new List<VisualState>();
			if (TargetObject != null)
			{
				foreach (VisualStateGroup visualStateGroup in VisualStateUtilities.GetVisualStateGroups(TargetObject))
				{
					foreach (VisualState state in visualStateGroup.States)
					{
						list.Add(state);
					}
				}
			}
			return list;
		}
	}

	protected override void OnAttached()
	{
		base.OnAttached();
		ValidateStateNamesDeferred();
	}

	private void ValidateStateNamesDeferred()
	{
		if (base.AssociatedObject.Parent is FrameworkElement element && IsElementLoaded(element))
		{
			ValidateStateNames();
			return;
		}
		base.AssociatedObject.Loaded += delegate
		{
			ValidateStateNames();
		};
	}

	internal static bool IsElementLoaded(FrameworkElement element)
	{
		return element.IsLoaded;
	}

	private void ValidateStateNames()
	{
		ValidateStateName(TrueState);
		ValidateStateName(FalseState);
	}

	private void ValidateStateName(string stateName)
	{
		if (base.AssociatedObject == null || string.IsNullOrEmpty(stateName))
		{
			return;
		}
		foreach (VisualState targetedVisualState in TargetedVisualStates)
		{
			if (stateName == targetedVisualState.Name)
			{
				return;
			}
		}
		throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, ExceptionStringTable.DataStateBehaviorStateNameNotFoundExceptionMessage, stateName, (TargetObject != null) ? TargetObject.GetType().Name : "null"));
	}

	private static void OnBindingChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
	{
		((DataStateBehavior)obj).Evaluate();
	}

	private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
	{
		((DataStateBehavior)obj).Evaluate();
	}

	private static void OnTrueStateChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
	{
		DataStateBehavior obj2 = (DataStateBehavior)obj;
		obj2.ValidateStateName(obj2.TrueState);
		obj2.Evaluate();
	}

	private static void OnFalseStateChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
	{
		DataStateBehavior obj2 = (DataStateBehavior)obj;
		obj2.ValidateStateName(obj2.FalseState);
		obj2.Evaluate();
	}

	private void Evaluate()
	{
		if (TargetObject != null)
		{
			string text = null;
			VisualStateUtilities.GoToState(stateName: (!ComparisonLogic.EvaluateImpl(Binding, ComparisonConditionType.Equal, Value)) ? FalseState : TrueState, element: TargetObject, useTransitions: true);
		}
	}
}
