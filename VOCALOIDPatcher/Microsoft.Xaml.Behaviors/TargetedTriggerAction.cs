// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace Microsoft.Xaml.Behaviors;

/// <summary>
///     Represents an action that can be targeted to affect an object other than its AssociatedObject.
/// </summary>
/// <typeparam name="T">The type constraint on the target.</typeparam>
/// <remarks>
///     TargetedTriggerAction extends TriggerAction to add knowledge of another element than the one it is attached to.
///     This allows a user to invoke the action on an element other than the one it is attached to in response to a
///     trigger firing. Override OnTargetChanged to hook or unhook handlers on the target element, and
///     OnAttached/OnDetaching
///     for the associated element. The type of the Target element can be constrained by the generic type parameter. If
///     you need control over the type of the AssociatedObject, set a TypeConstraintAttribute on your derived type.
/// </remarks>
public abstract class TargetedTriggerAction<T> : TargetedTriggerAction where T : class
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TargetedTriggerAction&lt;T&gt;" /> class.
    /// </summary>
    protected TargetedTriggerAction()
        : base(typeof(T))
    {
    }

    /// <summary>
    ///     Gets the target object. If TargetName is not set or cannot be resolved, defaults to the AssociatedObject.
    /// </summary>
    /// <value>The target.</value>
    /// <remarks>In general, this property should be used in place of AssociatedObject in derived classes.</remarks>
    protected new T Target => (T)base.Target;

    internal sealed override void OnTargetChangedImpl(object oldTarget, object newTarget)
    {
        base.OnTargetChangedImpl(oldTarget, newTarget);
        OnTargetChanged(oldTarget as T, newTarget as T);
    }

    /// <summary>
    ///     Called when the target property changes.
    /// </summary>
    /// <remarks>Override this to hook and unhook functionality on the specified Target, rather than the AssociatedObject.</remarks>
    /// <param name="oldTarget">The old target.</param>
    /// <param name="newTarget">The new target.</param>
    protected virtual void OnTargetChanged(T oldTarget, T newTarget)
    {
    }
}

/// <summary>
///     Represents an action that can be targeted to affect an object other than its AssociatedObject.
/// </summary>
/// <remarks>
///     This is an infrastructure class. Action authors should derive from TargetedTriggerAction&lt;T&gt; instead of
///     this class.
/// </remarks>
public abstract class TargetedTriggerAction : TriggerAction
{
    public static readonly DependencyProperty TargetObjectProperty = DependencyProperty.Register("TargetObject",
        typeof(object),
        typeof(TargetedTriggerAction),
        new FrameworkPropertyMetadata(
            OnTargetObjectChanged));

    public static readonly DependencyProperty TargetNameProperty = DependencyProperty.Register("TargetName",
        typeof(string),
        typeof(TargetedTriggerAction),
        new FrameworkPropertyMetadata(
            OnTargetNameChanged));

    private readonly Type targetTypeConstraint;

    internal TargetedTriggerAction(Type targetTypeConstraint)
        : base(typeof(DependencyObject))
    {
        this.targetTypeConstraint = targetTypeConstraint;
        TargetResolver = new NameResolver();
        RegisterTargetChanged();
    }

    /// <summary>
    ///     Gets or sets the target object. If TargetObject is not set, the target will look for the object specified by
    ///     TargetName. If an element referred to by TargetName cannot be found, the target will default to the
    ///     AssociatedObject. This is a dependency property.
    /// </summary>
    /// <value>The target object.</value>
    public object TargetObject
    {
        get => GetValue(TargetObjectProperty);
        set => SetValue(TargetObjectProperty, value);
    }

    /// <summary>
    ///     Gets or sets the name of the object this action targets. If Target is set, this property is ignored. If Target is
    ///     not set and TargetName is not set or cannot be resolved, the target will default to the AssociatedObject. This is a
    ///     dependency property.
    /// </summary>
    /// <value>The name of the target object.</value>
    public string TargetName
    {
        get => (string)GetValue(TargetNameProperty);
        set => SetValue(TargetNameProperty, value);
    }

    /// <summary>
    ///     Gets the target object. If TargetObject is set, returns TargetObject. Else, if TargetName is not set or cannot be
    ///     resolved, defaults to the AssociatedObject.
    /// </summary>
    /// <value>The target object.</value>
    /// <remarks>In general, this property should be used in place of AssociatedObject in derived classes.</remarks>
    /// <exception cref="InvalidOperationException">The Target element does not satisfy the type constraint.</exception>
    protected object Target
    {
        get
        {
            object target = AssociatedObject;
            if (TargetObject != null)
                target = TargetObject;
            else if (IsTargetNameSet) target = TargetResolver.Object;

            if (target != null && !TargetTypeConstraint.IsAssignableFrom(target.GetType()))
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    ExceptionStringTable.RetargetedTypeConstraintViolatedExceptionMessage,
                    GetType().Name,
                    target.GetType(),
                    TargetTypeConstraint,
                    "Target"));
            return target;
        }
    }

    /// <summary>
    ///     Gets the associated object type constraint.
    /// </summary>
    /// <value>The associated object type constraint.</value>
    /// <remarks>Define a TypeConstraintAttribute on a derived type to constrain the types it may be attached to.</remarks>
    protected sealed override Type AssociatedObjectTypeConstraint
    {
        get
        {
            var attributes = TypeDescriptor.GetAttributes(GetType());
            var typeConstraintAttribute = attributes[typeof(TypeConstraintAttribute)] as TypeConstraintAttribute;

            if (typeConstraintAttribute != null) return typeConstraintAttribute.Constraint;
            return typeof(DependencyObject);
        }
    }

    /// <summary>
    ///     Gets the target type constraint.
    /// </summary>
    /// <value>The target type constraint.</value>
    protected Type TargetTypeConstraint
    {
        get
        {
            ReadPreamble();
            return targetTypeConstraint;
        }
    }

    private bool IsTargetNameSet => !string.IsNullOrEmpty(TargetName) ||
                                    ReadLocalValue(TargetNameProperty) != DependencyProperty.UnsetValue;

    private NameResolver TargetResolver { get; }

    private bool IsTargetChangedRegistered { get; set; }

    /// <summary>
    ///     Called when the target changes.
    /// </summary>
    /// <param name="oldTarget">The old target.</param>
    /// <param name="newTarget">The new target.</param>
    /// <remarks>
    ///     This function should be overriden in derived classes to hook and unhook functionality from the changing source
    ///     objects.
    /// </remarks>
    internal virtual void OnTargetChangedImpl(object oldTarget, object newTarget)
    {
    }

    /// <summary>
    ///     Called after the action is attached to an AssociatedObject.
    /// </summary>
    protected override void OnAttached()
    {
        base.OnAttached();
        // We can't resolve element names using a Behavior, as it isn't a FrameworkElement.
        // Hence, if we are Hosted on a Behavior, we need to resolve against the Behavior's
        // Host rather than our own. See comment in EventTriggerBase.
        // TODO jekelly 6/20/08: Ideally we could do a namespace walk, but SL doesn't expose
        //						 a way to do this. This solution only looks one level deep. 
        //						 A Behavior with a Behavior attached won't work. This is OK
        //						 for now, but should consider a more general solution if needed.
        var hostObject = AssociatedObject;
        var newBehavior = hostObject as Behavior;

        RegisterTargetChanged();
        if (newBehavior != null)
        {
            hostObject = ((IAttachedObject)newBehavior).AssociatedObject;
            newBehavior.AssociatedObjectChanged += OnBehaviorHostChanged;
        }

        TargetResolver.NameScopeReferenceElement = hostObject as FrameworkElement;
    }

    /// <summary>
    ///     Called when the action is being detached from its AssociatedObject, but before it has actually occurred.
    /// </summary>
    protected override void OnDetaching()
    {
        var oldBehavior = AssociatedObject as Behavior;
        base.OnDetaching();
        OnTargetChangedImpl(TargetResolver.Object, null);
        UnregisterTargetChanged();

        if (oldBehavior != null) oldBehavior.AssociatedObjectChanged -= OnBehaviorHostChanged;
        TargetResolver.NameScopeReferenceElement = null;
    }

    private void OnBehaviorHostChanged(object? sender, EventArgs e)
    {
        TargetResolver.NameScopeReferenceElement = ((IAttachedObject)sender).AssociatedObject as FrameworkElement;
    }

    private void RegisterTargetChanged()
    {
        if (!IsTargetChangedRegistered)
        {
            TargetResolver.ResolvedElementChanged += OnTargetChanged;
            IsTargetChangedRegistered = true;
        }
    }

    private void UnregisterTargetChanged()
    {
        if (IsTargetChangedRegistered)
        {
            TargetResolver.ResolvedElementChanged -= OnTargetChanged;
            IsTargetChangedRegistered = false;
        }
    }

    private static void OnTargetObjectChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var targetedTriggerAction = (TargetedTriggerAction)obj;
        targetedTriggerAction.OnTargetChanged(obj, new NameResolvedEventArgs(args.OldValue, args.NewValue));
    }

    private static void OnTargetNameChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var targetedTriggerAction = (TargetedTriggerAction)obj;
        targetedTriggerAction.TargetResolver.Name = (string)args.NewValue;
    }

    private void OnTargetChanged(object? sender, NameResolvedEventArgs e)
    {
        if (AssociatedObject != null) OnTargetChangedImpl(e.OldObject, e.NewObject);
    }
}
