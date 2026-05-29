// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.Xaml.Behaviors.Core;

/// <summary>
///     An action that will remove the targeted element from the tree when invoked.
/// </summary>
/// <remarks>
///     This action may fail. The action understands how to remove elements from common parents but not from custom
///     collections or direct manipulation
///     of the visual tree.
/// </remarks>
public class RemoveElementAction : TargetedTriggerAction<FrameworkElement>
{
    protected override void Invoke(object parameter)
    {
        if (AssociatedObject != null && Target != null)
        {
            var parent = Target.Parent;

            var panel = parent as Panel;
            if (panel != null)
            {
                panel.Children.Remove(Target);
                return;
            }

            var contentControl = parent as ContentControl;
            if (contentControl != null)
            {
                if (contentControl.Content == Target) contentControl.Content = null;
                return;
            }

            var itemsControl = parent as ItemsControl;
            if (itemsControl != null)
            {
                itemsControl.Items.Remove(Target);
                return;
            }

            var page = parent as Page;
            if (page != null)
            {
                if (page.Content == Target) page.Content = null;
                return;
            }

            var decorator = parent as Decorator;
            if (decorator != null)
            {
                if (decorator.Child == Target) decorator.Child = null;
                return;
            }

            if (parent != null)
                throw new InvalidOperationException(ExceptionStringTable.UnsupportedRemoveTargetExceptionMessage);
        }
    }
}
