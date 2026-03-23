using System;
using System.ComponentModel;
using System.Globalization;

namespace Microsoft.Xaml.Behaviors;

internal static class TypeConverterHelper
{
	internal static object DoConversionFrom(TypeConverter converter, object value)
	{
		object result = value;
		try
		{
			if (converter != null && value != null && converter.CanConvertFrom(value.GetType()))
			{
				result = converter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
			}
		}
		catch (Exception e)
		{
			if (!ShouldEatException(e))
			{
				throw;
			}
		}
		return result;
	}

	private static bool ShouldEatException(Exception e)
	{
		bool flag = false;
		if (e.InnerException != null)
		{
			flag |= ShouldEatException(e.InnerException);
		}
		return flag || e is FormatException;
	}

	internal static TypeConverter GetTypeConverter(Type type)
	{
		return TypeDescriptor.GetConverter(type);
	}
}
