using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class AboutWindow : Window
{
    private const string CompanyWebsite = "hillierconsulting.ca";

    private readonly MainViewModel _viewModel;

    public AboutWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        VersionText.Text = $"Version {viewModel.AppVersion.TrimStart('v')}";
        CopyrightText.Text = $"{viewModel.CopyrightText}. All rights reserved.";
        ChannelText.Text = AppChannel.Name;
        DbPathText.Text = viewModel.CurrentDbPath;
        SettingsPathText.Text = AppConfig.SettingsFilePath;
        RuntimeText.Text = $".NET {Environment.Version} on {Environment.OSVersion.VersionString}";
    }

    private void Website_Click(object sender, RoutedEventArgs e)
    {
        UrlLauncher.Open(CompanyWebsite, this);
    }

    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        var details = string.Join(Environment.NewLine,
            $"{AppChannel.DisplayName} {VersionText.Text}",
            $"{_viewModel.CopyrightText}",
            $"Channel: {ChannelText.Text}",
            $"Task database: {DbPathText.Text}",
            $"Settings file: {SettingsPathText.Text}",
            $"Runtime: {RuntimeText.Text}");

        try
        {
            Clipboard.SetText(details);
        }
        catch (Exception ex)
        {
            // The clipboard can be momentarily locked by another process; nothing here is worth
            // failing the dialog over, so just say so rather than throwing.
            MessageBox.Show(this, $"Couldn't copy to the clipboard: {ex.Message}", "Copy Failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
