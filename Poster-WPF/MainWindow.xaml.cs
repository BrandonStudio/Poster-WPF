#nullable enable

using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

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
	private TaskbarItemProgressState _progressBarState;

	protected CancellationTokenSource? _sendButtonCTS;

	public bool ProgressBarIndeterminate
	{
		get => _progressBarState == TaskbarItemProgressState.Indeterminate;
		set
		{
			progressBar.IsIndeterminate = value;
			_progressBarState = value ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.Normal;
			TaskbarManager.Instance?.SetProgressState(_progressBarState.ToWinAPI(), this);
		}
	}

	public TaskbarItemProgressState ProgressState
	{
		get => _progressBarState;
		set
		{
			_progressBarState = value;
			if (value == TaskbarItemProgressState.Indeterminate)
			{
				progressBar.IsIndeterminate = true;
			}
			else
			{
				progressBar.IsIndeterminate = false;
			}
			TaskbarManager.Instance?.SetProgressState(value.ToWinAPI(), this);
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
			TaskbarManager.Instance?.SetProgressValue((int)(value * 100), 100, this);
			if (value < 1)
			{
				ShowHint($"Receiving {value * 100 :0.00}%");
			}
			else
			{
				ShowHint("Done.");
				ProgressState = TaskbarItemProgressState.None;
			}
		});
		var app = (App)Application.Current;
		app.OnExceptionCaught += (parent, ex) => CatchExceptions(ex);
	}

	private void CatchExceptions(Exception ex)
	{
		MarkError();
		statusText.Text = statusBar.Text = ex.Message;
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
		Process.Start("explorer.exe", $"/e, /select, \"{_responseModel.TempFilePath}\"");
	}

	private void OnOpenWithNotepadClicked(object sender, RoutedEventArgs e)
	{
		Process.Start("notepad.exe", '"' + _responseModel.TempFilePath + '"');
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
			if (s is null)
			{
				ShowHint("Nothing to save.");
				return;
			}
			s.Position = 0;
			await s.CopyToAsync(fileStream);
			Helpers.MarkFile(saveFileDialog.FileName!);
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
			File.Copy(_responseModel.TempFilePath, saveFileDialog.FileName!, true);
			Helpers.MarkFile(saveFileDialog.FileName!);
		}
	}

	private async Task StartSend()
	{
		_sendButtonCTS?.Cancel();
		_sendButtonCTS = new();
		_responseModel.Reset();
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
		ProgressState = TaskbarItemProgressState.Error;
		ShowHint("Error");
	}

	private string ShowHint(string hint)
	{
		string original = statusBar.Text;
		statusBar.Text = hint;
		return original;
	}
}
