#nullable enable

using System.Windows;

namespace Poster;

/// <summary>
/// Dialog that lets the user paste a cURL command for import.
/// </summary>
public partial class CurlImportDialog : Window
{
	/// <summary>The cURL command text entered by the user, or <c>null</c> when cancelled.</summary>
	public string? CurlCommand { get; private set; }

	public CurlImportDialog()
	{
		InitializeComponent();
	}

	private void OnOkClicked(object sender, RoutedEventArgs e)
	{
		CurlCommand = curlInput.Text;
		DialogResult = true;
	}

	private void OnCancelClicked(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
	}

	private void OnPasteClicked(object sender, RoutedEventArgs e)
	{
		if (Clipboard.ContainsText())
			curlInput.Text = Clipboard.GetText();
	}
}
