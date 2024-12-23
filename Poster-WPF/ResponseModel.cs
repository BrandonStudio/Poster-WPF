#nullable enable

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
	private MemoryStream? _responseStream;
	private bool _streamSaved = false;
	private bool _fileAvailable = false;

	private event PropertyChangedEventHandler? _propertyChanged;

	public event PropertyChangedEventHandler? PropertyChanged
	{
		add { _propertyChanged += value; }
		remove { _propertyChanged -= value; }
	}

	public HttpContentType ResponseType
	{
		get => _responseType;
		set
		{
			_responseType = value;
			_propertyChanged?.Invoke(this, new(nameof(ResponseType)));
		}
	}

	public bool StreamSaved
	{
		get => _streamSaved;
		set
		{
			_streamSaved = value;
			_propertyChanged?.Invoke(this, new(nameof(StreamSaved)));
		}
	}

	public bool FileAvailable
	{
		get => _fileAvailable;
		set
		{
			_fileAvailable = value;
			_propertyChanged?.Invoke(this, new(nameof(FileAvailable)));
		}
	}

	public DirectoryInfo TempFolder { get; } = GetTempFolder();

	public MemoryStream? ResponseStream
	{
		get => _responseStream;
		set
		{
			_responseStream?.Dispose();
			_responseStream = value;
		}
	}

	internal HttpContentHeaders? ResponseContentHeaders { get; set; }
	internal string? RealFileName { get; set; }
	internal string? TempFilePath { get; set; }

	public void Reset()
	{
		_responseStream?.Dispose();
		_responseStream = null;
		StreamSaved = false;
		FileAvailable = false;
		ResponseContentHeaders = null;
		RealFileName = null;
		TempFilePath = null;
	}

	static DirectoryInfo GetTempFolder()
	{
		string tempPath = Path.GetTempPath();
		string tempFolderName = Path.GetRandomFileName();
		string tempFolderPath = Path.Combine(tempPath, tempFolderName);
		return Directory.CreateDirectory(tempFolderPath);
	}

	#region Dispose
	private bool _disposedValue;

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
