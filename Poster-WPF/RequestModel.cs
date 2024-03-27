using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poster;

public class RequestModel : INotifyPropertyChanged
{
	private HttpContentType _requestType = HttpContentType.Text;

	public event PropertyChangedEventHandler PropertyChanged;

	public RequestModel()
	{
		RequestHeaders.CollectionChanged +=
			(sender, e) => PropertyChanged?.Invoke(this, new(nameof(RequestHeaders)));
	}

	public void NotifyChange(string propertyName) => PropertyChanged?.Invoke(this, new(propertyName));

	public HttpContentType RequestType
	{
		get => _requestType;
		set
		{
			_requestType = value;
			PropertyChanged?.Invoke(this, new(nameof(RequestType)));
		}
	}

	public ObservableCollection<RequestHeader> RequestHeaders { get; set; } = [];

	/// <remarks>Do NOT try using <c>struct</c>!</remarks>
	public class RequestHeader
	{
		public string Name { get; set; }
		public string Value { get; set; }

		public RequestHeader() { }
		public RequestHeader(string name, string value)
		{
			Name = name;
			Value = value;
		}

		public static RequestHeader[] ParseHeaders(string text)
		{
			var headers = text.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
			return headers.Select(header =>
			{
				var parts = header.Split([':'], 2);
				return new RequestHeader(parts[0], parts[1]);
			}).ToArray();
		}
	}
}

