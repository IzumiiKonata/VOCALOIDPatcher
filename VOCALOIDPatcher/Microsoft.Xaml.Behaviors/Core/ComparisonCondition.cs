// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

/// <summary>
///     Represents one ternary condition.
/// </summary>
public class ComparisonCondition : Freezable
{
    public static readonly DependencyProperty LeftOperandProperty = DependencyProperty.Register("LeftOperand",
        typeof(object), typeof(ComparisonCondition), new PropertyMetadata(null));

    public static readonly DependencyProperty OperatorProperty = DependencyProperty.Register("Operator",
        typeof(ComparisonConditionType), typeof(ComparisonCondition),
        new PropertyMetadata(ComparisonConditionType.Equal));

    public static readonly DependencyProperty RightOperandProperty = DependencyProperty.Register("RightOperand",
        typeof(object), typeof(ComparisonCondition), new PropertyMetadata(null));

    /// <summary>
    ///     Gets or sets the left operand.
    /// </summary>
    public object LeftOperand
    {
        get => GetValue(LeftOperandProperty);
        set => SetValue(LeftOperandProperty, value);
    }

    /// <summary>
    ///     Gets or sets the right operand.
    /// </summary>
    public object RightOperand
    {
        get => GetValue(RightOperandProperty);
        set => SetValue(RightOperandProperty, value);
    }

    /// <summary>
    ///     Gets or sets the comparison operator.
    /// </summary>
    public ComparisonConditionType Operator
    {
        get => (ComparisonConditionType)GetValue(OperatorProperty);
        set => SetValue(OperatorProperty, value);
    }

    #region Freezable

    protected override Freezable CreateInstanceCore()
    {
        return new ComparisonCondition();
    }

    #endregion

    /// <summary>
    ///     Method that evaluates the condition. Note that this method can throw ArgumentException if the operator is
    ///     incompatible with the type. For instance, operators LessThan, LessThanOrEqual, GreaterThan, and GreaterThanOrEqual
    ///     require both operators to implement IComparable.
    /// </summary>
    /// <returns>Returns true if the condition has been met; otherwise, returns false.</returns>
    public bool Evaluate()
    {
        EnsureBindingUpToDate();
        return ComparisonLogic.EvaluateImpl(LeftOperand, Operator, RightOperand);
    }

    /// <summary>
    ///     Ensure that any binding on DP operands are up-to-date.
    /// </summary>
    private void EnsureBindingUpToDate()
    {
        DataBindingHelper.EnsureBindingUpToDate(this, LeftOperandProperty);
        DataBindingHelper.EnsureBindingUpToDate(this, OperatorProperty);
        DataBindingHelper.EnsureBindingUpToDate(this, RightOperandProperty);
    }
}
