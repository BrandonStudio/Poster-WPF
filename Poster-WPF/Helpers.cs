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
using System.Windows.Shell;

namespace Poster;

static partial class Helpers
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

	public static IEnumerable<RequestModel.RequestHeader> ToHeaders(this string text)
	{
		foreach (var header in text.Split(['\n'], StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = header.Split([':'], 2);
			if (parts.Length < 2)
			{
				yield return new(parts[0], string.Empty);
				continue;
			}
			var values = parts[1].Split([','], StringSplitOptions.RemoveEmptyEntries);
			foreach (var value in values)
				yield return new(parts[0], value.Trim());
		}
	}

	private static readonly string[] s_textCTs =
	[
		"application/json",
		"application/xml",
		"application/javascript",
		"application/hta",
		"application/x-ndjson",
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

[Flags]
public enum HttpContentType
{
	Text = 1,
	Image = 2,
	File = 4,
}
