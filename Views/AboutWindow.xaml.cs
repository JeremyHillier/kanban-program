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
        CompanyText.Text = AppInfo.Company;
        CopyrightText.Text = $"{viewModel.CopyrightText}. All rights reserved.";
        ChannelText.Text = AppChannel.Name;
        DbPathText.Text = viewModel.CurrentDbPath;
        SettingsPathText.Text = AppConfig.SettingsFilePath;
        RuntimeText.Text = $".NET {Environment.Version} on {Environment.OSVersion.VersionString}";
        SupportEmailRun.Text = AppInfo.SupportEmail;
    }

    private void Website_Click(object sender, RoutedEventArgs e)
    {
        UrlLauncher.Open(CompanyWebsite, this);
    }

    private void SupportEmail_Click(object sender, RoutedEventArgs e)
    {
        UrlLauncher.Open($"mailto:{AppInfo.SupportEmail}", this);
    }

    private void Eula_Click(object sender, RoutedEventArgs e)
    {
        new LegalWindow("Licence Agreement", LegalDocuments.Eula) { Owner = this }.ShowDialog();
    }

    private void Privacy_Click(object sender, RoutedEventArgs e)
    {
        new LegalWindow("Privacy Note", LegalDocuments.Privacy) { Owner = this }.ShowDialog();
    }

    // Checking by hand: always answers, one way or the other, and ignores "Skip This Version". It
    // does not count as the day's automatic check, so trying it never delays that.
    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking...";
        UpdateStatusText.Visibility = Visibility.Visible;

        var update = await UpdateChecker.FetchLatestAsync(_viewModel.AppVersion.TrimStart('v'));
        if (!IsLoaded) return; // closed while waiting

        CheckUpdatesButton.IsEnabled = true;
        if (update is null)
        {
            UpdateStatusText.Text = "Couldn't check just now. Make sure this PC is connected to the internet and try again.";
            return;
        }

        if (!_viewModel.ShouldOfferUpdate(update, askedByHand: true))
        {
            UpdateStatusText.Text = $"You have the latest version ({_viewModel.AppVersion.TrimStart('v')}).";
            return;
        }

        UpdateStatusText.Text = $"Version {update.Version} is available.";
        new UpdateAvailableWindow(_viewModel, update) { Owner = this }.ShowDialog();
    }

    private void ReportProblem_Click(object sender, RoutedEventArgs e)
    {
        ProblemReport.Compose(this, _viewModel.CurrentDbPath);
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
