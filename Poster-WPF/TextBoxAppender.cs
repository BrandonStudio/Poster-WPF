using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Poster;

internal class TextBoxAppender : IDisposable
{
	private readonly TextBox _textBox;
	private readonly LinkedList<DispatcherOperation> _appendTasks = new();

	public TextBoxAppender(TextBox textBox)
	{
		_textBox = textBox;
	}

	public void Start()
	{
		_textBox.Text = string.Empty;
		_appendTasks.Clear();
	}

	public void AppendText(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
	{
		string text = Encoding.UTF8.GetString(buffer, offset, count); // TODO: Encoding
		var task = _textBox.Dispatcher.InvokeAsync(() =>
		{
			_textBox.AppendText(text);
		}, DispatcherPriority.Normal, cancellationToken);
		_appendTasks.AddLast(task);
	}

	public async Task FlushAsync(CancellationToken cancellationToken = default)
	{
		foreach (var task in _appendTasks)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				task.Abort();
			}
			else
			{
				await task;
			}
		}
		_appendTasks.Clear();
	}

	private bool _disposedValue;
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_appendTasks.Clear();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

internal delegate Task WriteStreamFunc(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
