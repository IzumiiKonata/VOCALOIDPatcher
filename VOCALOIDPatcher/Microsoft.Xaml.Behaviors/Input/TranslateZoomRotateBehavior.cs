// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors.Layout;

namespace Microsoft.Xaml.Behaviors.Input;

/// <summary>
///     Allows the user to use common touch gestures to translate, zoom, and rotate the attached object.
/// </summary>
public class TranslateZoomRotateBehavior : Behavior<FrameworkElement>
{
    /// <summary>
    ///     Called after the behavior is attached to an AssociatedObject.
    /// </summary>
    /// <remarks>Override this to hook up functionality to the AssociatedObject.</remarks>
    protected override void OnAttached()
    {
        AssociatedObject.AddHandler(UIElement.ManipulationStartingEvent,
            new EventHandler<ManipulationStartingEventArgs>(ManipulationStarting), false /* handledEventsToo */);
        AssociatedObject.AddHandler(UIElement.ManipulationInertiaStartingEvent,
            new EventHandler<ManipulationInertiaStartingEventArgs>(ManipulationInertiaStarting),
            false /* handledEventsToo */);
        AssociatedObject.AddHandler(UIElement.ManipulationDeltaEvent,
            new EventHandler<ManipulationDeltaEventArgs>(ManipulationDelta), false /* handledEventsToo */);
        AssociatedObject.IsManipulationEnabled = true;

        AssociatedObject.AddHandler(UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(MouseLeftButtonDown), false /* handledEventsToo */);
        AssociatedObject.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(MouseLeftButtonUp),
            false /* handledEventsToo */);
    }

    /// <summary>
    ///     Called when the behavior is getting detached from its AssociatedObject, but before it has actually occurred.
    /// </summary>
    /// <remarks>Override this to unhook functionality from the AssociatedObject.</remarks>
    protected override void OnDetaching()
    {
        AssociatedObject.RemoveHandler(UIElement.ManipulationStartingEvent,
            new EventHandler<ManipulationStartingEventArgs>(ManipulationStarting));
        AssociatedObject.RemoveHandler(UIElement.ManipulationInertiaStartingEvent,
            new EventHandler<ManipulationInertiaStartingEventArgs>(ManipulationInertiaStarting));
        AssociatedObject.RemoveHandler(UIElement.ManipulationDeltaEvent,
            new EventHandler<ManipulationDeltaEventArgs>(ManipulationDelta));
        AssociatedObject.IsManipulationEnabled = false;

        AssociatedObject.AddHandler(UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(MouseLeftButtonDown), false /* handledEventsToo */);
        AssociatedObject.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(MouseLeftButtonUp),
            false /* handledEventsToo */);
    }

    #region Fields

    private Transform cachedRenderTransform;

    // used for handling the mouse fallback behavior.
    private bool isDragging;

    // prevent us from trying to update the position when handling a mouse move
    private bool isAdjustingTransform;
    private Point lastMousePoint;

    // used to enforce min and max scale.
    private double lastScaleX = 1.0;
    private double lastScaleY = 1.0;
    private const double HardMinimumScale = 1e-6;

    #endregion

    #region Dependency properties

    public static readonly DependencyProperty SupportedGesturesProperty =
        DependencyProperty.Register("SupportedGestures", typeof(ManipulationModes), typeof(TranslateZoomRotateBehavior),
            new PropertyMetadata(ManipulationModes.All));

    public static readonly DependencyProperty TranslateFrictionProperty =
        DependencyProperty.Register("TranslateFriction", typeof(double), typeof(TranslateZoomRotateBehavior),
            new PropertyMetadata(0.0, frictionChanged, coerceFriction));

    public static readonly DependencyProperty RotationalFrictionProperty =
        DependencyProperty.Register("RotationalFriction", typeof(double), typeof(TranslateZoomRotateBehavior),
            new PropertyMetadata(0.0, frictionChanged, coerceFriction));

    public static readonly DependencyProperty ConstrainToParentBoundsProperty =
        DependencyProperty.Register("ConstrainToParentBounds", typeof(bool), typeof(TranslateZoomRotateBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty MinimumScaleProperty =
        DependencyProperty.Register("MinimumScale", typeof(double), typeof(TranslateZoomRotateBehavior),
            new PropertyMetadata(0.1));

    public static readonly DependencyProperty MaximumScaleProperty =
        DependencyProperty.Register("MaximumScale", typeof(double), typeof(TranslateZoomRotateBehavior),
            new PropertyMetadata(10.0));

    #endregion

    #region Public properties

    /// <summary>
    ///     Gets or sets a value specifying which zooming and translation variants to support.
    /// </summary>
    public ManipulationModes SupportedGestures
    {
        get => (ManipulationModes)GetValue(SupportedGesturesProperty);
        set => SetValue(SupportedGesturesProperty, value);
    }

    /// <summary>
    ///     Gets or sets a number describing the rate at which the translation will decrease.
    /// </summary>
    public double TranslateFriction
    {
        get => (double)GetValue(TranslateFrictionProperty);
        set => SetValue(TranslateFrictionProperty, value);
    }

    /// <summary>
    ///     Gets or sets a number describing the rate at which the rotation will decrease.
    /// </summary>
    public double RotationalFriction
    {
        get => (double)GetValue(RotationalFrictionProperty);
        set => SetValue(RotationalFrictionProperty, value);
    }

    /// <summary>
    ///     Gets or sets the value indicating whether the zoom and translate position of the attached object is limited by the
    ///     bounds of the parent object.
    /// </summary>
    public bool ConstrainToParentBounds
    {
        get => (bool)GetValue(ConstrainToParentBoundsProperty);
        set => SetValue(ConstrainToParentBoundsProperty, value);
    }

    /// <summary>
    ///     Gets or sets a number indicating the minimum zoom value allowed.
    /// </summary>
    public double MinimumScale
    {
        get => (double)GetValue(MinimumScaleProperty);
        set => SetValue(MinimumScaleProperty, value);
    }

    /// <summary>
    ///     Gets or sets a number indicating the maximum zoom value allowed.
    /// </summary>
    public double MaximumScale
    {
        get => (double)GetValue(MaximumScaleProperty);
        set => SetValue(MaximumScaleProperty, value);
    }

    #endregion

    #region PropertyChangedHandlers

    private static void frictionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        // this doesn't have to do anything, but is required to supply a CoerceValueCallback.
    }

    private static object coerceFriction(DependencyObject sender, object value)
    {
        var friction = (double)value;
        return Math.Max(0, Math.Min(1, friction));
    }

    #endregion

    #region Private properties

    private Transform RenderTransform
    {
        get
        {
            if (cachedRenderTransform == null ||
                !ReferenceEquals(cachedRenderTransform, AssociatedObject.RenderTransform))
            {
                var clonedTransform = MouseDragElementBehavior.CloneTransform(AssociatedObject.RenderTransform);
                RenderTransform = clonedTransform;
            }

            return cachedRenderTransform;
        }
        set
        {
            if (cachedRenderTransform != value)
            {
                cachedRenderTransform = value;
                AssociatedObject.RenderTransform = value;
            }
        }
    }

    private Point RenderTransformOriginInElementCoordinates =>
        new(AssociatedObject.RenderTransformOrigin.X * AssociatedObject.ActualWidth,
            AssociatedObject.RenderTransformOrigin.Y * AssociatedObject.ActualHeight);

    // This needs to take the render transform origin into account to get the proper transform value.
    private Matrix FullTransformValue
    {
        get
        {
            var center = RenderTransformOriginInElementCoordinates;
            var matrix = RenderTransform.Value;
            matrix.TranslatePrepend(-center.X, -center.Y);
            matrix.Translate(center.X, center.Y);
            return matrix;
        }
    }

    private MatrixTransform MatrixTransform
    {
        get
        {
            EnsureTransform();
            return (MatrixTransform)RenderTransform;
        }
    }

    private FrameworkElement ParentElement => AssociatedObject.Parent as FrameworkElement;

    #endregion

    #region Private methods

    // This behavior always enforces a matrix transform.
    internal void EnsureTransform()
    {
        var transform = RenderTransform as MatrixTransform;
        if (transform == null || transform.IsFrozen)
        {
            if (RenderTransform != null)
                transform = new MatrixTransform(FullTransformValue);
            else
                // can't use MatrixTransform.Identity because it is frozen.
                transform = new MatrixTransform(Matrix.Identity);
            RenderTransform = transform;
        }

        // The touch manipulation deltas need to be applied relative to the element's actual center.  
        // Keeping a render transform origin in place will cause the transform to be applied incorrectly, so we clear it.
        AssociatedObject.RenderTransformOrigin = new Point(0, 0);
    }

    internal void ApplyRotationTransform(double angle, Point rotationPoint)
    {
        // Need to use a temporary and set MatrixTransform.Matrix.
        // Modifying the matrix property directly will only affect a local copy, since Matrix is a value type.  
        var matrix = MatrixTransform.Matrix;
        matrix.RotateAt(angle, rotationPoint.X, rotationPoint.Y);
        MatrixTransform.Matrix = matrix;
    }

    internal void ApplyScaleTransform(double scaleX, double scaleY, Point scalePoint)
    {
        // lastScale will not go below HardMinimumScale due to the checks below.  Thus, we can safely divide it to constrain the scale delta.
        // scale is the incremental scale, while lastScale is the current accumulated scale in the transform.  We want to constrain the incremental scale
        // so that the accumulated scale doesn't exceed min or max scale.  To prevent collapsing to a zero scale, we'll enforce a positive hard minimum scale.
        var newScaleX = scaleX * lastScaleX;
        newScaleX = Math.Min(Math.Max(Math.Max(HardMinimumScale, MinimumScale), newScaleX), MaximumScale);
        scaleX = newScaleX / lastScaleX;
        lastScaleX = scaleX * lastScaleX;

        var newScaleY = scaleY * lastScaleY;
        newScaleY = Math.Min(Math.Max(Math.Max(HardMinimumScale, MinimumScale), newScaleY), MaximumScale);
        scaleY = newScaleY / lastScaleY;
        lastScaleY = scaleY * lastScaleY;

        // Need to use a temporary and set MatrixTransform.Matrix.
        // Modifying the matrix property directly will only affect a local copy, since Matrix is a value type.  
        var matrix = MatrixTransform.Matrix;
        matrix.ScaleAt(scaleX, scaleY, scalePoint.X, scalePoint.Y);
        MatrixTransform.Matrix = matrix;
    }

    internal void ApplyTranslateTransform(double x, double y)
    {
        // Need to use a temporary and set MatrixTransform.Matrix.
        // Modifying the matrix property directly will only affect a local copy, since Matrix is a value type.
        var matrix = MatrixTransform.Matrix;
        matrix.Translate(x, y);
        MatrixTransform.Matrix = matrix;
    }

    private void ManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        var manipulationContainer = ParentElement;
        // If the parent relationship goes through a popup(e.g. ComboBox/ComboBoxItem), then we need to use the element itself as the manipulation container, otherwise we'll crash(Expression 105258).
        if (manipulationContainer == null || !manipulationContainer.IsAncestorOf(AssociatedObject))
            manipulationContainer = AssociatedObject;
        e.ManipulationContainer = manipulationContainer;
        e.Mode = SupportedGestures;
        e.Handled = true;
    }

    private void ManipulationInertiaStarting(object? sender, ManipulationInertiaStartingEventArgs e)
    {
        // deceleration is pixels per ms^2
        // the translate factor is in the range of [0,1], with 0 being no deceleration, and 1 being instant deceleration.
        // We use log because the curve has good characteristics over the input range.
        var translateFactor = TranslateFriction == 1 ? 1.0 : -.00666 * Math.Log(1 - TranslateFriction);
        var translateDeceleration = e.InitialVelocities.LinearVelocity.Length * translateFactor;

        e.TranslationBehavior = new InertiaTranslationBehavior
        {
            InitialVelocity = e.InitialVelocities.LinearVelocity,
            DesiredDeceleration = Math.Max(translateDeceleration, 0)
        };

        var rotateFactor = RotationalFriction == 1 ? 1.0 : -.00666 * Math.Log(1 - RotationalFriction);
        var rotateDeceleration = Math.Abs(e.InitialVelocities.AngularVelocity) * rotateFactor;

        e.RotationBehavior = new InertiaRotationBehavior
        {
            InitialVelocity = e.InitialVelocities.AngularVelocity,
            DesiredDeceleration = Math.Max(rotateDeceleration, 0)
        };

        e.Handled = true;
    }

    // This handles the manipulation data from the touch events.  Currently it assumes the zoom and rotation is applied through the center of the element.
    private void ManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        EnsureTransform();
        var currentDelta = e.DeltaManipulation;

        // Always use the element's center point as the origin of the manipulation deltas.
        var origin = new Point(AssociatedObject.ActualWidth / 2, AssociatedObject.ActualHeight / 2);

        // Compute the manipulation center in element space.
        var center = FullTransformValue.Transform(origin);

        ApplyScaleTransform(currentDelta.Scale.X, currentDelta.Scale.Y, center);
        ApplyRotationTransform(currentDelta.Rotation, center);
        ApplyTranslateTransform(currentDelta.Translation.X, currentDelta.Translation.Y);

        var container = (FrameworkElement)e.ManipulationContainer;
        // If constraining to bounds, and the element leaves its parent bounds, then stop the inertia.
        var parentBounds = new Rect(container.RenderSize);

        var childBounds = AssociatedObject.TransformToVisual(container)
            .TransformBounds(new Rect(AssociatedObject.RenderSize));

        if (e.IsInertial && ConstrainToParentBounds && !parentBounds.Contains(childBounds)) e.Complete();

        e.Handled = true;
    }

    // Mouse fallback for panning is implemented by tracking the mouse movement while the mouse is down.
    private void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        AssociatedObject.CaptureMouse();
        AssociatedObject.MouseMove += AssociatedObject_MouseMove;
        AssociatedObject.LostMouseCapture += AssociatedObject_LostMouseCapture;
        e.Handled = true;
        lastMousePoint = e.GetPosition(AssociatedObject);
        isDragging = true;
    }

    // ends the mouse fallback for panning
    private void MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        AssociatedObject.ReleaseMouseCapture();
        e.Handled = true;
    }

    // capture can be lost by e.g. changing windows.
    private void AssociatedObject_LostMouseCapture(object sender, MouseEventArgs e)
    {
        isDragging = false;
        AssociatedObject.MouseMove -= AssociatedObject_MouseMove;
        AssociatedObject.LostMouseCapture -= AssociatedObject_LostMouseCapture;
    }

    // handle a mouse move by updating the object transform.
    private void AssociatedObject_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging && !isAdjustingTransform)
        {
            isAdjustingTransform = true;
            var newPoint = e.GetPosition(AssociatedObject);
            var delta = newPoint - lastMousePoint;
            if ((SupportedGestures & ManipulationModes.TranslateX) == 0) delta.X = 0;
            if ((SupportedGestures & ManipulationModes.TranslateY) == 0) delta.Y = 0;

            // Transform mouse movement into element space, taking the element's transform into account.
            var transformedDelta = FullTransformValue.Transform(delta);
            ApplyTranslateTransform(transformedDelta.X, transformedDelta.Y);
            // Need to get the position again, as it probably changed when updating the transform.
            lastMousePoint = e.GetPosition(AssociatedObject);
            isAdjustingTransform = false;
        }
    }

    #endregion
}
