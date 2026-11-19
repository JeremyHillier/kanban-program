using System.Windows;
using System.Windows.Input;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The Quick Add tasks still to review. Review closes this list and hands the board the task to start from
// (StartFrom), so the full task window opens in its place with the review strip; Mark Reviewed clears tasks from here.
public partial class QuickEntriesWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Set when the user chose Review: the task the board should open first.
    public CardViewModel? StartFrom { get; private set; }

    public QuickEntriesWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        _viewModel = viewModel;
        Refresh();
    }

    private void Refresh()
    {
        var entries = _viewModel.QuickEntriesToReview();
        EntryList.ItemsSource = entries;
        EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ReviewButton.IsEnabled = entries.Count > 0;
        if (entries.Count > 0) EntryList.SelectedIndex = 0;
        UpdateButtons();
    }

    private void UpdateButtons() => MarkReviewedButton.IsEnabled = EntryList.SelectedItems.Count > 0;

    private void EntryList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateButtons();

    private void EntryList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || EntryList.SelectedItems.Count == 0) return;
        MarkReviewed_Click(sender, e);
        e.Handled = true;
    }

    private void MarkReviewed_Click(object sender, RoutedEventArgs e)
    {
        var chosen = EntryList.SelectedItems.OfType<CardViewModel>().ToList();
        if (chosen.Count == 0) return;
        _viewModel.MarkReviewed(chosen);
        Refresh();
    }

    private void Review_Click(object sender, RoutedEventArgs e)
    {
        StartFrom = EntryList.SelectedItem as CardViewModel ?? (EntryList.Items.Count > 0 ? EntryList.Items[0] as CardViewModel : null);
        if (StartFrom is null) return;
        DialogResult = true;
    }
}
