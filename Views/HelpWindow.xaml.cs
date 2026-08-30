using System.Diagnostics;
using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class HelpWindow : Window
{
    private readonly MainViewModel _viewModel;

    public HelpWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        WebsiteUrlTextBox.Text = viewModel.WebsiteUrl;
    }

    private void WebsiteUrlTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.SetWebsiteUrl(WebsiteUrlTextBox.Text.Trim());
    }

    private void OpenWebsite_Click(object sender, RoutedEventArgs e)
    {
        var url = WebsiteUrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.Contains("://")) url = "https://" + url;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't open the website: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
