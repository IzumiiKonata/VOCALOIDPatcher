using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors.Core;

namespace Microsoft.Xaml.Behaviors.Layout;

public class MouseDragElementBehavior : Behavior<FrameworkElement>
{
	private bool settingPosition;

	private Point relativePosition;

	private Transform cachedRenderTransform;

	public static readonly DependencyProperty XProperty = DependencyProperty.Register("X", typeof(double), typeof(MouseDragElementBehavior), new PropertyMetadata(double.NaN, OnXChanged));

	public static readonly DependencyProperty YProperty = DependencyProperty.Register("Y", typeof(double), typeof(MouseDragElementBehavior), new PropertyMetadata(double.NaN, OnYChanged));

	public static readonly DependencyProperty ConstrainToParentBoundsProperty = DependencyProperty.Register("ConstrainToParentBounds", typeof(bool), typeof(MouseDragElementBehavior), new PropertyMetadata(false, OnConstrainToParentBoundsChanged));

	public double X
	{
		get
		{
			return (double)GetValue(XProperty);
		}
		set
		{
			SetValue(XProperty, value);
		}
	}

	public double Y
	{
		get
		{
			return (double)GetValue(YProperty);
		}
		set
		{
			SetValue(YProperty, value);
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

	private Point ActualPosition
	{
		get
		{
			Point transformOffset = GetTransformOffset(base.AssociatedObject.TransformToVisual(RootElement));
			return new Point(transformOffset.X, transformOffset.Y);
		}
	}

	private Rect ElementBounds
	{
		get
		{
			Rect layoutRect = ExtendedVisualStateManager.GetLayoutRect(base.AssociatedObject);
			return new Rect(new Point(0.0, 0.0), new Size(layoutRect.Width, layoutRect.Height));
		}
	}

	private FrameworkElement ParentElement => base.AssociatedObject.Parent as FrameworkElement;

	private UIElement RootElement
	{
		get
		{
			DependencyObject dependencyObject = base.AssociatedObject;
			for (DependencyObject dependencyObject2 = dependencyObject; dependencyObject2 != null; dependencyObject2 = VisualTreeHelper.GetParent(dependencyObject))
			{
				dependencyObject = dependencyObject2;
			}
			return dependencyObject as UIElement;
		}
	}

	private Transform RenderTransform
	{
		get
		{
			if (cachedRenderTransform == null || cachedRenderTransform != base.AssociatedObject.RenderTransform)
			{
				Transform renderTransform = CloneTransform(base.AssociatedObject.RenderTransform);
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

	public event MouseEventHandler DragBegun;

	public event MouseEventHandler Dragging;

	public event MouseEventHandler DragFinished;

	private static void OnXChanged(object sender, DependencyPropertyChangedEventArgs args)
	{
		MouseDragElementBehavior mouseDragElementBehavior = (MouseDragElementBehavior)sender;
		mouseDragElementBehavior.UpdatePosition(new Point((double)args.NewValue, mouseDragElementBehavior.Y));
	}

	private static void OnYChanged(object sender, DependencyPropertyChangedEventArgs args)
	{
		MouseDragElementBehavior obj = (MouseDragElementBehavior)sender;
		obj.UpdatePosition(new Point(obj.X, (double)args.NewValue));
	}

	private static void OnConstrainToParentBoundsChanged(object sender, DependencyPropertyChangedEventArgs args)
	{
		MouseDragElementBehavior mouseDragElementBehavior = (MouseDragElementBehavior)sender;
		mouseDragElementBehavior.UpdatePosition(new Point(mouseDragElementBehavior.X, mouseDragElementBehavior.Y));
	}

	private void UpdatePosition(Point point)
	{
		if (!settingPosition && base.AssociatedObject != null)
		{
			Point transformOffset = GetTransformOffset(base.AssociatedObject.TransformToVisual(RootElement));
			double x = (double.IsNaN(point.X) ? 0.0 : (point.X - transformOffset.X));
			double y = (double.IsNaN(point.Y) ? 0.0 : (point.Y - transformOffset.Y));
			ApplyTranslation(x, y);
		}
	}

	private void ApplyTranslation(double x, double y)
	{
		if (ParentElement == null)
		{
			return;
		}
		Point point = TransformAsVector(RootElement.TransformToVisual(ParentElement), x, y);
		x = point.X;
		y = point.Y;
		if (ConstrainToParentBounds)
		{
			FrameworkElement parentElement = ParentElement;
			Rect rect = new Rect(0.0, 0.0, parentElement.ActualWidth, parentElement.ActualHeight);
			GeneralTransform generalTransform = base.AssociatedObject.TransformToVisual(parentElement);
			Rect elementBounds = ElementBounds;
			elementBounds = generalTransform.TransformBounds(elementBounds);
			Rect rect2 = elementBounds;
			rect2.X += x;
			rect2.Y += y;
			if (!RectContainsRect(rect, rect2))
			{
				if (rect2.X < rect.Left)
				{
					double num = rect2.X - rect.Left;
					x -= num;
				}
				else if (rect2.Right > rect.Right)
				{
					double num2 = rect2.Right - rect.Right;
					x -= num2;
				}
				if (rect2.Y < rect.Top)
				{
					double num3 = rect2.Y - rect.Top;
					y -= num3;
				}
				else if (rect2.Bottom > rect.Bottom)
				{
					double num4 = rect2.Bottom - rect.Bottom;
					y -= num4;
				}
			}
		}
		ApplyTranslationTransform(x, y);
	}

	internal void ApplyTranslationTransform(double x, double y)
	{
		Transform renderTransform = RenderTransform;
		TranslateTransform translateTransform = renderTransform as TranslateTransform;
		if (translateTransform == null)
		{
			TransformGroup transformGroup = renderTransform as TransformGroup;
			MatrixTransform matrixTransform = renderTransform as MatrixTransform;
			if (transformGroup != null)
			{
				if (transformGroup.Children.Count > 0)
				{
					translateTransform = transformGroup.Children[transformGroup.Children.Count - 1] as TranslateTransform;
				}
				if (translateTransform == null)
				{
					translateTransform = new TranslateTransform();
					transformGroup.Children.Add(translateTransform);
				}
			}
			else
			{
				if (matrixTransform != null)
				{
					Matrix matrix = matrixTransform.Matrix;
					matrix.OffsetX += x;
					matrix.OffsetY += y;
					MatrixTransform matrixTransform2 = new MatrixTransform();
					matrixTransform2.Matrix = matrix;
					RenderTransform = matrixTransform2;
					return;
				}
				TransformGroup transformGroup2 = new TransformGroup();
				translateTransform = new TranslateTransform();
				if (renderTransform != null)
				{
					transformGroup2.Children.Add(renderTransform);
				}
				transformGroup2.Children.Add(translateTransform);
				RenderTransform = transformGroup2;
			}
		}
		translateTransform.X += x;
		translateTransform.Y += y;
	}

	internal static Transform CloneTransform(Transform transform)
	{
		ScaleTransform scaleTransform = null;
		RotateTransform rotateTransform = null;
		SkewTransform skewTransform = null;
		TranslateTransform translateTransform = null;
		MatrixTransform matrixTransform = null;
		TransformGroup transformGroup = null;
		if (transform == null)
		{
			return null;
		}
		transform.GetType();
		if (transform is ScaleTransform scaleTransform2)
		{
			return new ScaleTransform
			{
				CenterX = scaleTransform2.CenterX,
				CenterY = scaleTransform2.CenterY,
				ScaleX = scaleTransform2.ScaleX,
				ScaleY = scaleTransform2.ScaleY
			};
		}
		if (transform is RotateTransform rotateTransform2)
		{
			return new RotateTransform
			{
				Angle = rotateTransform2.Angle,
				CenterX = rotateTransform2.CenterX,
				CenterY = rotateTransform2.CenterY
			};
		}
		if (transform is SkewTransform skewTransform2)
		{
			return new SkewTransform
			{
				AngleX = skewTransform2.AngleX,
				AngleY = skewTransform2.AngleY,
				CenterX = skewTransform2.CenterX,
				CenterY = skewTransform2.CenterY
			};
		}
		if (transform is TranslateTransform translateTransform2)
		{
			return new TranslateTransform
			{
				X = translateTransform2.X,
				Y = translateTransform2.Y
			};
		}
		if (transform is MatrixTransform matrixTransform2)
		{
			return new MatrixTransform
			{
				Matrix = matrixTransform2.Matrix
			};
		}
		if (transform is TransformGroup transformGroup2)
		{
			TransformGroup transformGroup3 = new TransformGroup();
			{
				foreach (Transform child in transformGroup2.Children)
				{
					transformGroup3.Children.Add(CloneTransform(child));
				}
				return transformGroup3;
			}
		}
		return null;
	}

	private void UpdatePosition()
	{
		Point transformOffset = GetTransformOffset(base.AssociatedObject.TransformToVisual(RootElement));
		X = transformOffset.X;
		Y = transformOffset.Y;
	}

	internal void StartDrag(Point positionInElementCoordinates)
	{
		relativePosition = positionInElementCoordinates;
		base.AssociatedObject.CaptureMouse();
		base.AssociatedObject.MouseMove += OnMouseMove;
		base.AssociatedObject.LostMouseCapture += OnLostMouseCapture;
		base.AssociatedObject.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseLeftButtonUp), handledEventsToo: false);
	}

	internal void HandleDrag(Point newPositionInElementCoordinates)
	{
		double x = newPositionInElementCoordinates.X - relativePosition.X;
		double y = newPositionInElementCoordinates.Y - relativePosition.Y;
		Point point = TransformAsVector(base.AssociatedObject.TransformToVisual(RootElement), x, y);
		settingPosition = true;
		ApplyTranslation(point.X, point.Y);
		UpdatePosition();
		settingPosition = false;
	}

	internal void EndDrag()
	{
		base.AssociatedObject.MouseMove -= OnMouseMove;
		base.AssociatedObject.LostMouseCapture -= OnLostMouseCapture;
		base.AssociatedObject.RemoveHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnMouseLeftButtonUp));
	}

	private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		StartDrag(e.GetPosition(base.AssociatedObject));
		if (this.DragBegun != null)
		{
			this.DragBegun(this, e);
		}
	}

	private void OnLostMouseCapture(object sender, MouseEventArgs e)
	{
		EndDrag();
		if (this.DragFinished != null)
		{
			this.DragFinished(this, e);
		}
	}

	private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		base.AssociatedObject.ReleaseMouseCapture();
	}

	private void OnMouseMove(object sender, MouseEventArgs e)
	{
		HandleDrag(e.GetPosition(base.AssociatedObject));
		if (this.Dragging != null)
		{
			this.Dragging(this, e);
		}
	}

	private static bool RectContainsRect(Rect rect1, Rect rect2)
	{
		if (rect1.IsEmpty || rect2.IsEmpty)
		{
			return false;
		}
		if (rect1.X <= rect2.X && rect1.Y <= rect2.Y && rect1.X + rect1.Width >= rect2.X + rect2.Width)
		{
			return rect1.Y + rect1.Height >= rect2.Y + rect2.Height;
		}
		return false;
	}

	private static Point TransformAsVector(GeneralTransform transform, double x, double y)
	{
		Point point = transform.Transform(new Point(0.0, 0.0));
		Point point2 = transform.Transform(new Point(x, y));
		return new Point(point2.X - point.X, point2.Y - point.Y);
	}

	private static Point GetTransformOffset(GeneralTransform transform)
	{
		return transform.Transform(new Point(0.0, 0.0));
	}

	protected override void OnAttached()
	{
		base.AssociatedObject.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnMouseLeftButtonDown), handledEventsToo: false);
	}

	protected override void OnDetaching()
	{
		base.AssociatedObject.RemoveHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnMouseLeftButtonDown));
	}
}
