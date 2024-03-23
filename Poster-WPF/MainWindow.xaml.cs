using Microsoft.Win32;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Poster;

/// <summary>
/// MainWindow.xaml 的交互逻辑
/// </summary>
public partial class MainWindow : Window
{
	private ComboBox methodSelector, contentTypeSelector;
	private readonly RequestModel _requestModel = new();
	private readonly ResponseModel _responseModel = new();
	private Progress<double> _progress;

	protected CancellationTokenSource _sendButtonCTS;
	private bool _disposedValue;

	public MainWindow()
	{
		InitializeComponent();
		DataContext = this;
		requestPanel.DataContext = _requestModel;
		responsePanel.DataContext = _responseModel;
		_progress = new(value =>
		{
			progressBar.Value = value;
			if (value < 100)
			{
				ShowHint($"Receiving {value:00.00}%");
			}
			else
			{
				ShowHint("Done.");
			}
		});
	}

	protected override void OnContentRendered(EventArgs e)
	{
		base.OnContentRendered(e);

		methodSelector = this.FindUid(nameof(methodSelector)) as ComboBox;
		contentTypeSelector = this.FindUid(nameof(contentTypeSelector)) as ComboBox;

		methodSelector.ItemsSource = Constants.HttpMethods;
		methodSelector.SelectedIndex = 0;
		contentTypeSelector.ItemsSource = Constants.ContentTypes;
		contentTypeSelector.SelectedIndex = 0;

		urlInput.Focus();
	}

	private async void OnSendClicked(object sender, RoutedEventArgs e) => await StartSend();
	private async void OnUrlTextKeyPressed(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter)
		{
			await StartSend();
		}
	}

	private void OnUrlChanged(object sender, TextChangedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(urlInput.Text))
			urlText.Text = "URL";
		else if (!urlInput.Text.StartsWith("http://") && !urlInput.Text.StartsWith("https://"))
			urlText.Text = "http://" + urlInput.Text;
		else
			urlText.Text = urlInput.Text;
	}

	private void OnFileDragOver(object sender, DragEventArgs e)
	{
		e.Handled = true;
		if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
			if (files is not null && files.Length == 1)
			{
				e.Effects = DragDropEffects.Copy;
				ShowHint("Drop to upload.");
				return;
			}
			ShowHint("Only one file is allowed.");
		}
		else
		{
			ShowHint("Only files are allowed.");
		}
		e.Effects = DragDropEffects.None;
	}

	private void OnFileDrop(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
			return;
		string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
		inputFilePath.Text = files[0];
		ShowHint("File selected.");
	}

	private void OnFilePickerClicked(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new();
		if (openFileDialog.ShowDialog(this) == true)
		{
			inputFilePath.Text = openFileDialog.FileName;
		}
	}

	private void OnFileOpenClicked(object sender, RoutedEventArgs e)
	{
		Process.Start(_responseModel.TempFilePath);
	}

	private void OnFolderOpenClicked(object sender, RoutedEventArgs e)
	{
		Process.Start(_responseModel.TempFolder.FullName);
	}

	private void OnFileSaveClicked(object sender, RoutedEventArgs e)
	{
		var ext = System.IO.Path.GetExtension(_responseModel.RealFileName);
		SaveFileDialog saveFileDialog = new()
		{
			FileName = _responseModel.RealFileName,
			Filter = $"*{ext}|{ext}|All files|*",
			// Title = "Save file",
		};
		if (saveFileDialog.ShowDialog(this) == true)
		{
			File.Copy(_responseModel.TempFilePath, saveFileDialog.FileName, true);
		}
	}

	private async Task StartSend()
	{
		_sendButtonCTS?.Cancel();
		_sendButtonCTS = new();
		statusBar.Text = statusText.Text = "Sending...";
		statusText.Foreground = new SolidColorBrush(Colors.Gray);
		ClearResponse();
		try
		{
			await SendAsync(_sendButtonCTS.Token);
		}
		catch (OperationCanceledException)
		{
			ShowHint("Cancelled.");
			_responseModel?.ResponseStream?.Dispose();
		}
	}

	private void ClearResponse()
	{
		textResponse.Text = string.Empty;
		imageResponse.Source = null;
		fileResponsePath.Text = string.Empty;
		progressBar.Value = 0;
	}

	private string DumpHeaders(HttpContentHeaders headers) =>
		string.Join("\n", headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}"));

	private void MarkError()
	{
		statusText.Foreground = new SolidColorBrush(Colors.Red);
		ShowHint("Error");
	}

	private string ShowHint(string hint)
	{
		string original = statusBar.Text;
		statusBar.Text = hint;
		return original;
	}

	#region Models
	public class RequestModel : INotifyPropertyChanged
	{
		private HttpContentType _requestType;

		public event PropertyChangedEventHandler PropertyChanged;

		public RequestModel()
		{
			RequestHeaders.CollectionChanged +=
				(sender, e) => PropertyChanged?.Invoke(this, new(nameof(RequestHeaders)));
		}

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
	#endregion
}
