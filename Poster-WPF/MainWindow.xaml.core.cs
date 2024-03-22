using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace Poster;

public partial class MainWindow
{
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
		HttpContent requestContent = null;
		switch (methodSelector.SelectedValue.ToString())
		{
			case "GET":
			case "HEAD":
			case "OPTIONS":
				break;
			default:
				requestContent = GetContent();
				break;
		}
		try
		{
			var message = new HttpRequestMessage((HttpMethod)methodSelector.SelectedItem, urlText.Text)
			{
				Content = requestContent,
			};
			foreach (var header in _requestModel.RequestHeaders)
			{
				var name = header.Name;
				var value = header.Value;
				message.Headers.Add(name, value);
			}
			using var result = await client.SendAsync(
				message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

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

			using var responseContent = result.Content;
			using var responseStream = await responseContent.ReadAsStreamAsync();
			var responseHeaders = responseContent.Headers;
			responseInfo.Text = DumpHeaders(responseHeaders);
			_responseModel.ResponseContentHeaders = responseHeaders;
			string contentType = responseHeaders.ContentType?.MediaType;
			_responseModel.ResponseType =
				contentType is null ? HttpContentType.Text : contentType.ToContentType();
			var totalBytes = responseContent.Headers.ContentLength.GetValueOrDefault(-1L);
			MemoryStream memoryCopy = new();
			_responseModel.ResponseStream = memoryCopy;
			await ReadStreamAsnyc(
				responseStream, memoryCopy, totalBytes, _progress, cancellationToken);
			switch (_responseModel.ResponseType)
			{
				case HttpContentType.Text:
					memoryCopy.Position = 0;
					using (var reader = new StreamReader(memoryCopy))
						textResponse.Text = reader.ReadToEnd();
					break;
				case HttpContentType.Image:
					memoryCopy.Position = 0;
					var bitmap = new BitmapImage();
					imageResponse.Source = bitmap;
					bitmap.BeginInit();
					bitmap.StreamSource = memoryCopy;
					bitmap.BaseUri = null; // this MUST set to null HERE to avoid NullReferenceException in next line.
					bitmap.EndInit();
					break;
				case HttpContentType.File:
				default:
					string fileName = responseHeaders.ContentDisposition.FileName;
					if (string.IsNullOrWhiteSpace(fileName))
					{
						var path = urlText.Text.Split('/').Last();
						fileName = Path.GetFileName(path);
					}
					_responseModel.RealFileName = fileName;
					string ext = Path.GetExtension(fileName);
					if (string.IsNullOrEmpty(ext))
					{
						ext = ".tmp";
					}
					string tempPath = Path.GetTempPath();
					string tempFolderName = Path.GetRandomFileName();
					string tempFolderPath = Path.Combine(tempPath, tempFolderName);
					_tempFileFolder = Directory.CreateDirectory(tempFolderPath);
					string tempFilePath = Path.GetRandomFileName();
					tempFilePath = Path.ChangeExtension(tempFilePath, ext);
					using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
					{
						await memoryCopy.CopyToAsync(fileStream);
					}
					fileResponsePath.Text = tempFilePath;
					break;
			}
		}
		catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
		{
			MarkError();
			var msg = ex.Message;
			if (ex.InnerException is not null)
			{
				msg += "\t" + ex.InnerException.Message;
			}
			statusText.Text = statusBar.Text = msg;
		}
	}

	private async Task ReadStreamAsnyc(
		Stream inputStream, MemoryStream outputStream,
		long totalBytes, IProgress<double> progress, CancellationToken cancellationToken)
	{
		var canReportProgress = totalBytes != -1L;
		var bytesReceived = 0L;
		const int bufferSize = 8192;
		var buffer = new byte[bufferSize];

		int bytesRead;
		// The offset parameter tells where to start writing data in your array, the array parameter.
		// It does not point out an offset in the stream data.
		while ((bytesRead = await inputStream.ReadAsync(buffer, 0, bufferSize, cancellationToken)) != 0)
		{
			outputStream.Write(buffer, 0, bytesRead);
			bytesReceived += bytesRead;
			if (canReportProgress)
			{
				var progressPercentage = (double)bytesReceived / totalBytes * 100;
				progress.Report(progressPercentage);
			}
		}
	}
}
