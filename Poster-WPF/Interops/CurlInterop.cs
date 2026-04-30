#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Poster.Interops;

/// <summary>
/// Utilities for importing a request from a cURL command and exporting a request as a cURL command.
/// </summary>
public static class CurlInterop
{
	/// <summary>Represents an HTTP request parsed from a cURL command.</summary>
	public class CurlRequest
	{
		public string Url { get; }
		public string Method { get; }
		public IReadOnlyList<(string Name, string Value)> Headers { get; }
		public string? Body { get; }

		public CurlRequest(string url, string method, IReadOnlyList<(string Name, string Value)> headers, string? body)
		{
			Url = url;
			Method = method;
			Headers = headers;
			Body = body;
		}
	}

	/// <summary>
	/// Parses a cURL command string into a <see cref="CurlRequest"/>.
	/// Supports <c>-X</c>, <c>-H</c>, <c>-d</c>/<c>--data*</c>, <c>-u</c> flags
	/// and both space-separated and <c>--option=value</c> argument forms.
	/// Line continuations (<c>\</c> at end of line on POSIX, <c>^</c> on Windows CMD)
	/// are handled automatically.
	/// </summary>
	public static CurlRequest Parse(string? curlCommand)
	{
		if (string.IsNullOrWhiteSpace(curlCommand))
			return new CurlRequest(string.Empty, "GET", new List<(string, string)>(), null);

		var tokens = Tokenize(curlCommand);

		// Find and skip the leading "curl" token (tolerates paths like /usr/bin/curl or C:\curl.exe)
		int idx;
		for (idx = 0; idx < tokens.Count; idx++)
		{
			if (IsCurlToken(tokens[idx]))
				break;
		}
		if (idx < tokens.Count)
			idx++;  // skip the "curl" token itself
		else
			idx = 0;  // "curl" not found — treat all tokens as arguments

		string? url = null, method = null, body = null;
		var headers = new List<(string Name, string Value)>();

		for (; idx < tokens.Count;)
		{
			string token = tokens[idx];
			idx++;

			if (MatchFlag(token, tokens, ref idx, "-X", "--request", out string? v))
			{
				method = v;
			}
			else if (MatchFlag(token, tokens, ref idx, "-H", "--header", out v) && v != null)
			{
				int colon = v.IndexOf(':');
				if (colon > 0)
					headers.Add((v.Substring(0, colon).Trim(), v.Substring(colon + 1).Trim()));
			}
			else if (MatchFlag(token, tokens, ref idx, "-d", "--data", out v)
				|| MatchFlag(token, tokens, ref idx, "--data-raw", "--data-binary", out v)
				|| MatchFlag(token, tokens, ref idx, "--data-ascii", "--data-urlencode", out v))
			{
				// @ prefix means "read from file" — skip, cannot import
				if (v != null && !v.StartsWith("@"))
					body = (body == null) ? v : body + "&" + v;
			}
			else if (MatchFlag(token, tokens, ref idx, "-u", "--user", out v) && v != null)
			{
				// Convert user:password to Basic Authorization header
				var bytes = Encoding.UTF8.GetBytes(v);
				headers.Add(("Authorization", "Basic " + Convert.ToBase64String(bytes)));
			}
			else if (!token.StartsWith("-"))
			{
				// Bare token with no leading dash — treat as URL
				url = token;
			}
			// Ignore value-less flags: -L, -s, -v, -k, --compressed, --insecure, etc.
			// For combined short flags like -sL, also ignore (they carry no value we need).
		}

		url ??= string.Empty;
		method = (method ?? (body != null ? "POST" : "GET")).ToUpperInvariant();

		return new CurlRequest(url, method, headers, body);
	}

	/// <summary>
	/// Generates a cURL command string from the given request parameters.
	/// </summary>
	/// <param name="method">HTTP method, e.g. <c>POST</c>.</param>
	/// <param name="url">Full request URL.</param>
	/// <param name="headers">Additional request headers (should not include Content-Type).</param>
	/// <param name="body">Request body text, or <c>null</c> for requests without a body.</param>
	/// <param name="contentType">Content-Type value; included only when <paramref name="body"/> is non-empty.</param>
	public static string Export(
		string method,
		string url,
		IEnumerable<RequestModel.RequestHeader> headers,
		string? body,
		string? contentType)
	{
		var sb = new StringBuilder("curl");

		// Omit -X for GET (the default)
		if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
			sb.Append($" -X {method}");

		// Custom (non-standard) methods are not supported by HTTP/2; force HTTP/1.1.
		// A method is considered standard if it appears in the application's known method list.
		bool isCustomMethod = !Constants.HttpMethods.Any(
			m => string.Equals(m.Method, method, StringComparison.OrdinalIgnoreCase));
		if (isCustomMethod)
			sb.Append(" --http1.1");

		sb.Append($" \"{EscapeDoubleQuoted(url)}\"");

		// Content-Type only makes sense when there is a body
		if (!string.IsNullOrEmpty(body) && !string.IsNullOrEmpty(contentType))
			sb.Append($" \\\n  -H \"Content-Type: {EscapeDoubleQuoted(contentType)}\"");

		foreach (var header in headers)
		{
			if (string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
				continue;
			sb.Append($" \\\n  -H \"{EscapeDoubleQuoted(header.Name)}: {EscapeDoubleQuoted(header.Value)}\"");
		}

		if (!string.IsNullOrEmpty(body))
		{
			// Prefer single-quoted data (no escaping needed inside single quotes in shells)
			// unless the body itself contains a single quote.
			if (!body.Contains("'"))
				sb.Append($" \\\n  --data '{body}'");
			else
				sb.Append($" \\\n  --data \"{EscapeDoubleQuoted(body)}\"");
		}

		return sb.ToString();
	}

	// ---------------------------------------------------------------------------
	// Helpers
	// ---------------------------------------------------------------------------

	/// <summary>
	/// Matches <paramref name="token"/> against a short option (<paramref name="shortOpt"/>) and a
	/// long option (<paramref name="longOpt"/>), consuming the next token as the value when matched.
	/// Also handles the <c>--long-opt=value</c> form.
	/// </summary>
	private static bool MatchFlag(
		string token, List<string> tokens, ref int idx,
		string shortOpt, string longOpt, out string? value)
	{
		if (token == shortOpt || token == longOpt)
		{
			value = idx < tokens.Count ? tokens[idx++] : null;
			return true;
		}
		if (token.StartsWith(longOpt + "="))
		{
			value = token.Substring(longOpt.Length + 1);
			return true;
		}
		value = null;
		return false;
	}

	private static string EscapeDoubleQuoted(string? s)
		=> (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`").Replace("!", "\\!");

	// Characters that can be backslash-escaped inside a double-quoted shell string.
	private const string DoubleQuoteEscapable = "\"\\$`!";

	/// <summary>
	/// Splits a shell command into individual tokens, correctly handling
	/// single-quoted strings, double-quoted strings (with backslash escapes),
	/// and backslash line-continuations.
	/// </summary>
	private static List<string> Tokenize(string command)
	{
		// Normalise line endings, then collapse backslash-newline continuations
		command = command.Replace("\r\n", "\n").Replace("\r", "\n");
		command = Regex.Replace(command, @"\\\n[ \t]*", " ");

		var tokens = new List<string>();
		var current = new StringBuilder();
		bool inDouble = false, inSingle = false, escape = false;

		for (int i = 0; i < command.Length; i++)
		{
			char c = command[i];

			if (escape)
			{
				current.Append(c);
				escape = false;
				continue;
			}

			if (inDouble)
			{
				// Inside double quotes: backslash only escapes ", \, $, `, !
				if (c == '\\' && i + 1 < command.Length && DoubleQuoteEscapable.IndexOf(command[i + 1]) >= 0)
					escape = true;
				else if (c == '"')
					inDouble = false;
				else
					current.Append(c);
			}
			else if (inSingle)
			{
				// Inside single quotes: no escaping whatsoever (POSIX)
				if (c == '\'')
					inSingle = false;
				else
					current.Append(c);
			}
			else
			{
				if (c == '\\')
					escape = true;
				else if (c == '"')
					inDouble = true;
				else if (c == '\'')
					inSingle = true;
				else if (char.IsWhiteSpace(c))
				{
					if (current.Length > 0)
					{
						tokens.Add(current.ToString());
						current.Clear();
					}
				}
				else
					current.Append(c);
			}
		}

		if (current.Length > 0)
			tokens.Add(current.ToString());

		return tokens;
	}
}
