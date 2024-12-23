#nullable enable

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
using System.Net.Http.Headers;
using System.Windows.Shell;

using static Poster.Interops.FileZone;

namespace Poster;

public partial class MainWindow
{
	private HttpContent GetContent()
	{
		HttpContent content;
		switch (_requestModel.RequestType)
		{
			case HttpContentType.Text:
				content = new StringContent(textInput.Text, null, mediaType: contentTypeSelector.Text);
				break;
			case HttpContentType.Image:
				//var source = imageInput.Source as BitmapSource;
				//if (source is BitmapImage bitmap)
				//{
				//	content = new StreamContent(bitmap.StreamSource);
				//}
				//else
				//{
				//	var encoder = new PngBitmapEncoder(); // Use JpegBitmapEncoder or other encoders according to your needs
				//	encoder.Frames.Add(BitmapFrame.Create(source));

				//	var stream = new MemoryStream();
				//	encoder.Save(stream);
				//	stream.Position = 0; // Reset stream position

				//	content = new StreamContent(stream);
				//}
				//break;
			case HttpContentType.File:
			default:
				content = new StreamContent(File.OpenRead(inputFilePath.Text));
				break;
		}
		content.Headers.ContentType = new(contentTypeSelector.Text);
		return content;
	}

	/// <exception cref="OperationCanceledException">The operation was canceled.</exception>
	private async Task SendAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ProgressBarIndeterminate = true;
		HttpClientHandler httpClientHandler = new()
		{
			AllowAutoRedirect = false,
		};
		HttpClient client = new(httpClientHandler)
		{
			Timeout = TimeSpan.FromSeconds(120)
		};
		client.DefaultRequestHeaders.UserAgent.Add(
			new("Poster", Assembly.GetExecutingAssembly().GetName().Version.ToString()));
		HttpContent? requestContent = null;
		if (methodSelector.SelectedValue.ToString().HasMethodBody())
		{
			requestContent = GetContent();
		}
		try
		{
			var message = new HttpRequestMessage((HttpMethod)methodSelector.SelectedItem, urlText.Text)
			{
				Content = requestContent,
			};
			if (headersTab.SelectedIndex == 1)
			{
				SetHeadersGrid();
			}
			try
			{
				foreach (var header in _requestModel.RequestHeaders)
				{
					var name = header.Name;
					var value = header.Value;
					message.Headers.Add(name, value);
				}
			}
			catch (FormatException)
			{
				MarkError();
				statusText.Text = statusBar.Text = "Invalid header format";
				return;
			}
			using var result = await client.SendAsync(
				message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

			statusText.Text = $"{(int)result.StatusCode} {result.StatusCode}";
			int code = (int)result.StatusCode;
			if (code >= 100 && code < 200)
			{
				statusText.Foreground = new SolidColorBrush(Colors.Black);
				statusBar.Text = "Informational";
			}
			else if (code >= 200 && code < 300)
			{
				statusText.Foreground = new SolidColorBrush(Colors.Green);
				statusBar.Text = "Success. Receiveing...";
			}
			else if (code >= 300 && code < 400)
			{
				statusText.Foreground = new SolidColorBrush(Colors.Orange);
				statusBar.Text = "Redirect";
			}
			else
			{
				MarkError();
			}

			using var responseContent = result.Content;
			using var responseStream = await responseContent.ReadAsStreamAsync();
			var responseHeaders = responseContent.Headers;
			responseInfo.Text = DumpHeaders(result.Headers) + '\n' + DumpHeaders(responseHeaders);
			_responseModel.ResponseContentHeaders = responseHeaders;
			string? contentType = responseHeaders.ContentType?.MediaType;
			_responseModel.ResponseType =
				contentType is null ? HttpContentType.Text : contentType.ToContentType();
			var totalBytes = responseContent.Headers.ContentLength.GetValueOrDefault(-1L);
			if (totalBytes >= 0)
			{
				ProgressBarIndeterminate = false;
			}
			MemoryStream? memoryCopy = new();
			_responseModel.ResponseStream = memoryCopy;
			//await ReadStreamAsnyc(
			//	responseStream, memoryCopy, totalBytes, _progress, cancellationToken);
			async Task Read(WriteStreamFunc? func) => await ReadStreamAsnyc(responseStream, memoryCopy, func, totalBytes, _progress, cancellationToken);
			if (totalBytes == 0)
				return;
			switch (_responseModel.ResponseType)
			{
				case HttpContentType.Text:
					//memoryCopy.Position = 0;
					//using (var reader = new StreamReader(memoryCopy))
					//	textResponse.Text = reader.ReadToEnd();
					using (TextBoxAppender textBoxAppender = new(textResponse))
					{
						await Read((buffer, offset, count, cancellationToken) =>
						{
							textBoxAppender.AppendText(buffer, offset, count, cancellationToken);
							return Task.CompletedTask;
						});
						await textBoxAppender.FlushAsync(cancellationToken);
					}
					break;
				case HttpContentType.Image:
					await Read(null);
					memoryCopy.Position = 0;
					var bitmap = new BitmapImage();
					imageResponse.Source = bitmap;
					bitmap.BeginInit();
					bitmap.StreamSource = memoryCopy;
					bitmap.BaseUri = null; // this MUST set to null HERE to avoid NullReferenceException in next line.
					try
					{
						bitmap.EndInit();
					}
					catch (NotSupportedException)
					{
						imageResponse.Source = null;
						_responseModel.ResponseType = HttpContentType.File;
						goto SaveFile;
					}
					break;
				case HttpContentType.File:
				default:
				SaveFile:
					string? fileName = responseHeaders.ContentDisposition?.FileName;
					fileName = fileName?.Trim('"', '\'', ' ');
					if (string.IsNullOrWhiteSpace(fileName))
					{
						var path = urlText.Text.Split('/').Last();
						fileName = Path.GetFileName(path);
					}
					else
					{
						var chs = Path.GetInvalidFileNameChars();
						var array = fileName!.ToCharArray();
						for (int i = 0; i < array.Length; i++)
						{
							if (chs.Contains(array[i]))
							{
								array[i] = '_';
							}
						}
						fileName = new string(array);
					}
					_responseModel.RealFileName = fileName;
					string ext = Path.GetExtension(fileName);
					if (string.IsNullOrEmpty(ext) || Constants.InvalidExtensions.Contains(ext))
					{
						ext = ".tmp";
					}
					string tempFilePath = Path.GetRandomFileName();
					tempFilePath = Path.ChangeExtension(tempFilePath, ext);
					tempFilePath = Path.Combine(_responseModel.TempFolder.FullName, tempFilePath);
					_responseModel.TempFilePath = tempFilePath;
					using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
					{
						//memoryCopy.Position = 0;
						//await memoryCopy.CopyToAsync(fileStream, 81920, cancellationToken);
						await Read(fileStream.WriteAsync);
					}
					Helpers.MarkFile(tempFilePath, UrlZone.Internet);
					fileResponsePath.Text = tempFilePath;
					_responseModel.FileAvailable = true;
					break;
			}

			_responseModel.StreamSaved = true;
			if (ProgressBarIndeterminate)
			{
				if (statusBar.Text.EndsWith("..."))
				{
					statusBar.Text = "Done.";
				}
			}
		}
		catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UriFormatException)
		{
			MarkError();
			var msg = ex.Message;
			if (ex.InnerException is not null)
			{
				msg += "\t" + ex.InnerException.Message;
			}
			statusText.Text = statusBar.Text = msg;
		}
		finally
		{
			ProgressState = TaskbarItemProgressState.None;
			progressBar.Value = 0;
		}
	}

	private async Task ReadStreamAsnyc(
		Stream inputStream, Stream memoryStream, WriteStreamFunc? callback,
		long totalBytes, IProgress<double> progress, CancellationToken cancellationToken)
	{
		var canReportProgress = totalBytes >= 0;
		var bytesReceived = 0L;
		const int bufferSize = 8192;
		var buffer = new byte[bufferSize];

		int bytesRead;
		// The offset parameter tells where to start writing data in your array, the array parameter.
		// It does not point out an offset in the stream data.
		while ((bytesRead = await inputStream.ReadAsync(buffer, 0, bufferSize, cancellationToken)) != 0)
		{
			var mTask = memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
			var oTask = callback?.Invoke(buffer, 0, bytesRead) ?? Task.CompletedTask;
			await Task.WhenAll(mTask, oTask);
			bytesReceived += bytesRead;
			if (canReportProgress)
			{
				var progressPercentage = (double)bytesReceived / totalBytes;
				progress.Report(progressPercentage);
			}
		}
	}

	private string DumpHeaders(HttpHeaders headers) =>
		string.Join("\n", headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}"));
}
