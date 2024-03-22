using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Poster
{
	internal static class Constants
	{
		internal static readonly string[] ContentTypes =
		[
			"text/plain",
			"text/html",
			"application/json",
			"application/xml",
			"application/x-www-form-urlencoded",
			"image/jpeg",
			"image/png",
			"image/gif",
			"image/bmp",
			"image/webp",
			"application/octet-stream",
		];

		internal static readonly HttpMethod[] HttpMethods = [
			HttpMethod.Get,
			HttpMethod.Post,
			HttpMethod.Put,
			HttpMethod.Delete,
			HttpMethod.Head,
			HttpMethod.Options,
			new("PATCH"),
			HttpMethod.Trace
		];
	}
}
