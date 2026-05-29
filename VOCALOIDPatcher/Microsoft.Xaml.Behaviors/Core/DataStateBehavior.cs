// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

/// <summary>
///     Toggles between two states based on a conditional statement.
/// </summary>
public class DataStateBehavior : Behavior<FrameworkElement>
{
    public static readonly DependencyProperty BindingProperty = DependencyProperty.Register("Binding", typeof(object),
        typeof(DataStateBehavior), new PropertyMetadata(OnBindingChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(object),
        typeof(DataStateBehavior), new PropertyMetadata(OnValueChanged));

    public static readonly DependencyProperty TrueStateProperty = DependencyProperty.Register("TrueState",
        typeof(string), typeof(DataStateBehavior), new PropertyMetadata(OnTrueStateChanged));

    public static readonly DependencyProperty FalseStateProperty = DependencyProperty.Register("FalseState",
        typeof(string), typeof(DataStateBehavior), new PropertyMetadata(OnFalseStateChanged));

    /// <summary>
    ///     Gets or sets the binding that produces the property value of the data object. This is a dependency property.
    /// </summary>
    public object Binding
    {
        get => GetValue(BindingProperty);
        set => SetValue(BindingProperty, value);
    }

    /// <summary>
    ///     Gets or sets the value to be compared with the property value of the data object. This is a dependency property.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods",
        Justification = "This matches the structure of DataTrigger, which this is patterned after.")]
    public object Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    ///     Gets or sets the name of the visual state to transition to when the condition is met. This is a dependency
    ///     property.
    /// </summary>
    public string TrueState
    {
        get => (string)GetValue(TrueStateProperty);
        set => SetValue(TrueStateProperty, value);
    }

    /// <summary>
    ///     Gets or sets the name of the visual state to transition to when the condition is not met. This is a dependency
    ///     property.
    /// </summary>
    public string FalseState
    {
        get => (string)GetValue(FalseStateProperty);
        set => SetValue(FalseStateProperty, value);
    }

    private FrameworkElement TargetObject => VisualStateUtilities.FindNearestStatefulControl(AssociatedObject);

    private IEnumerable<VisualState> TargetedVisualStates
    {
        get
        {
            var states = new List<VisualState>();
            if (TargetObject != null)
            {
                var groups = VisualStateUtilities.GetVisualStateGroups(TargetObject);
                foreach (VisualStateGroup group in groups)
                foreach (VisualState state in group.States)
                    states.Add(state);
            }

            return states;
        }
    }

    /// <summary>
    ///     Called after the behavior is attached to an AssociatedObject.
    /// </summary>
    /// <remarks>Override this to hook up functionality to the AssociatedObject.</remarks>
    protected override void OnAttached()
    {
        base.OnAttached();
        ValidateStateNamesDeferred();
    }

    private void ValidateStateNamesDeferred()
    {
        var parentElement = AssociatedObject.Parent as FrameworkElement;

        if (parentElement != null && IsElementLoaded(parentElement))
            ValidateStateNames();
        else
            AssociatedObject.Loaded += (o, e) => { ValidateStateNames(); };
    }

    // todo jekelly: this is duplicated from Interaction.IsElementLoaded, find some way to share them
    /// <summary>
    ///     A helper function to take the place of FrameworkElement.IsLoaded, as this property isn't available in Silverlight.
    /// </summary>
    /// <param name="element">The element of interest.</param>
    /// <returns>Returns true if the element has been loaded; otherwise, returns false.</returns>
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
        if (AssociatedObject != null)
            if (!string.IsNullOrEmpty(stateName))
            {
                foreach (var state in TargetedVisualStates)
                    if (stateName == state.Name)
                        return;

                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    ExceptionStringTable.DataStateBehaviorStateNameNotFoundExceptionMessage,
                    stateName,
                    TargetObject != null ? TargetObject.GetType().Name : "null"));
            }
    }

    private static void OnBindingChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var dataStateBehavior = (DataStateBehavior)obj;
        dataStateBehavior.Evaluate();
    }

    private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var dataStateBehavior = (DataStateBehavior)obj;
        dataStateBehavior.Evaluate();
    }

    private static void OnTrueStateChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var dataStateBehavior = (DataStateBehavior)obj;
        dataStateBehavior.ValidateStateName(dataStateBehavior.TrueState);
        dataStateBehavior.Evaluate();
    }

    private static void OnFalseStateChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var dataStateBehavior = (DataStateBehavior)obj;
        dataStateBehavior.ValidateStateName(dataStateBehavior.FalseState);
        dataStateBehavior.Evaluate();
    }

    private void Evaluate()
    {
        if (TargetObject != null)
        {
            string stateName = null;
            if (ComparisonLogic.EvaluateImpl(Binding, ComparisonConditionType.Equal, Value))
                stateName = TrueState;
            else
                stateName = FalseState;

            VisualStateUtilities.GoToState(TargetObject, stateName, true);
        }
    }
}
