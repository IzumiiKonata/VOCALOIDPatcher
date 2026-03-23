using System;

namespace Microsoft.Xaml.Behaviors;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CustomPropertyValueEditorAttribute : Attribute
{
	public CustomPropertyValueEditor CustomPropertyValueEditor { get; private set; }

	public CustomPropertyValueEditorAttribute(CustomPropertyValueEditor customPropertyValueEditor)
	{
		CustomPropertyValueEditor = customPropertyValueEditor;
	}
}
