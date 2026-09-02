using System.Windows;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class WhatsNewWindow : Window
{
    private readonly MainViewModel _viewModel;

    public WhatsNewWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        var notes = ReleaseNotes.Load(MainViewModel.WhatsNewVersionCount);
        ReleaseList.ItemsSource = notes;

        HeadlineText.Text = $"What's New in {viewModel.AppVersion}";
        SubheadText.Text = notes.Count > 0
            ? $"Everything added across the last {notes.Count} update{(notes.Count == 1 ? "" : "s")}."
            : "No release notes are available in this build.";

        ShowAgainCheckBox.IsChecked = viewModel.ShowWhatsNew;
    }

    private void ShowAgainCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetShowWhatsNew(ShowAgainCheckBox.IsChecked == true);
    }
}
