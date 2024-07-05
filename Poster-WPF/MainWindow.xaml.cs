#nullable enable

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
#pragma warning disable IDE1006 // We use camel case for XAML components.
	private ComboBox methodSelector = null!, contentTypeSelector = null!;
#pragma warning restore
	private readonly RequestModel _requestModel = new();
	private readonly ResponseModel _responseModel = new();
	private readonly Progress<double> _progress;
	private bool _progressBarIndeterminate;

	protected CancellationTokenSource? _sendButtonCTS;

	public bool ProgressBarIndeterminate
	{
		get => _progressBarIndeterminate;
		set
		{
			_progressBarIndeterminate = value;
			progressBar.IsIndeterminate = value;
		}
	}

	public MainWindow()
	{
		InitializeComponent();
		DataContext = this;
		requestPanel.DataContext = _requestModel;
		responsePanel.DataContext = _responseModel;
		_progress = new(value =>
		{
			ProgressBarIndeterminate = false;
			progressBar.Value = value;
			if (value < 1)
			{
				ShowHint($"Receiving {value * 100 :0.00}%");
			}
			else
			{
				ShowHint("Done.");
				progressBar.Value = 0;
			}
		});
	}

	protected override void OnContentRendered(EventArgs e)
	{
		base.OnContentRendered(e);

		methodSelector = (this.FindUid(nameof(methodSelector)) as ComboBox)!;
		contentTypeSelector = (this.FindUid(nameof(contentTypeSelector)) as ComboBox)!;

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

	private void ExpandOnClick(object sender, RoutedEventArgs e)
	{
		if (sender is ComboBox comboBox)
		{
			comboBox.IsDropDownOpen = true;
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

	private void OnMethodChanged(object sender, SelectionChangedEventArgs e)
	{
		bool enabled = e.AddedItems.Count == 0;
		if (!enabled)
		{
			string method = e.AddedItems[0].ToString();
			enabled = method.HasMethodBody();
		}
		contentTypeSelector.IsEnabled = requestTab.IsEnabled = enabled;
	}

	private void OnContentTypeChanged(object sender, SelectionChangedEventArgs e)
	{
		string selectedValue = e.AddedItems.Cast<string>().First();
		var type = selectedValue.ToContentType();
		requestTab.SelectedIndex = (int)HttpContentTypeConverter.Default.Convert(type, typeof(int), null, null);
	}

	private void OnHeadersTabChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue)
		{
			SetHeadersText();
			headersInput.Select(headersInput.Text.Length, 0);
			headersInput.Focus();
		}
		else
			SetHeadersGrid();
	}

	private const string DragImageBits = "DragImageBits";

	private void OnImageDragOver(object sender, DragEventArgs e)
	{
		e.Handled = true;
		if (e.Data.GetDataPresent(DragImageBits))
		{
			ShowHint("Drop to upload.");
			e.Effects = DragDropEffects.Copy;
			return;
		}
		else
		{
			ShowHint("Only files are allowed.");
		}
		e.Effects = DragDropEffects.None;
	}

	private void OnImageDrop(object sender, DragEventArgs e)
	{
		//if (!e.Data.GetDataPresent(DragImageBits))
		//	return;
		//MemoryStream imageStream = (MemoryStream)e.Data.GetData(DragImageBits);
		//var bitmap = imageStream.GetDragImage();
		//imageInput.Source = bitmap;
		//ShowHint("Image selected.");
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

	private async void OnImageSaveClicked(object sender, RoutedEventArgs e)
	{
		var ext = System.IO.Path.GetExtension(_responseModel.RealFileName);
		SaveFileDialog saveFileDialog = new()
		{
			FileName = _responseModel.RealFileName,
			Filter = $"*{ext}|{ext}|All files|*",
			Title = "Save image",
		};
		if (saveFileDialog.ShowDialog(this) == true)
		{
			using FileStream fileStream = new(saveFileDialog.FileName, FileMode.Create);
			var s = _responseModel.ResponseStream;
			s.Position = 0;
			await s.CopyToAsync(fileStream);
		}
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

	private void SetHeadersGrid()
	{
		var headers = headersInput.Text.ToHeaders();
		_requestModel.RequestHeaders = new(headers);
		_requestModel.NotifyChange(nameof(RequestModel.RequestHeaders));
	}

	private void SetHeadersText()
	{
		var text = string.Join("\n", _requestModel.RequestHeaders.Select(header =>
			$"{header.Name}: {string.Join(", ", header.Value)}"));
		headersInput.Text = text;
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
}
