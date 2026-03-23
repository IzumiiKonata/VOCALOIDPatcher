using System.Windows;

namespace Microsoft.Xaml.Behaviors.Core;

[DefaultTrigger(typeof(UIElement), typeof(EventTrigger), "Loaded")]
public class SetDataStoreValueAction : ChangePropertyAction
{
}
