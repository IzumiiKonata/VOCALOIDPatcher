using System.Windows;

namespace Microsoft.Xaml.Behaviors;

public interface IAttachedObject
{
	DependencyObject AssociatedObject { get; }

	void Attach(DependencyObject dependencyObject);

	void Detach();
}
