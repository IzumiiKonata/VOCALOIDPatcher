using System.Windows;

namespace Microsoft.Xaml.Behaviors.Layout;

public sealed class FluidMoveSetTagBehavior : FluidMoveBehaviorBase
{
	internal override void UpdateLayoutTransitionCore(FrameworkElement child, FrameworkElement root, object tag, TagData newTagData)
	{
		if (!FluidMoveBehaviorBase.TagDictionary.TryGetValue(tag, out var value))
		{
			value = new TagData();
			FluidMoveBehaviorBase.TagDictionary.Add(tag, value);
		}
		value.ParentRect = newTagData.ParentRect;
		value.AppRect = newTagData.AppRect;
		value.Parent = newTagData.Parent;
		value.Child = newTagData.Child;
		value.Timestamp = newTagData.Timestamp;
	}
}
