using System;
using System.Globalization;
using Microsoft.Xaml.Behaviors.Core;

namespace Microsoft.Xaml.Behaviors;

internal static class ComparisonLogic
{
	internal static bool EvaluateImpl(object leftOperand, ComparisonConditionType operatorType, object rightOperand)
	{
		bool result = false;
		if (leftOperand != null)
		{
			Type type = leftOperand.GetType();
			if (rightOperand != null)
			{
				rightOperand = TypeConverterHelper.DoConversionFrom(TypeConverterHelper.GetTypeConverter(type), rightOperand);
			}
		}
		IComparable comparable = leftOperand as IComparable;
		IComparable comparable2 = rightOperand as IComparable;
		if (comparable != null && comparable2 != null)
		{
			return EvaluateComparable(comparable, operatorType, comparable2);
		}
		switch (operatorType)
		{
		case ComparisonConditionType.Equal:
			result = object.Equals(leftOperand, rightOperand);
			break;
		case ComparisonConditionType.NotEqual:
			result = !object.Equals(leftOperand, rightOperand);
			break;
		case ComparisonConditionType.LessThan:
		case ComparisonConditionType.LessThanOrEqual:
		case ComparisonConditionType.GreaterThan:
		case ComparisonConditionType.GreaterThanOrEqual:
			if (comparable == null && comparable2 == null)
			{
				throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, ExceptionStringTable.InvalidOperands, (leftOperand != null) ? leftOperand.GetType().Name : "null", (rightOperand != null) ? rightOperand.GetType().Name : "null", operatorType.ToString()));
			}
			if (comparable == null)
			{
				throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, ExceptionStringTable.InvalidLeftOperand, (leftOperand != null) ? leftOperand.GetType().Name : "null", operatorType.ToString()));
			}
			throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, ExceptionStringTable.InvalidRightOperand, (rightOperand != null) ? rightOperand.GetType().Name : "null", operatorType.ToString()));
		}
		return result;
	}

	private static bool EvaluateComparable(IComparable leftOperand, ComparisonConditionType operatorType, IComparable rightOperand)
	{
		object obj = null;
		try
		{
			obj = Convert.ChangeType(rightOperand, leftOperand.GetType(), CultureInfo.CurrentCulture);
		}
		catch (FormatException)
		{
		}
		catch (InvalidCastException)
		{
		}
		if (obj == null)
		{
			return operatorType == ComparisonConditionType.NotEqual;
		}
		int num = leftOperand.CompareTo((IComparable)obj);
		bool result = false;
		switch (operatorType)
		{
		case ComparisonConditionType.Equal:
			result = num == 0;
			break;
		case ComparisonConditionType.GreaterThan:
			result = num > 0;
			break;
		case ComparisonConditionType.GreaterThanOrEqual:
			result = num >= 0;
			break;
		case ComparisonConditionType.LessThan:
			result = num < 0;
			break;
		case ComparisonConditionType.LessThanOrEqual:
			result = num <= 0;
			break;
		case ComparisonConditionType.NotEqual:
			result = num != 0;
			break;
		}
		return result;
	}
}
