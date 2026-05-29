// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System.Diagnostics;
using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

/// <summary>
///     An action that will launch a process to open a file or Uri. For files, this action will launch the default program
///     for the given file extension. A Uri will open in a web browser.
/// </summary>
public class LaunchUriOrFileAction : TriggerAction<DependencyObject>
{
    public static readonly DependencyProperty PathProperty =
        DependencyProperty.Register("Path", typeof(string), typeof(LaunchUriOrFileAction));

    /// <summary>
    ///     The file or Uri to open.
    /// </summary>
    public string Path
    {
        get => (string)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <summary>
    ///     This method is called when some criteria is met and the action is invoked.
    /// </summary>
    /// <param name="parameter"></param>
    protected override void Invoke(object parameter)
    {
        if (AssociatedObject != null && !string.IsNullOrEmpty(Path))
        {
            var processStartInfo = new ProcessStartInfo(Path)
            {
                UseShellExecute = true
            };
            Process.Start(processStartInfo);
        }
    }
}
