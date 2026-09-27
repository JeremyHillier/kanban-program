using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Shown when the update check finds a newer version: what is new in it, and a button that opens
// the download page. Nothing is downloaded or installed from here.
public partial class UpdateAvailableWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly UpdateInfo _update;

    public UpdateAvailableWindow(MainViewModel viewModel, UpdateInfo update)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _update = update;

        HeadlineText.Text = $"Version {update.Version} is available";
        SubheadText.Text = $"You have version {viewModel.AppVersion.TrimStart('v')}.";
        VersionText.Text = $"New in {update.Version}";
        NotesList.ItemsSource = update.Notes;
        NoNotesText.Visibility = update.Notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GetUpdate_Click(object sender, RoutedEventArgs e)
    {
        UrlLauncher.Open(AppInfo.DownloadPageUrl, this);
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SkipUpdateVersion(_update.Version);
        Close();
    }
}
