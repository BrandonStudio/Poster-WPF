#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Shell;

namespace Poster;

public class HttpContentTypeConverter : IValueConverter
{
	public static HttpContentTypeConverter Default { get; } = new();

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
	{
		if (targetType == typeof(string))
		{
			if (value is HttpContentType ct)
				return ct.ToString();
			return HttpContentType.File.ToString();
		}
		else if (targetType.IsValueType)
		{
			if (value is HttpContentType ct)
				return (int)Math.Log((int)ct, 2);
			return (int)Math.Log((int)HttpContentType.File, 2);
		}
		else
		{
			Debug.Assert(false);
			throw new NotSupportedException();
		}
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
	{
		Debug.Assert(targetType == typeof(HttpContentType));
		if (value is string s)
		{
			if (!Enum.TryParse(s, out HttpContentType ct))
				ct = HttpContentType.File;
			return ct;
		}
		else if (value is ValueType)
			return (HttpContentType)(int)Math.Pow(2, (int)value);
		else
			return HttpContentType.File;
	}
}

public class ProgressStateConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
	{
		if (value is not bool b)
		{
			Debug.Assert(false);
			throw new NotSupportedException();
		}
		Debug.Assert(targetType == typeof(TaskbarItemProgressState));
		return b ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.Normal;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
	{
		if (value is not TaskbarItemProgressState s)
		{
			Debug.Assert(false);
			throw new NotSupportedException();
		}
		Debug.Assert(targetType == typeof(bool));
		return s switch
		{
			TaskbarItemProgressState.Indeterminate => true,
			_ => (object?)false,
		};
	}
}

public class RatioValueConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
	{
		if (parameter is string str && double.TryParse(str, out double ratio))
		{
			return value switch
			{
				double d => d * ratio,
				int i => i * ratio,
				_ => value,
			};
		}
		return value;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
	{
		throw new NotImplementedException();
	}
}
