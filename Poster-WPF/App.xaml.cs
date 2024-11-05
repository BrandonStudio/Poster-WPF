#nullable enable

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Poster
{
	/// <summary>
	/// App.xaml 的交互逻辑
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			DispatcherUnhandledException += DispatcherExceptionHandler;
			TaskScheduler.UnobservedTaskException += TaskExceptionHandler;
		}

		private void TaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
		{
			GlobalCatch(sender, e.Exception);
			e.SetObserved();
		}

		public void DispatcherExceptionHandler(object? sender, DispatcherUnhandledExceptionEventArgs e)
		{
			GlobalCatch(sender, e.Exception);
			e.Handled = true;
		}

		public void GlobalCatch(object? sender, Exception ex)
		{
			var window = sender as Window;
			var parent = window?.Parent as Window;

			GlobalCatch(parent, ex);
		}

		public void GlobalCatch(Window? parent, Exception ex)
		{
			if (parent is not null)
			{
				MessageBox.Show(parent,
					string.Format(
						"An unhandled exception occurred:\n{0}\nThrown through {1}",
						ex.Message,
						ex.StackTrace),
					"Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
			else
			{
				MessageBox.Show(
					string.Format(
						"An unhandled exception occurred:\n{0}\nThrown through {1}",
						ex.Message,
						ex.StackTrace),
					"Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
			_onExceptionCaught?.Invoke(parent, ex);
		}

		public event ExceptionHandler? OnExceptionCaught
		{
			add => _onExceptionCaught += value;
			remove => _onExceptionCaught -= value;
		}

		protected event ExceptionHandler? _onExceptionCaught;
	}

	public delegate void ExceptionHandler(Window? parent, Exception ex);
}
