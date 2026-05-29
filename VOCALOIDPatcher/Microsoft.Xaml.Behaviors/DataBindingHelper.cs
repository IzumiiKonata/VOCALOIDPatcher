// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;

namespace Microsoft.Xaml.Behaviors;

/// <summary>
///     Helper class for managing binding expressions on dependency objects.
/// </summary>
internal static class DataBindingHelper
{
    private static readonly Dictionary<Type, IList<DependencyProperty>> DependenciesPropertyCache = new();

    /// <summary>
    ///     Ensure that all DP on an action with binding expressions are
    ///     up to date. DataTrigger fires during data binding phase. Since
    ///     actions are children of the trigger, any bindings on the action
    ///     may not be up-to-date. This routine is called before the action
    ///     is invoked in order to guarantee that all bindings are up-to-date
    ///     with the most current data.
    /// </summary>
    public static void EnsureDataBindingUpToDateOnMembers(DependencyObject dpObject)
    {
        IList<DependencyProperty> dpList = null;

        if (!DependenciesPropertyCache.TryGetValue(dpObject.GetType(), out dpList))
        {
            dpList = new List<DependencyProperty>();
            var type = dpObject.GetType();

            while (type != null)
            {
                var fieldInfos = type.GetFields();

                foreach (var fieldInfo in fieldInfos)
                    if (fieldInfo.IsPublic &&
                        fieldInfo.FieldType == typeof(DependencyProperty))
                    {
                        var property = fieldInfo.GetValue(null) as DependencyProperty;
                        if (property != null) dpList.Add(property);
                    }

                type = type.BaseType;
            }

            // Cache the list of DP for performance gain
            DependenciesPropertyCache[dpObject.GetType()] = dpList;
        }

        if (dpList == null) return;

        foreach (var property in dpList) EnsureBindingUpToDate(dpObject, property);
    }

    /// <summary>
    ///     Ensures that all binding expression on actions are up to date
    /// </summary>
    public static void EnsureDataBindingOnActionsUpToDate(TriggerBase<DependencyObject> trigger)
    {
        // Update the bindings on the actions. 
        foreach (var action in trigger.Actions) EnsureDataBindingUpToDateOnMembers(action);
    }

    /// <summary>
    ///     This helper function ensures that, if a dependency property on a dependency object
    ///     has a binding expression, the binding expression is up-to-date.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="dp"></param>
    public static void EnsureBindingUpToDate(DependencyObject target, DependencyProperty dp)
    {
        var binding = BindingOperations.GetBindingExpression(target, dp);
        if (binding != null) binding.UpdateTarget();
    }
}
