// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Microsoft.Xaml.Behaviors.Core;

/// <summary>
///     An action that will change a specified property to a specified value when invoked.
/// </summary>
public class ChangePropertyAction : TargetedTriggerAction<object>
{
    public static readonly DependencyProperty PropertyNameProperty =
        DependencyProperty.Register("PropertyName", typeof(string), typeof(ChangePropertyAction), null);

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value", typeof(object), typeof(ChangePropertyAction), null);

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register("Duration", typeof(Duration), typeof(ChangePropertyAction), null);

    public static readonly DependencyProperty IncrementProperty =
        DependencyProperty.Register("Increment", typeof(bool), typeof(ChangePropertyAction), null);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChangePropertyAction" /> class.
    /// </summary>
    public ChangePropertyAction()
    {
    }

    /// <summary>
    ///     Gets or sets the name of the property to change. This is a dependency property.
    /// </summary>
    /// <value>The name of the property to change.</value>
    public string PropertyName
    {
        get => (string)GetValue(PropertyNameProperty);

        set => SetValue(PropertyNameProperty, value);
    }

    /// <summary>
    ///     Gets or sets the value to set. This is a dependency property.
    /// </summary>
    /// <value>The value to set.</value>
    [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
    public object Value
    {
        get => GetValue(ValueProperty);

        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    ///     Gets or sets the duration of the animation that will occur when the ChangePropertyAction is invoked.  This is a
    ///     dependency property.
    ///     If the duration is unset, no animation will be applied.
    /// </summary>
    public Duration Duration
    {
        get => (Duration)GetValue(DurationProperty);

        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    ///     Increment by Value if true; otherwise, set the value directly. If the property cannot be incremented, it will
    ///     instead try to set the value directly.
    /// </summary>
    public bool Increment
    {
        get => (bool)GetValue(IncrementProperty);

        set => SetValue(IncrementProperty, value);
    }

    /// <summary>
    ///     Invokes the action.
    /// </summary>
    /// <param name="parameter">
    ///     The parameter of the action. If the action does not require a parameter, then the parameter may
    ///     be set to a null reference.
    /// </param>
    /// <exception cref="ArgumentException">A property with <c cref="PropertyName" /> could not be found on the Target.</exception>
    /// <exception cref="ArgumentException">
    ///     Could not set <c cref="PropertyName" /> to the value specified by
    ///     <c cref="Value" />.
    /// </exception>
    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    protected override void Invoke(object parameter)
    {
        if (AssociatedObject == null) return;
        if (string.IsNullOrEmpty(PropertyName)) return;
        if (Target == null) return;

        var targetType = Target.GetType();
        var propertyInfo = targetType.GetProperty(PropertyName);
        ValidateProperty(propertyInfo);

        var newValue = Value;
        var converter = TypeConverterHelper.GetTypeConverter(propertyInfo.PropertyType);

        Exception innerException = null;
        try
        {
            if (Value != null)
            {
                if (converter != null && converter.CanConvertFrom(Value.GetType()))
                {
                    newValue = converter.ConvertFrom(null, CultureInfo.InvariantCulture, Value);
                }
                else
                {
                    // Try asking the value if it can convert itself to the target property
                    converter = TypeConverterHelper.GetTypeConverter(Value.GetType());
                    if (converter != null && converter.CanConvertTo(propertyInfo.PropertyType))
                        newValue = converter.ConvertTo(
                            null,
                            CultureInfo.InvariantCulture,
                            Value,
                            propertyInfo.PropertyType);
                }
            }

            // If a duration is set, we should animate this value.
            if (Duration.HasTimeSpan)
            {
                ValidateAnimationPossible(targetType);
                var fromValue = GetCurrentPropertyValue(Target, propertyInfo);
                AnimatePropertyChange(propertyInfo, fromValue, newValue);
            }
            else
            {
                if (Increment) newValue = IncrementCurrentValue(propertyInfo);
                propertyInfo.SetValue(Target, newValue, new object[0]);
            }
        }
        catch (FormatException e)
        {
            innerException = e;
        }
        catch (ArgumentException e)
        {
            innerException = e;
        }
        catch (MethodAccessException e)
        {
            innerException = e;
        }

        if (innerException != null)
            throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    ExceptionStringTable.ChangePropertyActionCannotSetValueExceptionMessage,
                    Value != null ? Value.GetType().Name : "null",
                    PropertyName,
                    propertyInfo.PropertyType.Name),
                innerException);
    }

    private void AnimatePropertyChange(PropertyInfo propertyInfo, object fromValue, object newValue)
    {
        var sb = new Storyboard();
        Timeline timeline;
        if (typeof(double).IsAssignableFrom(propertyInfo.PropertyType))
            timeline = CreateDoubleAnimation((double)fromValue, (double)newValue);
        else if (typeof(Color).IsAssignableFrom(propertyInfo.PropertyType))
            timeline = CreateColorAnimation((Color)fromValue, (Color)newValue);
        else if (typeof(Point).IsAssignableFrom(propertyInfo.PropertyType))
            timeline = CreatePointAnimation((Point)fromValue, (Point)newValue);
        else
            timeline = CreateKeyFrameAnimation(fromValue, newValue);

        timeline.Duration = Duration;
        sb.Children.Add(timeline);

        if (TargetObject == null &&
            TargetName != null &&
            Target is Freezable)
            // Workaround Dev10 bug 542374, Storyboard.Target property does not work properly
            // when the target of the animation is a freezable.
            Storyboard.SetTargetName(sb, TargetName);
        else
            Storyboard.SetTarget(sb, (DependencyObject)Target);
        Storyboard.SetTargetProperty(sb, new PropertyPath(propertyInfo.Name));

        sb.Completed += (o, e) => { propertyInfo.SetValue(Target, newValue, new object[0]); };
        sb.FillBehavior = FillBehavior.Stop;

        // Give the storyboard the neccesary context to resolve target names
        var containingObject = AssociatedObject as FrameworkElement;
        if (containingObject != null)
            sb.Begin(containingObject);
        else
            sb.Begin();
    }

    private static object GetCurrentPropertyValue(object target, PropertyInfo propertyInfo)
    {
        var targetElement = target as FrameworkElement;
        var targetType = target.GetType();
        var fromValue = propertyInfo.GetValue(target, null);

        if (targetElement != null &&
            (propertyInfo.Name == "Width" || propertyInfo.Name == "Height") &&
            double.IsNaN((double)fromValue!))
        {
            if (propertyInfo.Name == "Width")
                fromValue = targetElement.ActualWidth;
            else
                fromValue = targetElement.ActualHeight;
        }

        return fromValue;
    }

    private void ValidateAnimationPossible(Type targetType)
    {
        if (Increment)
            throw new InvalidOperationException(ExceptionStringTable
                .ChangePropertyActionCannotIncrementAnimatedPropertyChangeExceptionMessage);
        if (!typeof(DependencyObject).IsAssignableFrom(targetType))
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                ExceptionStringTable.ChangePropertyActionCannotAnimateTargetTypeExceptionMessage,
                targetType.Name));
    }

    private Timeline CreateKeyFrameAnimation(object newValue, object fromValue)
    {
        var objectAnimation = new ObjectAnimationUsingKeyFrames();
        var k1 = new DiscreteObjectKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0)),
            Value = fromValue
        };
        var k2 = new DiscreteObjectKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(Duration.TimeSpan),
            Value = newValue
        };

        objectAnimation.KeyFrames.Add(k1);
        objectAnimation.KeyFrames.Add(k2);

        return objectAnimation;
    }

    [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic",
        Justification = "Need this.Ease for the Silverlight implementation.")]
    private Timeline CreatePointAnimation(Point fromValue, Point newValue)
    {
        return new PointAnimation
        {
            From = fromValue,
            To = newValue
        };
    }

    [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic",
        Justification = "Need this.Ease for the Silverlight implementation.")]
    private Timeline CreateColorAnimation(Color fromValue, Color newValue)
    {
        return new ColorAnimation
        {
            From = fromValue,
            To = newValue
        };
    }

    [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic",
        Justification = "Need this.Ease for the Silverlight implementation.")]
    private Timeline CreateDoubleAnimation(double fromValue, double newValue)
    {
        return new DoubleAnimation
        {
            From = fromValue,
            To = newValue
        };
    }

    private void ValidateProperty(PropertyInfo propertyInfo)
    {
        if (propertyInfo == null)
            throw new ArgumentException(string.Format(
                CultureInfo.CurrentCulture,
                ExceptionStringTable.ChangePropertyActionCannotFindPropertyNameExceptionMessage,
                PropertyName,
                Target.GetType().Name));

        if (!propertyInfo.CanWrite)
            throw new ArgumentException(string.Format(
                CultureInfo.CurrentCulture,
                ExceptionStringTable.ChangePropertyActionPropertyIsReadOnlyExceptionMessage,
                PropertyName,
                Target.GetType().Name));
    }

    private object IncrementCurrentValue(PropertyInfo propertyInfo)
    {
        if (!propertyInfo.CanRead)
            throw new InvalidOperationException(string.Format(
                CultureInfo.CurrentCulture,
                ExceptionStringTable.ChangePropertyActionCannotIncrementWriteOnlyPropertyExceptionMessage,
                propertyInfo.Name));

        var currentValue = propertyInfo.GetValue(Target, null);
        var returnValue = currentValue;

        var propertyType = propertyInfo.PropertyType;
        var converter = TypeConverterHelper.GetTypeConverter(propertyInfo.PropertyType);
        var value = Value;

        if (value == null || currentValue == null)
            // we can't increment by null, so we'll attempt to set it instead
            // likewise, we can't increment, null by x, so we'll just set value instead
            return value;

        if (converter.CanConvertFrom(value.GetType())) value = TypeConverterHelper.DoConversionFrom(converter, value);

        if (typeof(double).IsAssignableFrom(propertyType))
            returnValue = (double)currentValue + (double)value;
        else if (typeof(int).IsAssignableFrom(propertyType))
            returnValue = (int)currentValue + (int)value;
        else if (typeof(float).IsAssignableFrom(propertyType))
            returnValue = (float)currentValue + (float)value;
        else if (typeof(string).IsAssignableFrom(propertyType))
            returnValue = (string)currentValue + (string)value;
        else
            returnValue = TryAddition(currentValue, value);
        return returnValue;
    }

    private static object TryAddition(object currentValue, object value)
    {
        object returnValue = null;
        var valueType = value.GetType();
        var additiveType = currentValue.GetType();

        MethodInfo uniqueAdditionOperation = null;
        var convertedValue = value;

        foreach (var additionOperation in additiveType.GetMethods())
        {
            if (string.Compare(additionOperation.Name, "op_Addition", StringComparison.Ordinal) != 0) continue;

            var parameters = additionOperation.GetParameters();

            Debug.Assert(parameters.Length == 2, "op_Addition is expected to have 2 parameters");

            var secondParameterType = parameters[1].ParameterType;
            if (!parameters[0].ParameterType.IsAssignableFrom(additiveType)) continue;

            if (!secondParameterType.IsAssignableFrom(valueType))
            {
                var additionConverter = TypeConverterHelper.GetTypeConverter(secondParameterType);
                if (additionConverter.CanConvertFrom(valueType))
                    convertedValue = TypeConverterHelper.DoConversionFrom(additionConverter, value);
                else
                    continue;
            }

            if (uniqueAdditionOperation != null)
                throw new ArgumentException(string.Format(
                    CultureInfo.CurrentCulture,
                    ExceptionStringTable.ChangePropertyActionAmbiguousAdditionOperationExceptionMessage,
                    additiveType.Name));
            uniqueAdditionOperation = additionOperation;
        }

        if (uniqueAdditionOperation != null)
            returnValue = uniqueAdditionOperation.Invoke(null, new[] { currentValue, convertedValue });
        else
            // we couldn't figure out how to add, so pack it up and just set value
            returnValue = value;

        return returnValue;
    }
}
