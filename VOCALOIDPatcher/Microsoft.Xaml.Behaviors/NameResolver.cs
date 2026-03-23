using System;
using System.Windows;

namespace Microsoft.Xaml.Behaviors;

internal sealed class NameResolver
{
	private string name;

	private FrameworkElement nameScopeReferenceElement;

	public string Name
	{
		get
		{
			return name;
		}
		set
		{
			DependencyObject oldObject = Object;
			name = value;
			UpdateObjectFromName(oldObject);
		}
	}

	public DependencyObject Object
	{
		get
		{
			if (string.IsNullOrEmpty(Name) && HasAttempedResolve)
			{
				return NameScopeReferenceElement;
			}
			return ResolvedObject;
		}
	}

	public FrameworkElement NameScopeReferenceElement
	{
		get
		{
			return nameScopeReferenceElement;
		}
		set
		{
			FrameworkElement oldNameScopeReference = NameScopeReferenceElement;
			nameScopeReferenceElement = value;
			OnNameScopeReferenceElementChanged(oldNameScopeReference);
		}
	}

	private FrameworkElement ActualNameScopeReferenceElement
	{
		get
		{
			if (NameScopeReferenceElement == null || !Interaction.IsElementLoaded(NameScopeReferenceElement))
			{
				return null;
			}
			return GetActualNameScopeReference(NameScopeReferenceElement);
		}
	}

	private DependencyObject ResolvedObject { get; set; }

	private bool PendingReferenceElementLoad { get; set; }

	private bool HasAttempedResolve { get; set; }

	public event EventHandler<NameResolvedEventArgs> ResolvedElementChanged;

	private void OnNameScopeReferenceElementChanged(FrameworkElement oldNameScopeReference)
	{
		if (PendingReferenceElementLoad)
		{
			oldNameScopeReference.Loaded -= OnNameScopeReferenceLoaded;
			PendingReferenceElementLoad = false;
		}
		HasAttempedResolve = false;
		UpdateObjectFromName(Object);
	}

	private void UpdateObjectFromName(DependencyObject oldObject)
	{
		DependencyObject resolvedObject = null;
		ResolvedObject = null;
		if (NameScopeReferenceElement != null)
		{
			if (!Interaction.IsElementLoaded(NameScopeReferenceElement))
			{
				NameScopeReferenceElement.Loaded += OnNameScopeReferenceLoaded;
				PendingReferenceElementLoad = true;
				return;
			}
			if (!string.IsNullOrEmpty(Name))
			{
				FrameworkElement actualNameScopeReferenceElement = ActualNameScopeReferenceElement;
				if (actualNameScopeReferenceElement != null)
				{
					resolvedObject = actualNameScopeReferenceElement.FindName(Name) as DependencyObject;
				}
			}
		}
		HasAttempedResolve = true;
		ResolvedObject = resolvedObject;
		if (oldObject != Object)
		{
			OnObjectChanged(oldObject, Object);
		}
	}

	private void OnObjectChanged(DependencyObject oldTarget, DependencyObject newTarget)
	{
		if (this.ResolvedElementChanged != null)
		{
			this.ResolvedElementChanged(this, new NameResolvedEventArgs(oldTarget, newTarget));
		}
	}

	private FrameworkElement GetActualNameScopeReference(FrameworkElement initialReferenceElement)
	{
		FrameworkElement frameworkElement = initialReferenceElement;
		if (IsNameScope(initialReferenceElement))
		{
			frameworkElement = (initialReferenceElement.Parent as FrameworkElement) ?? frameworkElement;
		}
		return frameworkElement;
	}

	private bool IsNameScope(FrameworkElement frameworkElement)
	{
		if (frameworkElement.Parent is FrameworkElement frameworkElement2)
		{
			return frameworkElement2.FindName(Name) != null;
		}
		return false;
	}

	private void OnNameScopeReferenceLoaded(object sender, RoutedEventArgs e)
	{
		PendingReferenceElementLoad = false;
		NameScopeReferenceElement.Loaded -= OnNameScopeReferenceLoaded;
		UpdateObjectFromName(Object);
	}
}
