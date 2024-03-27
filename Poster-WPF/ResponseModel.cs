using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Poster;

public class ResponseModel : INotifyPropertyChanged, IDisposable
{
	private HttpContentType _responseType = HttpContentType.Text;
	private MemoryStream _responseStream;
	private bool _disposedValue;

	public event PropertyChangedEventHandler PropertyChanged;

	public HttpContentType ResponseType
	{
		get => _responseType;
		set
		{
			_responseType = value;
			PropertyChanged?.Invoke(this, new(nameof(ResponseType)));
		}
	}

	public DirectoryInfo TempFolder { get; } = GetTempFolder();

	public MemoryStream ResponseStream
	{
		get => _responseStream;
		set
		{
			_responseStream?.Dispose();
			_responseStream = value;
		}
	}

	internal HttpContentHeaders ResponseContentHeaders { get; set; }
	internal string RealFileName { get; set; }
	internal string TempFilePath { get; set; }

	static DirectoryInfo GetTempFolder()
	{
		string tempPath = System.IO.Path.GetTempPath();
		string tempFolderName = System.IO.Path.GetRandomFileName();
		string tempFolderPath = System.IO.Path.Combine(tempPath, tempFolderName);
		return Directory.CreateDirectory(tempFolderPath);
	}

	#region Dispose
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_responseStream?.Dispose();
			}

			TempFolder?.Delete(true);
			_disposedValue = true;
		}
	}

	~ResponseModel()
	{
		Dispose(disposing: false);
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
