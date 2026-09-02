using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

public partial class ManageCustomFiltersWindow : Window
{
    // Flattens a slot into the shape the list template binds to. Rebuilt wholesale after every
    // change rather than kept in sync, since there are only ten of them.
    private sealed record SlotRow(int Slot, string Shortcut, string DisplayName, string Summary);

    private readonly MainViewModel _viewModel;

    public ManageCustomFiltersWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        RefreshList(selectSlot: 0);
    }

    private void RefreshList(int selectSlot)
    {
        SlotList.ItemsSource = Enumerable.Range(0, MainViewModel.CustomFilterSlotCount)
            .Select(slot =>
            {
                var filter = _viewModel.CustomFilters[slot];
                return new SlotRow(slot, $"Alt+{slot}",
                    filter.IsDefined ? filter.Name : "(unassigned)", filter.Summary);
            })
            .ToList();

        SlotList.SelectedIndex = selectSlot;
        UpdateButtonStates();
    }

    private SlotRow? Selected => SlotList.SelectedItem as SlotRow;

    private void SlotList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var isDefined = Selected is not null && _viewModel.CustomFilters[Selected.Slot].IsDefined;
        RenameButton.IsEnabled = isDefined;
        ApplyButton.IsEnabled = isDefined;
        ClearButton.IsEnabled = isDefined;
        SaveCurrentButton.IsEnabled = Selected is not null;
    }

    private void SaveCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;

        var existing = _viewModel.CustomFilters[row.Slot];
        var suggested = existing.IsDefined ? existing.Name : $"Filter {row.Slot}";

        var prompt = new PromptWindow($"Save to Alt+{row.Slot}", "Name for this filter:") { Owner = this };
        if (prompt.ShowDialog() != true) return;

        _viewModel.CaptureCustomFilter(row.Slot, string.IsNullOrWhiteSpace(prompt.Value) ? suggested : prompt.Value);
        RefreshList(row.Slot);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;

        var prompt = new PromptWindow($"Rename Alt+{row.Slot}", "New name:") { Owner = this };
        if (prompt.ShowDialog() != true) return;

        _viewModel.RenameCustomFilter(row.Slot, prompt.Value);
        RefreshList(row.Slot);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;

        _viewModel.ApplyCustomFilter(row.Slot);
        Close();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;

        var filter = _viewModel.CustomFilters[row.Slot];
        var confirm = MessageBox.Show(this,
            $"Clear the filter saved on Alt+{row.Slot} (\"{filter.Name}\")?",
            "Clear Custom Filter", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _viewModel.ClearCustomFilter(row.Slot);
        RefreshList(row.Slot);
    }
}
