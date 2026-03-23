using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors.Layout;

namespace Microsoft.Xaml.Behaviors.Input;

public class TranslateZoomRotateBehavior : Behavior<FrameworkElement>
{
	private Transform cachedRenderTransform;

	private bool isDragging;

	private bool isAdjustingTransform;

	private Point lastMousePoint;

	private double lastScaleX = 1.0;

	private double lastScaleY = 1.0;

	private const double HardMinimumScale = 1E-06;

	public static readonly DependencyProperty SupportedGesturesProperty = DependencyProperty.Register("SupportedGestures", typeof(ManipulationModes), typeof(TranslateZoomRotateBehavior), new PropertyMetadata(ManipulationModes.All));

	public static readonly DependencyProperty TranslateFrictionProperty = DependencyProperty.Register("TranslateFriction", typeof(double), typeof(TranslateZoomRotateBehavior), new PropertyMetadata(0.0, frictionChanged, coerceFriction));

	public static readonly DependencyProperty RotationalFrictionProperty = DependencyProperty.Register("RotationalFriction", typeof(double), typeof(TranslateZoomRotateBehavior), new PropertyMetadata(0.0, frictionChanged, coerceFriction));

	public static readonly DependencyProperty ConstrainToParentBoundsProperty = DependencyProperty.Register("ConstrainToParentBounds", typeof(bool), typeof(TranslateZoomRotateBehavior), new PropertyMetadata(false));

	public static readonly DependencyProperty MinimumScaleProperty = DependencyProperty.Register("MinimumScale", typeof(double), typeof(TranslateZoomRotateBehavior), new PropertyMetadata(0.1));

	public static readonly DependencyProperty MaximumScaleProperty = DependencyProperty.Register("MaximumScale", typeof(double), typeof(TranslateZoomRotateBehavior), new PropertyMetadata(10.0));

	public ManipulationModes SupportedGestures
	{
		get
		{
			return (ManipulationModes)GetValue(SupportedGesturesProperty);
		}
		set
		{
			SetValue(SupportedGesturesProperty, value);
		}
	}

	public double TranslateFriction
	{
		get
		{
			return (double)GetValue(TranslateFrictionProperty);
		}
		set
		{
			SetValue(TranslateFrictionProperty, value);
		}
	}

	public double RotationalFriction
	{
		get
		{
			return (double)GetValue(RotationalFrictionProperty);
		}
		set
		{
			SetValue(RotationalFrictionProperty, value);
		}
	}

	public bool ConstrainToParentBounds
	{
		get
		{
			return (bool)GetValue(ConstrainToParentBoundsProperty);
		}
		set
		{
			SetValue(ConstrainToParentBoundsProperty, value);
		}
	}

	public double MinimumScale
	{
		get
		{
			return (double)GetValue(MinimumScaleProperty);
		}
		set
		{
			SetValue(MinimumScaleProperty, value);
		}
	}

	public double MaximumScale
	{
		get
		{
			return (double)GetValue(MaximumScaleProperty);
		}
		set
		{
			SetValue(MaximumScaleProperty, value);
		}
	}

	private Transform RenderTransform
	{
		get
		{
			if (cachedRenderTransform == null || cachedRenderTransform != base.AssociatedObject.RenderTransform)
			{
				Transform renderTransform = MouseDragElementBehavior.CloneTransform(base.AssociatedObject.RenderTransform);
				RenderTransform = renderTransform;
			}
			return cachedRenderTransform;
		}
		set
		{
			if (cachedRenderTransform != value)
			{
				cachedRenderTransform = value;
				base.AssociatedObject.RenderTransform = value;
			}
		}
	}

	private Point RenderTransformOriginInElementCoordinates => new Point(base.AssociatedObject.RenderTransformOrigin.X * base.AssociatedObject.ActualWidth, base.AssociatedObject.RenderTransformOrigin.Y * base.AssociatedObject.ActualHeight);

	private Matrix FullTransformValue
	{
		get
		{
			Point renderTransformOriginInElementCoordinates = RenderTransformOriginInElementCoordinates;
			Matrix value = RenderTransform.Value;
			value.TranslatePrepend(0.0 - renderTransformOriginInElementCoordinates.X, 0.0 - renderTransformOriginInElementCoordinates.Y);
			value.Translate(renderTransformOriginInElementCoordinates.X, renderTransformOriginInElementCoordinates.Y);
			return value;
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

	private FrameworkElement ParentElement => base.AssociatedObject.Parent as FrameworkElement;

	private static void frictionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
	}

	private static object coerceFriction(DependencyObject sender, object value)
	{
		double val = (double)value;
		return Math.Max(0.0, Math.Min(1.0, val));
	}

	internal void EnsureTransform()
	{
		if (!(RenderTransform is MatrixTransform { IsFrozen: false }))
		{
			MatrixTransform renderTransform = ((RenderTransform == null) ? new MatrixTransform(Matrix.Identity) : new MatrixTransform(FullTransformValue));
			RenderTransform = renderTransform;
		}
		base.AssociatedObject.RenderTransformOrigin = new Point(0.0, 0.0);
	}

	internal void ApplyRotationTransform(double angle, Point rotationPoint)
	{
		Matrix matrix = MatrixTransform.Matrix;
		matrix.RotateAt(angle, rotationPoint.X, rotationPoint.Y);
		MatrixTransform.Matrix = matrix;
	}

	internal void ApplyScaleTransform(double scaleX, double scaleY, Point scalePoint)
	{
		double val = scaleX * lastScaleX;
		val = Math.Min(Math.Max(Math.Max(1E-06, MinimumScale), val), MaximumScale);
		scaleX = val / lastScaleX;
		lastScaleX = scaleX * lastScaleX;
		double val2 = scaleY * lastScaleY;
		val2 = Math.Min(Math.Max(Math.Max(1E-06, MinimumScale), val2), MaximumScale);
		scaleY = val2 / lastScaleY;
		lastScaleY = scaleY * lastScaleY;
		Matrix matrix = MatrixTransform.Matrix;
		matrix.ScaleAt(scaleX, scaleY, scalePoint.X, scalePoint.Y);
		MatrixTransform.Matrix = matrix;
	}

	internal void ApplyTranslateTransform(double x, double y)
	{
		Matrix matrix = MatrixTransform.Matrix;
		matrix.Translate(x, y);
		MatrixTransform.Matrix = matrix;
	}

	private void ManipulationStarting(object sender, ManipulationStartingEventArgs e)
	{
		FrameworkElement parentElement = ParentElement;
		if (parentElement == null || !parentElement.IsAncestorOf(base.AssociatedObject))
		{
			parentElement = base.AssociatedObject;
		}
		e.ManipulationContainer = parentElement;
		e.Mode = SupportedGestures;
		e.Handled = true;
	}

	private void ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e)
	{
		double num = ((TranslateFriction == 1.0) ? 1.0 : (-0.00666 * Math.Log(1.0 - TranslateFriction)));
		double val = e.InitialVelocities.LinearVelocity.Length * num;
		e.TranslationBehavior = new InertiaTranslationBehavior
		{
			InitialVelocity = e.InitialVelocities.LinearVelocity,
			DesiredDeceleration = Math.Max(val, 0.0)
		};
		double num2 = ((RotationalFriction == 1.0) ? 1.0 : (-0.00666 * Math.Log(1.0 - RotationalFriction)));
		double val2 = Math.Abs(e.InitialVelocities.AngularVelocity) * num2;
		e.RotationBehavior = new InertiaRotationBehavior
		{
			InitialVelocity = e.InitialVelocities.AngularVelocity,
			DesiredDeceleration = Math.Max(val2, 0.0)
		};
		e.Handled = true;
	}

	private void ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
	{
		EnsureTransform();
		ManipulationDelta deltaManipulation = e.DeltaManipulation;
		Point point = new Point(base.AssociatedObject.ActualWidth / 2.0, base.AssociatedObject.ActualHeight / 2.0);
		Point point2 = FullTransformValue.Transform(point);
		ApplyScaleTransform(deltaManipulation.Scale.X, deltaManipulation.Scale.Y, point2);
		ApplyRotationTransform(deltaManipulation.Rotation, point2);
		ApplyTranslateTransform(deltaManipulation.Translation.X, deltaManipulation.Translation.Y);
		FrameworkElement frameworkElement = (FrameworkElement)e.ManipulationContainer;
		Rect rect = new Rect(frameworkElement.RenderSize);
		Rect rect2 = base.AssociatedObject.TransformToVisual(frameworkElement).TransformBounds(new Rect(base.AssociatedObject.RenderSize));
		if (e.IsInertial && ConstrainToParentBounds && !rect.Contains(rect2))
		{
			e.Complete();
		}
		e.Handled = true;
	}

	private void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		base.AssociatedObject.CaptureMouse();
		base.AssociatedObject.MouseMove += AssociatedObject_MouseMove;
		base.AssociatedObject.LostMouseCapture += AssociatedObject_LostMouseCapture;
		e.Handled = true;
		lastMousePoint = e.GetPosition(base.AssociatedObject);
		isDragging = true;
	}

	private void MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		base.AssociatedObject.ReleaseMouseCapture();
		e.Handled = true;
	}

	private void AssociatedObject_LostMouseCapture(object sender, MouseEventArgs e)
	{
		isDragging = false;
		base.AssociatedObject.MouseMove -= AssociatedObject_MouseMove;
		base.AssociatedObject.LostMouseCapture -= AssociatedObject_LostMouseCapture;
	}

	private void AssociatedObject_MouseMove(object sender, MouseEventArgs e)
	{
		if (isDragging && !isAdjustingTransform)
		{
			isAdjustingTransform = true;
			Vector vector = e.GetPosition(base.AssociatedObject) - lastMousePoint;
			if ((SupportedGestures & ManipulationModes.TranslateX) == 0)
			{
				vector.X = 0.0;
			}
			if ((SupportedGestures & ManipulationModes.TranslateY) == 0)
			{
				vector.Y = 0.0;
			}
			Vector vector2 = FullTransformValue.Transform(vector);
			ApplyTranslateTransform(vector2.X, vector2.Y);
			lastMousePoint = e.GetPosition(base.AssociatedObject);
			isAdjustingTransform = false;
		}
	}

	protected override void OnAttached()
	{
		base.AssociatedObject.AddHandler(UIElement.ManipulationStartingEvent, new EventHandler<ManipulationStartingEventArgs>(ManipulationStarting), handledEventsToo: false);
		base.AssociatedObject.AddHandler(UIElement.ManipulationInertiaStartingEvent, new EventHandler<ManipulationInertiaStartingEventArgs>(ManipulationInertiaStarting), handledEventsToo: false);
		base.AssociatedObject.AddHandler(UIElement.ManipulationDeltaEvent, new EventHandler<ManipulationDeltaEventArgs>(ManipulationDelta), handledEventsToo: false);
		base.AssociatedObject.IsManipulationEnabled = true;
		base.AssociatedObject.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(MouseLeftButtonDown), handledEventsToo: false);
		base.AssociatedObject.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(MouseLeftButtonUp), handledEventsToo: false);
	}

	protected override void OnDetaching()
	{
		base.AssociatedObject.RemoveHandler(UIElement.ManipulationStartingEvent, new EventHandler<ManipulationStartingEventArgs>(ManipulationStarting));
		base.AssociatedObject.RemoveHandler(UIElement.ManipulationInertiaStartingEvent, new EventHandler<ManipulationInertiaStartingEventArgs>(ManipulationInertiaStarting));
		base.AssociatedObject.RemoveHandler(UIElement.ManipulationDeltaEvent, new EventHandler<ManipulationDeltaEventArgs>(ManipulationDelta));
		base.AssociatedObject.IsManipulationEnabled = false;
		base.AssociatedObject.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(MouseLeftButtonDown), handledEventsToo: false);
		base.AssociatedObject.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(MouseLeftButtonUp), handledEventsToo: false);
	}
}
