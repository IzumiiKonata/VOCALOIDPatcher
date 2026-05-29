// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors.Core;

namespace Microsoft.Xaml.Behaviors.Layout;

/// <summary>
///     Repositions the attached element in response to mouse drag gestures on the element.
/// </summary>
public class MouseDragElementBehavior : Behavior<FrameworkElement>
{
    /// <summary>
    ///     Called after the behavior is attached to an AssociatedObject.
    /// </summary>
    /// <remarks>Override this to hook up functionality to the AssociatedObject.</remarks>
    protected override void OnAttached()
    {
        AssociatedObject.AddHandler(UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnMouseLeftButtonDown), false /* handledEventsToo */);
    }

    /// <summary>
    ///     Called when the behavior is getting detached from its AssociatedObject, but before it has actually occurred.
    /// </summary>
    /// <remarks>Override this to unhook functionality from the AssociatedObject.</remarks>
    protected override void OnDetaching()
    {
        AssociatedObject.RemoveHandler(UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnMouseLeftButtonDown));
    }

    #region Fields

    private bool settingPosition;
    private Point relativePosition;
    private Transform cachedRenderTransform;

    #endregion

    #region Events

    /// <summary>
    ///     Occurs when a drag gesture is initiated.
    /// </summary>
    public event MouseEventHandler DragBegun;

    /// <summary>
    ///     Occurs when a drag gesture update is processed.
    /// </summary>
    public event MouseEventHandler Dragging;

    /// <summary>
    ///     Occurs when a drag gesture is finished.
    /// </summary>
    public event MouseEventHandler DragFinished;

    #endregion

    #region Dependency properties

    /// <summary>
    ///     Dependency property for the X position of the dragged element, relative to the left of the root element.
    /// </summary>
    public static readonly DependencyProperty XProperty = DependencyProperty.Register("X", typeof(double),
        typeof(MouseDragElementBehavior), new PropertyMetadata(double.NaN, OnXChanged));

    /// <summary>
    ///     Dependency property for the Y position of the dragged element, relative to the top of the root element.
    /// </summary>
    public static readonly DependencyProperty YProperty = DependencyProperty.Register("Y", typeof(double),
        typeof(MouseDragElementBehavior), new PropertyMetadata(double.NaN, OnYChanged));

    /// <summary>
    ///     Dependency property for the ConstrainToParentBounds property. If true, the dragged element will be constrained to
    ///     stay within the bounds of its parent container.
    /// </summary>
    public static readonly DependencyProperty ConstrainToParentBoundsProperty =
        DependencyProperty.Register("ConstrainToParentBounds", typeof(bool), typeof(MouseDragElementBehavior),
            new PropertyMetadata(false, OnConstrainToParentBoundsChanged));

    #endregion

    #region Public properties

    /// <summary>
    ///     Gets or sets the X position of the dragged element, relative to the left of the root element. This is a dependency
    ///     property.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "X",
        Justification = "X is the correct property name.")]
    public double X
    {
        get => (double)GetValue(XProperty);
        set => SetValue(XProperty, value);
    }

    /// <summary>
    ///     Gets or sets the Y position of the dragged element, relative to the top of the root element. This is a dependency
    ///     property.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Y",
        Justification = "Y is the correct property name.")]
    public double Y
    {
        get => (double)GetValue(YProperty);
        set => SetValue(YProperty, value);
    }

    /// <summary>
    ///     Gets or sets a value indicating whether the dragged element is constrained to stay within the bounds of its parent
    ///     container. This is a dependency property.
    /// </summary>
    /// <value>
    ///     <c>True</c> if the dragged element should be constrained to its parents bounds; otherwise, <c>False</c>.
    /// </value>
    public bool ConstrainToParentBounds
    {
        get => (bool)GetValue(ConstrainToParentBoundsProperty);
        set => SetValue(ConstrainToParentBoundsProperty, value);
    }

    #endregion

    #region PropertyChangedHandlers

    private static void OnXChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        var dragBehavior = (MouseDragElementBehavior)sender;
        dragBehavior.UpdatePosition(new Point((double)args.NewValue, dragBehavior.Y));
    }

    private static void OnYChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        var dragBehavior = (MouseDragElementBehavior)sender;
        dragBehavior.UpdatePosition(new Point(dragBehavior.X, (double)args.NewValue));
    }

    private static void OnConstrainToParentBoundsChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        var b = (MouseDragElementBehavior)sender;
        b.UpdatePosition(new Point(b.X, b.Y));
    }

    #endregion

    #region Private properties

    /// <summary>
    ///     Gets the on-screen position of the associated element in root coordinates.
    /// </summary>
    /// <value>The on-screen position of the associated element in root coordinates.</value>
    private Point ActualPosition
    {
        get
        {
            var elementToRoot = AssociatedObject.TransformToVisual(RootElement);
            var translation = GetTransformOffset(elementToRoot);
            return new Point(translation.X, translation.Y);
        }
    }


    /// <summary>
    ///     Gets the element bounds in element coordinates.
    /// </summary>
    /// <value>The element bounds in element coordinates.</value>
    private Rect ElementBounds
    {
        get
        {
            var layoutRect = ExtendedVisualStateManager.GetLayoutRect(AssociatedObject);
            return new Rect(new Point(0, 0), new Size(layoutRect.Width, layoutRect.Height));
        }
    }

    /// <summary>
    ///     Gets the parent element of the associated object.
    /// </summary>
    /// <value>The parent element of the associated object.</value>
    private FrameworkElement ParentElement => AssociatedObject.Parent as FrameworkElement;

    /// <summary>
    ///     Gets the root element of the scene in which the associated object is located.
    /// </summary>
    /// <value>The root element of the scene in which the associated object is located.</value>
    private UIElement RootElement
    {
        get
        {
            DependencyObject child = AssociatedObject;
            var parent = child;
            while (parent != null)
            {
                child = parent;
                parent = VisualTreeHelper.GetParent(child);
            }

            return child as UIElement;
        }
    }

    /// <summary>
    ///     Gets and sets the RenderTransform of the associated element.
    /// </summary>
    private Transform RenderTransform
    {
        get
        {
            if (cachedRenderTransform == null ||
                !ReferenceEquals(cachedRenderTransform, AssociatedObject.RenderTransform))
            {
                var clonedTransform = CloneTransform(AssociatedObject.RenderTransform);
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

    #endregion

    #region Private methods

    /// <summary>
    ///     Attempts to update the position of the associated element to the specified coordinates.
    /// </summary>
    /// <param name="point">The desired position of the element in root coordinates.</param>
    private void UpdatePosition(Point point)
    {
        if (!settingPosition && AssociatedObject != null)
        {
            var elementToRoot = AssociatedObject.TransformToVisual(RootElement);
            var translation = GetTransformOffset(elementToRoot);
            var xChange = double.IsNaN(point.X) ? 0 : point.X - translation.X;
            var yChange = double.IsNaN(point.Y) ? 0 : point.Y - translation.Y;
            ApplyTranslation(xChange, yChange);
        }
    }

    /// <summary>
    ///     Applies a relative position translation to the associated element.
    /// </summary>
    /// <param name="x">The X component of the desired translation in root coordinates.</param>
    /// <param name="y">The Y component of the desired translation in root coordinates.</param>
    private void ApplyTranslation(double x, double y)
    {
        if (ParentElement != null)
        {
            var rootToParent = RootElement.TransformToVisual(ParentElement);
            var transformedPoint = TransformAsVector(rootToParent, x, y);
            x = transformedPoint.X;
            y = transformedPoint.Y;

            if (ConstrainToParentBounds)
            {
                var parentElement = ParentElement;
                var parentBounds = new Rect(0, 0, parentElement.ActualWidth, parentElement.ActualHeight);

                var objectToParent = AssociatedObject.TransformToVisual(parentElement);
                var objectBoundingBox = ElementBounds;
                objectBoundingBox = objectToParent.TransformBounds(objectBoundingBox);

                var endPosition = objectBoundingBox;
                endPosition.X += x;
                endPosition.Y += y;

                if (!RectContainsRect(parentBounds, endPosition))
                {
                    if (endPosition.X < parentBounds.Left)
                    {
                        var diff = endPosition.X - parentBounds.Left;
                        x -= diff;
                    }
                    else if (endPosition.Right > parentBounds.Right)
                    {
                        var diff = endPosition.Right - parentBounds.Right;
                        x -= diff;
                    }

                    if (endPosition.Y < parentBounds.Top)
                    {
                        var diff = endPosition.Y - parentBounds.Top;
                        y -= diff;
                    }
                    else if (endPosition.Bottom > parentBounds.Bottom)
                    {
                        var diff = endPosition.Bottom - parentBounds.Bottom;
                        y -= diff;
                    }
                }
            }

            ApplyTranslationTransform(x, y);
        }
    }

    /// <summary>
    ///     Applies the given translation to the RenderTransform of the associated element.
    /// </summary>
    /// <param name="x">The X component of the translation in parent coordinates.</param>
    /// <param name="y">The Y component of the translation in parent coordinates.</param>
    internal void ApplyTranslationTransform(double x, double y)
    {
        var renderTransform = RenderTransform;
        // todo jekelly: what if its frozen?
        var translateTransform = renderTransform as TranslateTransform;

        if (translateTransform == null)
        {
            var renderTransformGroup = renderTransform as TransformGroup;
            var renderMatrixTransform = renderTransform as MatrixTransform;
            if (renderTransformGroup != null)
            {
                if (renderTransformGroup.Children.Count > 0)
                    translateTransform =
                        renderTransformGroup.Children[renderTransformGroup.Children.Count - 1] as TranslateTransform;
                if (translateTransform == null)
                {
                    translateTransform = new TranslateTransform();
                    renderTransformGroup.Children.Add(translateTransform);
                }
            }
            else if (renderMatrixTransform != null)
            {
                var matrix = renderMatrixTransform.Matrix;
                matrix.OffsetX += x;
                matrix.OffsetY += y;
                var matrixTransform = new MatrixTransform();
                matrixTransform.Matrix = matrix;
                RenderTransform = matrixTransform;
                return;
            }
            else
            {
                var transformGroup = new TransformGroup();
                translateTransform = new TranslateTransform();
                // this will break multi-step animations that target the render transform
                if (renderTransform != null) transformGroup.Children.Add(renderTransform);
                transformGroup.Children.Add(translateTransform);
                RenderTransform = transformGroup;
            }
        }

        Debug.Assert(translateTransform != null, "TranslateTransform should not be null by this point.");
        translateTransform.X += x;
        translateTransform.Y += y;
    }

    /// <summary>
    ///     Does a recursive deep copy of the specified transform.
    /// </summary>
    /// <param name="transform">The transform to clone.</param>
    /// <returns>A deep copy of the specified transform, or null if the specified transform is null.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the type of the Transform is not recognized.</exception>
    internal static Transform CloneTransform(Transform transform)
    {
        ScaleTransform scaleTransform = null;
        RotateTransform rotateTransform = null;
        SkewTransform skewTransform = null;
        TranslateTransform translateTransform = null;
        MatrixTransform matrixTransform = null;
        TransformGroup transformGroup = null;

        if (transform == null) return null;

        var transformType = transform.GetType();
        if ((scaleTransform = transform as ScaleTransform) != null)
            return new ScaleTransform
            {
                CenterX = scaleTransform.CenterX,
                CenterY = scaleTransform.CenterY,
                ScaleX = scaleTransform.ScaleX,
                ScaleY = scaleTransform.ScaleY
            };

        if ((rotateTransform = transform as RotateTransform) != null)
            return new RotateTransform
            {
                Angle = rotateTransform.Angle,
                CenterX = rotateTransform.CenterX,
                CenterY = rotateTransform.CenterY
            };

        if ((skewTransform = transform as SkewTransform) != null)
            return new SkewTransform
            {
                AngleX = skewTransform.AngleX,
                AngleY = skewTransform.AngleY,
                CenterX = skewTransform.CenterX,
                CenterY = skewTransform.CenterY
            };

        if ((translateTransform = transform as TranslateTransform) != null)
            return new TranslateTransform
            {
                X = translateTransform.X,
                Y = translateTransform.Y
            };

        if ((matrixTransform = transform as MatrixTransform) != null)
            return new MatrixTransform
            {
                Matrix = matrixTransform.Matrix
            };

        if ((transformGroup = transform as TransformGroup) != null)
        {
            var group = new TransformGroup();
            foreach (var childTransform in transformGroup.Children) group.Children.Add(CloneTransform(childTransform));
            return group;
        }

        Debug.Assert(false, "Unexpected Transform type encountered");
        return null;
    }

    /// <summary>
    ///     Updates the X and Y properties based on the current rendered position of the associated element.
    /// </summary>
    private void UpdatePosition()
    {
        var elementToRoot = AssociatedObject.TransformToVisual(RootElement);
        var translation = GetTransformOffset(elementToRoot);
        X = translation.X;
        Y = translation.Y;
    }

    internal void StartDrag(Point positionInElementCoordinates)
    {
        relativePosition = positionInElementCoordinates;

        AssociatedObject.CaptureMouse();

        AssociatedObject.MouseMove += OnMouseMove;
        AssociatedObject.LostMouseCapture += OnLostMouseCapture;
        AssociatedObject.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseLeftButtonUp),
            false /* handledEventsToo */);
    }

    internal void HandleDrag(Point newPositionInElementCoordinates)
    {
        var relativeXDiff = newPositionInElementCoordinates.X - relativePosition.X;
        var relativeYDiff = newPositionInElementCoordinates.Y - relativePosition.Y;

        var elementToRoot = AssociatedObject.TransformToVisual(RootElement);
        var relativeDifferenceInRootCoordinates = TransformAsVector(elementToRoot, relativeXDiff, relativeYDiff);

        settingPosition = true;
        ApplyTranslation(relativeDifferenceInRootCoordinates.X, relativeDifferenceInRootCoordinates.Y);
        UpdatePosition();
        settingPosition = false;
    }

    internal void EndDrag()
    {
        AssociatedObject.MouseMove -= OnMouseMove;
        AssociatedObject.LostMouseCapture -= OnLostMouseCapture;
        AssociatedObject.RemoveHandler(UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnMouseLeftButtonUp));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartDrag(e.GetPosition(AssociatedObject));

        if (DragBegun != null) DragBegun(this, e);
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        EndDrag();

        if (DragFinished != null) DragFinished(this, e);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        AssociatedObject.ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        HandleDrag(e.GetPosition(AssociatedObject));

        if (Dragging != null) Dragging(this, e);
    }

    #endregion

    #region Linear algebra helper methods

    /// <summary>
    ///     Check if one Rect is contained by another.
    /// </summary>
    /// <param name="rect1">The containing Rect.</param>
    /// <param name="rect2">The contained Rect.</param>
    /// <returns><c>True</c> if rect1 contains rect2; otherwise, <c>False</c>.</returns>
    private static bool RectContainsRect(Rect rect1, Rect rect2)
    {
        if (rect1.IsEmpty || rect2.IsEmpty) return false;
        return rect1.X <= rect2.X && rect1.Y <= rect2.Y && rect1.X + rect1.Width >= rect2.X + rect2.Width &&
               rect1.Y + rect1.Height >= rect2.Y + rect2.Height;
    }

    /// <summary>
    ///     Transforms as vector.
    /// </summary>
    /// <param name="transform">The transform.</param>
    /// <param name="x">The X component of the vector.</param>
    /// <param name="y">The Y component of the vector.</param>
    /// <returns>A point containing the values of X and Y transformed by transform as a vector.</returns>
    private static Point TransformAsVector(GeneralTransform transform, double x, double y)
    {
        var origin = transform.Transform(new Point(0, 0));
        var transformedPoint = transform.Transform(new Point(x, y));
        return new Point(transformedPoint.X - origin.X, transformedPoint.Y - origin.Y);
    }

    /// <summary>
    ///     Gets the transform offset.
    /// </summary>
    /// <param name="transform">The transform.</param>
    /// <returns>The offset of the transform.</returns>
    private static Point GetTransformOffset(GeneralTransform transform)
    {
        return transform.Transform(new Point(0, 0));
    }

    #endregion
}
