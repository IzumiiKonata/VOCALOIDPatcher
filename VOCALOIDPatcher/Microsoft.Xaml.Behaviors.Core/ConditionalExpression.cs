using System.Windows;
using System.Windows.Markup;

namespace Microsoft.Xaml.Behaviors.Core;

[ContentProperty("Conditions")]
public class ConditionalExpression : Freezable, ICondition
{
	public static readonly DependencyProperty ConditionsProperty = DependencyProperty.Register("Conditions", typeof(ConditionCollection), typeof(ConditionalExpression), new PropertyMetadata(null));

	public static readonly DependencyProperty ForwardChainingProperty = DependencyProperty.Register("ForwardChaining", typeof(ForwardChaining), typeof(ConditionalExpression), new PropertyMetadata(ForwardChaining.And));

	public ForwardChaining ForwardChaining
	{
		get
		{
			return (ForwardChaining)GetValue(ForwardChainingProperty);
		}
		set
		{
			SetValue(ForwardChainingProperty, value);
		}
	}

	public ConditionCollection Conditions => (ConditionCollection)GetValue(ConditionsProperty);

	protected override Freezable CreateInstanceCore()
	{
		return new ConditionalExpression();
	}

	public ConditionalExpression()
	{
		SetValue(ConditionsProperty, new ConditionCollection());
	}

	public bool Evaluate()
	{
		bool flag = false;
		foreach (ComparisonCondition condition in Conditions)
		{
			flag = condition.Evaluate();
			if (!flag && ForwardChaining == ForwardChaining.And)
			{
				return flag;
			}
			if (flag && ForwardChaining == ForwardChaining.Or)
			{
				return flag;
			}
		}
		return flag;
	}
}
