using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Diagnostics;

namespace Poster;

static class Helpers
{
	public static UIElement FindUid(this DependencyObject parent, string uid)
	{
		var count = VisualTreeHelper.GetChildrenCount(parent);
		if (count == 0) return null;

		for (int i = 0; i < count; i++)
		{
			var el = VisualTreeHelper.GetChild(parent, i) as UIElement;
			if (el == null) continue;

			if (el.Uid == uid) return el;

			el = el.FindUid(uid);
			if (el != null) return el;
		}
		return null;
	}

	public static HttpContentType ToContentType(this string contentType)
	{
		if (contentType.StartsWith("image/"))
			return HttpContentType.Image;
		if (contentType.StartsWith("text/") ||
			s_textCTs.Contains(contentType) ||
			contentType.EndsWith("+xml") || contentType.EndsWith("+json"))
			return HttpContentType.Text;
		return HttpContentType.File;
	}

	private static readonly string[] s_textCTs =
	[
		"application/json",
		"application/xml",
		"application/javascript",
		"application/hta",
		"application/x-www-form-urlencoded",
		"application/x-javascript",
		"application/x-latex",
		"application/x-sh",
		"application/x-tex",
		"application/x-tex-info",
		"application/x-x509-ca-cert",
		"audio/x-mpegurl",
	];
}

public enum HttpContentType
{
	Text,
	Image,
	File,
}

public class HttpContentTypeConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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
				return (int)ct;
			return (int)HttpContentType.File;
		}
		else
		{
			Debug.Assert(false);
			throw new NotSupportedException();
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string s)
		{
			if (!Enum.TryParse(s, out HttpContentType ct))
				ct = HttpContentType.File;
			return ct;
		}
		else if (value is ValueType)
			return (HttpContentType)(int)value;
		else
			return HttpContentType.File;
	}
}
