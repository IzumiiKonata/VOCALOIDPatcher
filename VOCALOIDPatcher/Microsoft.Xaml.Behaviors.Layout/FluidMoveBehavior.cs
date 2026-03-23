using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Microsoft.Xaml.Behaviors.Layout;

public sealed class FluidMoveBehavior : FluidMoveBehaviorBase
{
	public static readonly DependencyProperty DurationProperty = DependencyProperty.Register("Duration", typeof(Duration), typeof(FluidMoveBehavior), new PropertyMetadata(new Duration(TimeSpan.FromSeconds(1.0))));

	public static readonly DependencyProperty InitialTagProperty = DependencyProperty.Register("InitialTag", typeof(TagType), typeof(FluidMoveBehavior), new PropertyMetadata(TagType.Element));

	public static readonly DependencyProperty InitialTagPathProperty = DependencyProperty.Register("InitialTagPath", typeof(string), typeof(FluidMoveBehavior), new PropertyMetadata(string.Empty));

	private static readonly DependencyProperty initialIdentityTagProperty = DependencyProperty.RegisterAttached("InitialIdentityTag", typeof(object), typeof(FluidMoveBehavior), new PropertyMetadata(null));

	public static readonly DependencyProperty FloatAboveProperty = DependencyProperty.Register("FloatAbove", typeof(bool), typeof(FluidMoveBehavior), new PropertyMetadata(true));

	public static readonly DependencyProperty EaseXProperty = DependencyProperty.Register("EaseX", typeof(IEasingFunction), typeof(FluidMoveBehavior), new PropertyMetadata(null));

	public static readonly DependencyProperty EaseYProperty = DependencyProperty.Register("EaseY", typeof(IEasingFunction), typeof(FluidMoveBehavior), new PropertyMetadata(null));

	private static readonly DependencyProperty overlayProperty = DependencyProperty.RegisterAttached("Overlay", typeof(object), typeof(FluidMoveBehavior), new PropertyMetadata(null));

	private static readonly DependencyProperty cacheDuringOverlayProperty = DependencyProperty.RegisterAttached("CacheDuringOverlay", typeof(object), typeof(FluidMoveBehavior), new PropertyMetadata(null));

	private static readonly DependencyProperty hasTransformWrapperProperty = DependencyProperty.RegisterAttached("HasTransformWrapper", typeof(bool), typeof(FluidMoveBehavior), new PropertyMetadata(false));

	private static Dictionary<object, Storyboard> transitionStoryboardDictionary = new Dictionary<object, Storyboard>();

	public Duration Duration
	{
		get
		{
			return (Duration)GetValue(DurationProperty);
		}
		set
		{
			SetValue(DurationProperty, value);
		}
	}

	public TagType InitialTag
	{
		get
		{
			return (TagType)GetValue(InitialTagProperty);
		}
		set
		{
			SetValue(InitialTagProperty, value);
		}
	}

	public string InitialTagPath
	{
		get
		{
			return (string)GetValue(InitialTagPathProperty);
		}
		set
		{
			SetValue(InitialTagPathProperty, value);
		}
	}

	public bool FloatAbove
	{
		get
		{
			return (bool)GetValue(FloatAboveProperty);
		}
		set
		{
			SetValue(FloatAboveProperty, value);
		}
	}

	public IEasingFunction EaseX
	{
		get
		{
			return (IEasingFunction)GetValue(EaseXProperty);
		}
		set
		{
			SetValue(EaseXProperty, value);
		}
	}

	public IEasingFunction EaseY
	{
		get
		{
			return (IEasingFunction)GetValue(EaseYProperty);
		}
		set
		{
			SetValue(EaseYProperty, value);
		}
	}

	protected override bool ShouldSkipInitialLayout
	{
		get
		{
			if (!base.ShouldSkipInitialLayout)
			{
				return InitialTag == TagType.DataContext;
			}
			return true;
		}
	}

	private static object GetInitialIdentityTag(DependencyObject obj)
	{
		return obj.GetValue(initialIdentityTagProperty);
	}

	private static void SetInitialIdentityTag(DependencyObject obj, object value)
	{
		obj.SetValue(initialIdentityTagProperty, value);
	}

	private static object GetOverlay(DependencyObject obj)
	{
		return obj.GetValue(overlayProperty);
	}

	private static void SetOverlay(DependencyObject obj, object value)
	{
		obj.SetValue(overlayProperty, value);
	}

	private static object GetCacheDuringOverlay(DependencyObject obj)
	{
		return obj.GetValue(cacheDuringOverlayProperty);
	}

	private static void SetCacheDuringOverlay(DependencyObject obj, object value)
	{
		obj.SetValue(cacheDuringOverlayProperty, value);
	}

	private static bool GetHasTransformWrapper(DependencyObject obj)
	{
		return (bool)obj.GetValue(hasTransformWrapperProperty);
	}

	private static void SetHasTransformWrapper(DependencyObject obj, bool value)
	{
		obj.SetValue(hasTransformWrapperProperty, value);
	}

	protected override void EnsureTags(FrameworkElement child)
	{
		base.EnsureTags(child);
		if (InitialTag == TagType.DataContext && !(child.ReadLocalValue(initialIdentityTagProperty) is BindingExpression))
		{
			child.SetBinding(initialIdentityTagProperty, new Binding(InitialTagPath));
		}
	}

	internal override void UpdateLayoutTransitionCore(FrameworkElement child, FrameworkElement root, object tag, TagData newTagData)
	{
		bool flag = false;
		bool flag2 = false;
		object initialIdentityTag = GetInitialIdentityTag(child);
		TagData value;
		bool flag3 = FluidMoveBehaviorBase.TagDictionary.TryGetValue(tag, out value);
		if (flag3 && value.InitialTag != initialIdentityTag)
		{
			flag3 = false;
			FluidMoveBehaviorBase.TagDictionary.Remove(tag);
		}
		Rect rect;
		if (!flag3)
		{
			if (initialIdentityTag != null && FluidMoveBehaviorBase.TagDictionary.TryGetValue(initialIdentityTag, out var value2))
			{
				rect = FluidMoveBehaviorBase.TranslateRect(value2.AppRect, root, newTagData.Parent);
				flag = true;
				flag2 = true;
			}
			else
			{
				rect = Rect.Empty;
			}
			value = new TagData
			{
				ParentRect = Rect.Empty,
				AppRect = Rect.Empty,
				Parent = newTagData.Parent,
				Child = child,
				Timestamp = DateTime.Now,
				InitialTag = initialIdentityTag
			};
			FluidMoveBehaviorBase.TagDictionary.Add(tag, value);
		}
		else if (value.Parent != VisualTreeHelper.GetParent(child))
		{
			rect = FluidMoveBehaviorBase.TranslateRect(value.AppRect, root, newTagData.Parent);
			flag = true;
		}
		else
		{
			rect = value.ParentRect;
		}
		FrameworkElement originalChild = child;
		if ((!IsEmptyRect(rect) && !IsEmptyRect(newTagData.ParentRect) && (!IsClose(rect.Left, newTagData.ParentRect.Left) || !IsClose(rect.Top, newTagData.ParentRect.Top))) || (child != value.Child && transitionStoryboardDictionary.ContainsKey(tag)))
		{
			Rect currentRect = rect;
			bool flag4 = false;
			Storyboard value3 = null;
			if (transitionStoryboardDictionary.TryGetValue(tag, out value3))
			{
				object overlay = GetOverlay(value.Child);
				AdornerContainer adornerContainer = (AdornerContainer)overlay;
				flag4 = overlay != null;
				FrameworkElement child2 = value.Child;
				if (overlay != null && adornerContainer.Child is Canvas canvas)
				{
					child2 = canvas.Children[0] as FrameworkElement;
				}
				if (!flag2)
				{
					currentRect = GetTransform(child2).TransformBounds(currentRect);
				}
				transitionStoryboardDictionary.Remove(tag);
				value3.Stop();
				value3 = null;
				RemoveTransform(child2);
				if (overlay != null)
				{
					AdornerLayer.GetAdornerLayer(root).Remove(adornerContainer);
					TransferLocalValue(value.Child, cacheDuringOverlayProperty, UIElement.RenderTransformProperty);
					SetOverlay(value.Child, null);
				}
			}
			object overlay2 = null;
			if (flag4 || (flag && FloatAbove))
			{
				Canvas canvas2 = new Canvas
				{
					Width = newTagData.ParentRect.Width,
					Height = newTagData.ParentRect.Height,
					IsHitTestVisible = false
				};
				Rectangle rectangle = new Rectangle
				{
					Width = newTagData.ParentRect.Width,
					Height = newTagData.ParentRect.Height,
					IsHitTestVisible = false
				};
				rectangle.Fill = new VisualBrush(child);
				canvas2.Children.Add(rectangle);
				AdornerContainer adorner = (AdornerContainer)(overlay2 = new AdornerContainer(child)
				{
					Child = canvas2
				});
				SetOverlay(originalChild, overlay2);
				AdornerLayer.GetAdornerLayer(root).Add(adorner);
				TransferLocalValue(child, UIElement.RenderTransformProperty, cacheDuringOverlayProperty);
				child.RenderTransform = new TranslateTransform(-10000.0, -10000.0);
				canvas2.RenderTransform = new TranslateTransform(10000.0, 10000.0);
				child = rectangle;
			}
			Rect layoutRect = newTagData.ParentRect;
			Storyboard transitionStoryboard = CreateTransitionStoryboard(child, flag2, ref layoutRect, ref currentRect);
			transitionStoryboardDictionary.Add(tag, transitionStoryboard);
			transitionStoryboard.Completed += delegate
			{
				if (transitionStoryboardDictionary.TryGetValue(tag, out var value4) && value4 == transitionStoryboard)
				{
					transitionStoryboardDictionary.Remove(tag);
					transitionStoryboard.Stop();
					RemoveTransform(child);
					child.InvalidateMeasure();
					if (overlay2 != null)
					{
						AdornerLayer.GetAdornerLayer(root).Remove((AdornerContainer)overlay2);
						TransferLocalValue(originalChild, cacheDuringOverlayProperty, UIElement.RenderTransformProperty);
						SetOverlay(originalChild, null);
					}
				}
			};
			transitionStoryboard.Begin();
		}
		value.ParentRect = newTagData.ParentRect;
		value.AppRect = newTagData.AppRect;
		value.Parent = newTagData.Parent;
		value.Child = newTagData.Child;
		value.Timestamp = newTagData.Timestamp;
	}

	private Storyboard CreateTransitionStoryboard(FrameworkElement child, bool usingBeforeLoaded, ref Rect layoutRect, ref Rect currentRect)
	{
		Duration duration = Duration;
		Storyboard storyboard = new Storyboard();
		storyboard.Duration = duration;
		double num = ((!usingBeforeLoaded || layoutRect.Width == 0.0) ? 1.0 : (currentRect.Width / layoutRect.Width));
		double num2 = ((!usingBeforeLoaded || layoutRect.Height == 0.0) ? 1.0 : (currentRect.Height / layoutRect.Height));
		double num3 = currentRect.Left - layoutRect.Left;
		double num4 = currentRect.Top - layoutRect.Top;
		TransformGroup transformGroup = new TransformGroup();
		transformGroup.Children.Add(new ScaleTransform
		{
			ScaleX = num,
			ScaleY = num2
		});
		transformGroup.Children.Add(new TranslateTransform
		{
			X = num3,
			Y = num4
		});
		AddTransform(child, transformGroup);
		string text = "(FrameworkElement.RenderTransform).";
		if (child.RenderTransform is TransformGroup transformGroup2 && GetHasTransformWrapper(child))
		{
			text = text + "(TransformGroup.Children)[" + (transformGroup2.Children.Count - 1) + "].";
		}
		if (usingBeforeLoaded)
		{
			if (num != 1.0)
			{
				DoubleAnimation doubleAnimation = new DoubleAnimation
				{
					Duration = duration,
					From = num,
					To = 1.0
				};
				Storyboard.SetTarget(doubleAnimation, child);
				Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(text + "(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
				doubleAnimation.EasingFunction = EaseX;
				storyboard.Children.Add(doubleAnimation);
			}
			if (num2 != 1.0)
			{
				DoubleAnimation doubleAnimation2 = new DoubleAnimation
				{
					Duration = duration,
					From = num2,
					To = 1.0
				};
				Storyboard.SetTarget(doubleAnimation2, child);
				Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath(text + "(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
				doubleAnimation2.EasingFunction = EaseY;
				storyboard.Children.Add(doubleAnimation2);
			}
		}
		if (num3 != 0.0)
		{
			DoubleAnimation doubleAnimation3 = new DoubleAnimation
			{
				Duration = duration,
				From = num3,
				To = 0.0
			};
			Storyboard.SetTarget(doubleAnimation3, child);
			Storyboard.SetTargetProperty(doubleAnimation3, new PropertyPath(text + "(TransformGroup.Children)[1].(TranslateTransform.X)"));
			doubleAnimation3.EasingFunction = EaseX;
			storyboard.Children.Add(doubleAnimation3);
		}
		if (num4 != 0.0)
		{
			DoubleAnimation doubleAnimation4 = new DoubleAnimation
			{
				Duration = duration,
				From = num4,
				To = 0.0
			};
			Storyboard.SetTarget(doubleAnimation4, child);
			Storyboard.SetTargetProperty(doubleAnimation4, new PropertyPath(text + "(TransformGroup.Children)[1].(TranslateTransform.Y)"));
			doubleAnimation4.EasingFunction = EaseY;
			storyboard.Children.Add(doubleAnimation4);
		}
		return storyboard;
	}

	private static void AddTransform(FrameworkElement child, Transform transform)
	{
		TransformGroup transformGroup = child.RenderTransform as TransformGroup;
		if (transformGroup == null)
		{
			transformGroup = new TransformGroup();
			transformGroup.Children.Add(child.RenderTransform);
			child.RenderTransform = transformGroup;
			SetHasTransformWrapper(child, value: true);
		}
		transformGroup.Children.Add(transform);
	}

	private static Transform GetTransform(FrameworkElement child)
	{
		if (child.RenderTransform is TransformGroup transformGroup && transformGroup.Children.Count > 0)
		{
			return transformGroup.Children[transformGroup.Children.Count - 1];
		}
		return new TranslateTransform();
	}

	private static void RemoveTransform(FrameworkElement child)
	{
		if (child.RenderTransform is TransformGroup transformGroup)
		{
			if (GetHasTransformWrapper(child))
			{
				child.RenderTransform = transformGroup.Children[0];
				SetHasTransformWrapper(child, value: false);
			}
			else
			{
				transformGroup.Children.RemoveAt(transformGroup.Children.Count - 1);
			}
		}
	}

	private static void TransferLocalValue(FrameworkElement element, DependencyProperty source, DependencyProperty dest)
	{
		object obj = element.ReadLocalValue(source);
		if (obj is BindingExpressionBase bindingExpressionBase)
		{
			element.SetBinding(dest, bindingExpressionBase.ParentBindingBase);
		}
		else if (obj == DependencyProperty.UnsetValue)
		{
			element.ClearValue(dest);
		}
		else
		{
			element.SetValue(dest, element.GetAnimationBaseValue(source));
		}
		element.ClearValue(source);
	}

	private static bool IsClose(double a, double b)
	{
		return Math.Abs(a - b) < 1E-07;
	}

	private static bool IsEmptyRect(Rect rect)
	{
		if (!rect.IsEmpty && !double.IsNaN(rect.Left))
		{
			return double.IsNaN(rect.Top);
		}
		return true;
	}
}
