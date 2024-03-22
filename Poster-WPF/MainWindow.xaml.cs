using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace Poster
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		private ComboBox methodSelector, contentTypeSelector;
		private readonly RequestModel _requestModel = new();
		private readonly ResponseModel _responseModel = new();

		protected CancellationTokenSource _sendButtonCTS;
		private DirectoryInfo _tempFileFolder;

		readonly IEnumerable<HttpMethod> _httpMethods =
			typeof(HttpMethod).GetProperties()
			.Where(p => p.PropertyType == typeof(HttpMethod))
			.Select(p => (HttpMethod)p.GetValue(null));

		public MainWindow()
		{
			InitializeComponent();
			DataContext = this;
			requestPanel.DataContext = _requestModel;
			responsePanel.DataContext = _responseModel;
		}

		protected override void OnContentRendered(EventArgs e)
		{
			base.OnContentRendered(e);

			methodSelector = this.FindUid(nameof(methodSelector)) as ComboBox;
			contentTypeSelector = this.FindUid(nameof(contentTypeSelector)) as ComboBox;

			methodSelector.ItemsSource = _httpMethods;
			methodSelector.SelectedIndex = 0;
			contentTypeSelector.ItemsSource = Constants.ContentTypes;
			contentTypeSelector.SelectedIndex = 0;

			urlInput.Focus();
		}

		private async void StartSend(object sender, RoutedEventArgs e)
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
				// Do nothing.
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

		/// <exception cref="OperationCanceledException">The operation was canceled.</exception>
		private async Task SendAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			HttpClient client = new()
			{
				Timeout = TimeSpan.FromSeconds(120)
			};
			client.DefaultRequestHeaders.UserAgent.Add(
				new("Poster", Assembly.GetExecutingAssembly().GetName().Version.ToString()));
			HttpContent content = null;
			switch (methodSelector.SelectedValue.ToString())
			{
				case "POST":
				case "PUT":
				case "PATCH":
				case "DELETE":
					content = GetContent();
					break;
				default:
					break;
			}
			try
			{
				var message = new HttpRequestMessage((HttpMethod)methodSelector.SelectedItem, urlText.Text)
				{
					Content = content,
				};
				foreach (var header in _requestModel.RequestHeaders)
				{
					var name = header.Name;
					var value = header.Value;
					message.Headers.Add(name, value);
				}
				using var result = await client.SendAsync(message, cancellationToken); // TODO: param[1]

				statusText.Text = $"{(int)result.StatusCode} {result.StatusCode}";
				if (!result.IsSuccessStatusCode)
				{
					MarkError();
				}
				else
				{
					statusText.Foreground = new SolidColorBrush(Colors.Green);
					statusBar.Text = "Success";
				}

				using var responseStream = await result.Content.ReadAsStreamAsync();
				_responseModel.ResponseContentHeaders = result.Content.Headers;
				string contentType = result.Content.Headers.ContentType.MediaType;
				_responseModel.ResponseType = contentType.ToContentType();
				switch (_responseModel.ResponseType)
				{
					case HttpContentType.Text:
						using (var reader = new StreamReader(responseStream))
							textResponse.Text = reader.ReadToEnd();
						break;
					case HttpContentType.Image:
						var bitmap = new BitmapImage();
						imageResponse.Source = bitmap;
						bitmap.BeginInit();
						bitmap.StreamSource = responseStream;
						bitmap.EndInit();
						break;
					case HttpContentType.File:
					default:
						string fileName = result.Content.Headers.ContentDisposition.FileName;
						if (string.IsNullOrWhiteSpace(fileName))
						{
							var path = urlText.Text.Split('/').Last();
							fileName = System.IO.Path.GetFileName(path);
						}
						_responseModel.RealFileName = fileName;
						string ext = System.IO.Path.GetExtension(fileName);
						if (string.IsNullOrEmpty(ext))
						{
							ext = ".tmp";
						}
						string tempPath = System.IO.Path.GetTempPath();
						string tempFolderName = System.IO.Path.GetRandomFileName();
						string tempFolderPath = System.IO.Path.Combine(tempPath, tempFolderName);
						_tempFileFolder = Directory.CreateDirectory(tempFolderPath);
						string tempFilePath = System.IO.Path.GetRandomFileName();
						tempFilePath = System.IO.Path.ChangeExtension(tempFilePath, ext);
						using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
						{
							await responseStream.CopyToAsync(fileStream);
						}
						fileResponsePath.Text = tempFilePath;
						break;
				}
			}
			catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
			{
				MarkError();
				statusText.Text = statusBar.Text = ex.Message;
			}
		}

		private void ClearResponse()
		{
			textResponse.Text = string.Empty;
			imageResponse.Source = null;
			fileResponsePath.Text = string.Empty;
			try
			{
				_tempFileFolder?.Delete(true);
			}
			catch (Exception)
			{
				// Do nothing.
			}
			finally
			{
				_tempFileFolder = null;
			}
		}

		private HttpContent GetContent()
		{
			HttpContent content = null;
			switch (_requestModel.RequestType)
			{
				case HttpContentType.Text:
					if (!string.IsNullOrWhiteSpace(textInput.Text))
						content = new StringContent(textInput.Text, null, mediaType: contentTypeSelector.Text);
					break;
				case HttpContentType.Image:
					var source = imageInput.Source as BitmapSource;
					if (source is BitmapImage bitmap)
					{
						content = new StreamContent(bitmap.StreamSource);
					}
					else
					{
						var encoder = new PngBitmapEncoder(); // Use JpegBitmapEncoder or other encoders according to your needs
						encoder.Frames.Add(BitmapFrame.Create(source));

						var stream = new MemoryStream();
						encoder.Save(stream);
						stream.Position = 0; // Reset stream position

						content = new StreamContent(stream);
					}
					break;
				case HttpContentType.File:
				default:
					content = new StreamContent(File.OpenRead(inputFilePath.Text));
					break;
			}
			return content;
		}

		private void MarkError()
		{
			statusText.Foreground = new SolidColorBrush(Colors.Red);
			statusBar.Text = "Error";
		}

		public class RequestModel : INotifyPropertyChanged
		{
			private HttpContentType _requestType;

			public event PropertyChangedEventHandler PropertyChanged;

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

			public struct RequestHeader
			{
				public string Name { get; set; }
				public string Value { get; set; }
			}
		}

		public class ResponseModel : INotifyPropertyChanged
		{
			private HttpContentType _responseType = HttpContentType.Text;

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

			internal HttpContentHeaders ResponseContentHeaders { get; set; }
			internal string RealFileName { get; set; }

		}
	}
}
