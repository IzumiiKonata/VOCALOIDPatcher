using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

public class DataTrigger : PropertyChangedTrigger
{
	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(object), typeof(DataTrigger), new PropertyMetadata(OnValueChanged));

	public static readonly DependencyProperty ComparisonProperty = DependencyProperty.Register("Comparison", typeof(ComparisonConditionType), typeof(DataTrigger), new PropertyMetadata(OnComparisonChanged));

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

	public ComparisonConditionType Comparison
	{
		get
		{
			return (ComparisonConditionType)GetValue(ComparisonProperty);
		}
		set
		{
			SetValue(ComparisonProperty, value);
		}
	}

	protected override void OnAttached()
	{
		base.OnAttached();
		if (base.AssociatedObject is FrameworkElement frameworkElement)
		{
			frameworkElement.Loaded += OnElementLoaded;
		}
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();
		UnsubscribeElementLoadedEvent();
	}

	private void OnElementLoaded(object sender, RoutedEventArgs e)
	{
		try
		{
			EvaluateBindingChange(e);
		}
		finally
		{
			UnsubscribeElementLoadedEvent();
		}
	}

	private void UnsubscribeElementLoadedEvent()
	{
		if (base.AssociatedObject is FrameworkElement frameworkElement)
		{
			frameworkElement.Loaded -= OnElementLoaded;
		}
	}

	protected override void EvaluateBindingChange(object args)
	{
		if (Compare())
		{
			InvokeActions(args);
		}
	}

	private static void OnValueChanged(object sender, DependencyPropertyChangedEventArgs args)
	{
		((DataTrigger)sender).EvaluateBindingChange(args);
	}

	private static void OnComparisonChanged(object sender, DependencyPropertyChangedEventArgs args)
	{
		((DataTrigger)sender).EvaluateBindingChange(args);
	}

	private bool Compare()
	{
		if (base.AssociatedObject != null)
		{
			return ComparisonLogic.EvaluateImpl(base.Binding, Comparison, Value);
		}
		return false;
	}
}
