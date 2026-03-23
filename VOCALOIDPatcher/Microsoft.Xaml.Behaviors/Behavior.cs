using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Animation;

namespace Microsoft.Xaml.Behaviors;

public abstract class Behavior<T> : Behavior where T : DependencyObject
{
	protected new T AssociatedObject => (T)base.AssociatedObject;

	protected Behavior()
		: base(typeof(T))
	{
	}
}
public abstract class Behavior : Animatable, IAttachedObject
{
	private Type associatedType;

	private DependencyObject associatedObject;

	protected Type AssociatedType
	{
		get
		{
			ReadPreamble();
			return associatedType;
		}
	}

	protected DependencyObject AssociatedObject
	{
		get
		{
			ReadPreamble();
			return associatedObject;
		}
	}

	DependencyObject IAttachedObject.AssociatedObject => AssociatedObject;

	internal event EventHandler AssociatedObjectChanged;

	internal Behavior(Type associatedType)
	{
		this.associatedType = associatedType;
	}

	protected virtual void OnAttached()
	{
	}

	protected virtual void OnDetaching()
	{
	}

	protected override Freezable CreateInstanceCore()
	{
		return (Freezable)Activator.CreateInstance(GetType());
	}

	private void OnAssociatedObjectChanged()
	{
		if (this.AssociatedObjectChanged != null)
		{
			this.AssociatedObjectChanged(this, new EventArgs());
		}
	}

	public void Attach(DependencyObject dependencyObject)
	{
		if (dependencyObject != AssociatedObject)
		{
			if (AssociatedObject != null)
			{
				throw new InvalidOperationException(ExceptionStringTable.CannotHostBehaviorMultipleTimesExceptionMessage);
			}
			if (dependencyObject != null && !AssociatedType.IsAssignableFrom(dependencyObject.GetType()))
			{
				throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, ExceptionStringTable.TypeConstraintViolatedExceptionMessage, GetType().Name, dependencyObject.GetType().Name, AssociatedType.Name));
			}
			WritePreamble();
			associatedObject = dependencyObject;
			WritePostscript();
			OnAssociatedObjectChanged();
			OnAttached();
		}
	}

	public void Detach()
	{
		OnDetaching();
		WritePreamble();
		associatedObject = null;
		WritePostscript();
		OnAssociatedObjectChanged();
	}
}
