using System.Windows;
using System.Windows.Markup;

namespace Microsoft.Xaml.Behaviors.Core;

[ContentProperty("Condition")]
public class ConditionBehavior : Behavior<TriggerBase>
{
	public static readonly DependencyProperty ConditionProperty = DependencyProperty.Register("Condition", typeof(ICondition), typeof(ConditionBehavior), new PropertyMetadata(null));

	public ICondition Condition
	{
		get
		{
			return (ICondition)GetValue(ConditionProperty);
		}
		set
		{
			SetValue(ConditionProperty, value);
		}
	}

	protected override void OnAttached()
	{
		base.OnAttached();
		base.AssociatedObject.PreviewInvoke += OnPreviewInvoke;
	}

	protected override void OnDetaching()
	{
		base.AssociatedObject.PreviewInvoke -= OnPreviewInvoke;
		base.OnDetaching();
	}

	private void OnPreviewInvoke(object sender, PreviewInvokeEventArgs e)
	{
		if (Condition != null)
		{
			e.Cancelling = !Condition.Evaluate();
		}
	}
}
