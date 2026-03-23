using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace Microsoft.Xaml.Behaviors;

[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
[DebuggerNonUserCode]
[CompilerGenerated]
internal class ExceptionStringTable
{
	private static ResourceManager resourceMan;

	private static CultureInfo resourceCulture;

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static ResourceManager ResourceManager
	{
		get
		{
			if (resourceMan == null)
			{
				resourceMan = new ResourceManager("Microsoft.Xaml.Behaviors.ExceptionStringTable", typeof(ExceptionStringTable).Assembly);
			}
			return resourceMan;
		}
	}

	[EditorBrowsable(EditorBrowsableState.Advanced)]
	internal static CultureInfo Culture
	{
		get
		{
			return resourceCulture;
		}
		set
		{
			resourceCulture = value;
		}
	}

	internal static string CallMethodActionValidMethodNotFoundExceptionMessage => ResourceManager.GetString("CallMethodActionValidMethodNotFoundExceptionMessage", resourceCulture);

	internal static string CannotHostBehaviorCollectionMultipleTimesExceptionMessage => ResourceManager.GetString("CannotHostBehaviorCollectionMultipleTimesExceptionMessage", resourceCulture);

	internal static string CannotHostBehaviorMultipleTimesExceptionMessage => ResourceManager.GetString("CannotHostBehaviorMultipleTimesExceptionMessage", resourceCulture);

	internal static string CannotHostTriggerActionMultipleTimesExceptionMessage => ResourceManager.GetString("CannotHostTriggerActionMultipleTimesExceptionMessage", resourceCulture);

	internal static string CannotHostTriggerCollectionMultipleTimesExceptionMessage => ResourceManager.GetString("CannotHostTriggerCollectionMultipleTimesExceptionMessage", resourceCulture);

	internal static string CannotHostTriggerMultipleTimesExceptionMessage => ResourceManager.GetString("CannotHostTriggerMultipleTimesExceptionMessage", resourceCulture);

	internal static string ChangePropertyActionAmbiguousAdditionOperationExceptionMessage => ResourceManager.GetString("ChangePropertyActionAmbiguousAdditionOperationExceptionMessage", resourceCulture);

	internal static string ChangePropertyActionCannotAnimateTargetTypeExceptionMessage => ResourceManager.GetString("ChangePropertyActionCannotAnimateTargetTypeExceptionMessage", resourceCulture);

	internal static string ChangePropertyActionCannotFindPropertyNameExceptionMessage => ResourceManager.GetString("ChangePropertyActionCannotFindPropertyNameExceptionMessage", resourceCulture);

	internal static string ChangePropertyActionCannotIncrementAnimatedPropertyChangeExceptionMessage => ResourceManager.GetString("ChangePropertyActionCannotIncrementAnimatedPropertyChangeExceptionMessage", resourceCulture);

	internal static string ChangePropertyActionCannotIncrementWriteOnlyPropertyExceptionMessage => ResourceManager.GetString("ChangePropertyActionCannotIncrementWriteOnlyPropertyExceptionMessage", resourceCulture);

	internal static string ChangePropertyActionCannotSetValueExceptionMessage => ResourceManager.GetString("ChangePropertyActionCannotSetValueExceptionMessage", resourceCulture);

	internal static string ChangePropertyActionPropertyIsReadOnlyExceptionMessage => ResourceManager.GetString("ChangePropertyActionPropertyIsReadOnlyExceptionMessage", resourceCulture);

	internal static string CommandDoesNotExistOnBehaviorWarningMessage => ResourceManager.GetString("CommandDoesNotExistOnBehaviorWarningMessage", resourceCulture);

	internal static string DataStateBehaviorStateNameNotFoundExceptionMessage => ResourceManager.GetString("DataStateBehaviorStateNameNotFoundExceptionMessage", resourceCulture);

	internal static string DefaultTriggerAttributeInvalidTriggerTypeSpecifiedExceptionMessage => ResourceManager.GetString("DefaultTriggerAttributeInvalidTriggerTypeSpecifiedExceptionMessage", resourceCulture);

	internal static string DuplicateItemInCollectionExceptionMessage => ResourceManager.GetString("DuplicateItemInCollectionExceptionMessage", resourceCulture);

	internal static string EventTriggerBaseInvalidEventExceptionMessage => ResourceManager.GetString("EventTriggerBaseInvalidEventExceptionMessage", resourceCulture);

	internal static string EventTriggerCannotFindEventNameExceptionMessage => ResourceManager.GetString("EventTriggerCannotFindEventNameExceptionMessage", resourceCulture);

	internal static string GoToStateActionTargetHasNoStateGroups => ResourceManager.GetString("GoToStateActionTargetHasNoStateGroups", resourceCulture);

	internal static string InvalidLeftOperand => ResourceManager.GetString("InvalidLeftOperand", resourceCulture);

	internal static string InvalidOperands => ResourceManager.GetString("InvalidOperands", resourceCulture);

	internal static string InvalidRightOperand => ResourceManager.GetString("InvalidRightOperand", resourceCulture);

	internal static string RetargetedTypeConstraintViolatedExceptionMessage => ResourceManager.GetString("RetargetedTypeConstraintViolatedExceptionMessage", resourceCulture);

	internal static string TypeConstraintViolatedExceptionMessage => ResourceManager.GetString("TypeConstraintViolatedExceptionMessage", resourceCulture);

	internal static string UnableToResolveTargetNameWarningMessage => ResourceManager.GetString("UnableToResolveTargetNameWarningMessage", resourceCulture);

	internal static string UnsupportedRemoveTargetExceptionMessage => ResourceManager.GetString("UnsupportedRemoveTargetExceptionMessage", resourceCulture);

	internal ExceptionStringTable()
	{
	}
}
