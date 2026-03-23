using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

public class PropertyChangedTrigger : TriggerBase<DependencyObject>
{
	public static readonly DependencyProperty BindingProperty = DependencyProperty.Register("Binding", typeof(object), typeof(PropertyChangedTrigger), new PropertyMetadata(OnBindingChanged));

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

	protected virtual void EvaluateBindingChange(object args)
	{
		InvokeActions(args);
	}

	protected override void OnAttached()
	{
		base.OnAttached();
		base.PreviewInvoke += OnPreviewInvoke;
	}

	protected override void OnDetaching()
	{
		base.PreviewInvoke -= OnPreviewInvoke;
		OnDetaching();
	}

	private void OnPreviewInvoke(object sender, PreviewInvokeEventArgs e)
	{
		DataBindingHelper.EnsureDataBindingOnActionsUpToDate(this);
	}

	private static void OnBindingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
	{
		((PropertyChangedTrigger)sender).EvaluateBindingChange(args);
	}
}
