using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Data;

namespace Microsoft.Xaml.Behaviors;

internal static class DataBindingHelper
{
	private static Dictionary<Type, IList<DependencyProperty>> DependenciesPropertyCache = new Dictionary<Type, IList<DependencyProperty>>();

	public static void EnsureDataBindingUpToDateOnMembers(DependencyObject dpObject)
	{
		IList<DependencyProperty> value = null;
		if (!DependenciesPropertyCache.TryGetValue(dpObject.GetType(), out value))
		{
			value = new List<DependencyProperty>();
			Type type = dpObject.GetType();
			while (type != null)
			{
				FieldInfo[] fields = type.GetFields();
				foreach (FieldInfo fieldInfo in fields)
				{
					if (fieldInfo.IsPublic && fieldInfo.FieldType == typeof(DependencyProperty) && fieldInfo.GetValue(null) is DependencyProperty item)
					{
						value.Add(item);
					}
				}
				type = type.BaseType;
			}
			DependenciesPropertyCache[dpObject.GetType()] = value;
		}
		if (value == null)
		{
			return;
		}
		foreach (DependencyProperty item2 in value)
		{
			EnsureBindingUpToDate(dpObject, item2);
		}
	}

	public static void EnsureDataBindingOnActionsUpToDate(TriggerBase<DependencyObject> trigger)
	{
		foreach (TriggerAction action in trigger.Actions)
		{
			EnsureDataBindingUpToDateOnMembers(action);
		}
	}

	public static void EnsureBindingUpToDate(DependencyObject target, DependencyProperty dp)
	{
		BindingOperations.GetBindingExpression(target, dp)?.UpdateTarget();
	}
}
