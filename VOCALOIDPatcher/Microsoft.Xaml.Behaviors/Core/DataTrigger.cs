// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

/// <summary>
///     Represents a trigger that performs actions when the bound data meets a specified condition.
/// </summary>
public class DataTrigger : PropertyChangedTrigger
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(object),
        typeof(DataTrigger), new PropertyMetadata(OnValueChanged));

    public static readonly DependencyProperty ComparisonProperty = DependencyProperty.Register("Comparison",
        typeof(ComparisonConditionType), typeof(DataTrigger), new PropertyMetadata(OnComparisonChanged));

    /// <summary>
    ///     Gets or sets the value to be compared with the property value of the data object. This is a dependency property.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods",
        Justification = "This matches the structure of the WPF DataTrigger.")]
    public object Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    ///     Gets or sets the type of comparison to be performed between the specified values. This is a dependency property.
    /// </summary>
    public ComparisonConditionType Comparison
    {
        get => (ComparisonConditionType)GetValue(ComparisonProperty);
        set => SetValue(ComparisonProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        //fixes issue #11. We want to evaluate the binding's initial value when the element is first loaded
        if (AssociatedObject is FrameworkElement element) element.Loaded += OnElementLoaded;
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
        if (AssociatedObject is FrameworkElement element) element.Loaded -= OnElementLoaded;
    }

    /// <summary>
    ///     Called when the binding property has changed.
    ///     UA_REVIEW:chabiss
    /// </summary>
    /// <param name="args"><see cref="T:System.Windows.DependencyPropertyChangedEventArgs" /> argument.</param>
    protected override void EvaluateBindingChange(object args)
    {
        if (Compare())
            // Fire the actions when the binding data has changed
            InvokeActions(args);
    }

    private static void OnValueChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        var trigger = (DataTrigger)sender;
        trigger.EvaluateBindingChange(args);
    }

    private static void OnComparisonChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        var trigger = (DataTrigger)sender;
        trigger.EvaluateBindingChange(args);
    }

    private bool Compare()
    {
        if (AssociatedObject != null) return ComparisonLogic.EvaluateImpl(Binding, Comparison, Value);
        return false;
    }
}
