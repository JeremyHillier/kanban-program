using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;
using Microsoft.Win32;

namespace KanbanApp.Views;

// Saved report views: loading one onto the screen, saving (updating the loaded view or saving
// as new), and deleting.
public partial class ReportBuilderWindow
{
    // The view most recently loaded (or saved) on this screen - Save View offers to update it.
    private string? _loadedViewName;

    private void LoadReportView_Click(object sender, RoutedEventArgs e)
    {
        if (SavedViewsComboBox.SelectedItem is not SavedReportView view) return;
        ApplyReportView(view);
        _loadedViewName = view.Name;
    }

    // After a view has been loaded, the prompt starts with that view's name and saving under it
    // asks to update the view; a different name saves as a new view (asking first only if that
    // name is already taken by some other view).
    private void SaveReportView_Click(object sender, RoutedEventArgs e)
    {
        var updating = _loadedViewName is not null && _viewModel.SavedReportViews.Any(v => string.Equals(v.Name, _loadedViewName, StringComparison.OrdinalIgnoreCase));
        var prompt = new PromptWindow("Save Report View", updating ? "Name for this view (keep the name to update the loaded view):" : "Name for this view:",
            updating ? _loadedViewName : null, "Save") { Owner = this };
        if (prompt.ShowDialog() != true) return;

        var name = prompt.Value.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        var existing = _viewModel.SavedReportViews.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            var isTheLoadedOne = updating && string.Equals(name, _loadedViewName, StringComparison.OrdinalIgnoreCase);
            // Updating the view that is loaded is the everyday case, so Enter still does it; replacing
            // a different view by accident is not, so there Enter keeps it.
            var message = isTheLoadedOne
                ? DialogMessage.Ask("Update View", $"Update the saved view \"{existing.Name}\" with what is on the screen now?", "Update It")
                : DialogMessage.AskDanger("Replace View",
                    $"Replace the saved view \"{existing.Name}\"?\n\nThere is already a saved view with that name. It will be replaced with what is on the screen now.", "Replace It");
            if (!Dialogs.Confirm(this, message)) return;
            name = existing.Name; // keep the capitals it was saved with
        }

        _viewModel.SaveReportView(CaptureCurrentAsView(name));
        SavedViewsComboBox.SelectedItem = _viewModel.SavedReportViews.FirstOrDefault(v => v.Name == name);
        _loadedViewName = name;
    }

    private void DeleteReportView_Click(object sender, RoutedEventArgs e)
    {
        if (SavedViewsComboBox.SelectedItem is not SavedReportView view) return;

        if (!Dialogs.Confirm(this, DialogMessage.AskDanger("Delete View",
                $"Delete the saved view \"{view.Name}\"?\n\nOnly the saved settings go. Tasks are not affected.", "Delete")))
            return;

        _viewModel.DeleteReportView(view.Name);
        if (string.Equals(_loadedViewName, view.Name, StringComparison.OrdinalIgnoreCase)) _loadedViewName = null;
    }

    // Captures every field this window exposes - broader than GetParameterSummary, which only
    // describes the filter/scope/sort choices that affect which rows match, not layout choices
    // like columns, orientation, or the Notes/Sub-tasks toggles.
    private SavedReportView CaptureCurrentAsView(string name) => new()
    {
        Name = name,
        Title = GetReportTitle(),
        IncludedColumns = GetIncludedColumns().ToList(),
        Project = _projectOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
        Priority = _priorityOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
        Who = _whoOptions.Where(o => o.IsSelected).Select(o => o.Name).ToList(),
        Goal = (string)GoalFilterComboBox.SelectedItem,
        Flag = (string)FlagFilterComboBox.SelectedItem,
        Due = (string)DueFilterComboBox.SelectedItem,
        DueFrom = SavedDate(DueFromDatePicker),
        DueTo = SavedDate(DueToDatePicker),
        IncludeNoDueDate = IncludeNoDueDateCheckBox.IsChecked == true,
        CustomFilterNames = _customFilterCheckBoxes.Where(c => c.IsChecked == true).Select(c => ((CustomFilter)c.Tag).Name).ToList(),
        SortLevel1 = (string)((ComboBoxItem)SortLevel1ComboBox.SelectedItem).Tag,
        SortLevel2 = (string)((ComboBoxItem)SortLevel2ComboBox.SelectedItem).Tag,
        SortLevel3 = (string)((ComboBoxItem)SortLevel3ComboBox.SelectedItem).Tag,
        GroupBy = GetGroupBy(),
        ArchiveScope = GetArchiveScope().ToString(),
        ArchivedFrom = SavedDate(ArchivedFromDatePicker),
        ArchivedTo = SavedDate(ArchivedToDatePicker),
        IsLandscape = LandscapeRadio.IsChecked == true,
        IncludeNotes = IncludeNotesCheckBox.IsChecked == true,
        IncludeSubTasks = IncludeSubTasksCheckBox.IsChecked == true,
        IncludeSubTaskSummary = IncludeSubTaskSummaryCheckBox.IsChecked == true
    };

    private void ApplyReportView(SavedReportView view)
    {
        ReportTitleTextBox.Text = view.Title;

        foreach (var checkBox in _columnCheckBoxes) checkBox.IsChecked = view.IncludedColumns.Contains((string)checkBox.Tag);

        foreach (var option in _projectOptions) option.IsSelected = view.Project.Contains(option.Name);
        foreach (var option in _priorityOptions) option.IsSelected = view.Priority.Contains(option.Name);
        foreach (var option in _whoOptions) option.IsSelected = view.Who.Contains(option.Name);

        SelectComboItem(GoalFilterComboBox, view.Goal);
        SelectComboItem(FlagFilterComboBox, view.Flag);
        SelectComboItem(DueFilterComboBox, view.Due);

        SetDate(DueFromDatePicker, view.DueFrom);
        SetDate(DueToDatePicker, view.DueTo);
        IncludeNoDueDateCheckBox.IsChecked = view.IncludeNoDueDate;

        // A custom filter slot renamed or deleted since this view was saved just drops out here,
        // the same as a gone Project/Who/Goal name silently falling out of the lists above.
        foreach (var checkBox in _customFilterCheckBoxes)
        {
            checkBox.IsChecked = view.CustomFilterNames.Contains(((CustomFilter)checkBox.Tag).Name);
        }

        SelectComboItemByTag(GroupByComboBox, view.GroupBy);
        SelectComboItemByTag(SortLevel1ComboBox, view.SortLevel1);
        SelectComboItemByTag(SortLevel2ComboBox, view.SortLevel2);
        SelectComboItemByTag(SortLevel3ComboBox, view.SortLevel3);

        BoardOnlyRadio.IsChecked = view.ArchiveScope == nameof(ReportArchiveScope.BoardOnly);
        BoardAndArchivedRadio.IsChecked = view.ArchiveScope == nameof(ReportArchiveScope.BoardAndArchived);
        ArchivedOnlyRadio.IsChecked = view.ArchiveScope == nameof(ReportArchiveScope.ArchivedOnly);

        SetDate(ArchivedFromDatePicker, view.ArchivedFrom);
        SetDate(ArchivedToDatePicker, view.ArchivedTo);

        LandscapeRadio.IsChecked = view.IsLandscape;
        PortraitRadio.IsChecked = !view.IsLandscape;

        IncludeNotesCheckBox.IsChecked = view.IncludeNotes;
        IncludeSubTasksCheckBox.IsChecked = view.IncludeSubTasks;
        IncludeSubTaskSummaryCheckBox.IsChecked = view.IncludeSubTaskSummary;

        UpdateSortLevelAvailability();
    }

    private static void SelectComboItem(ComboBox combo, string value)
    {
        if (combo.Items.Contains(value)) combo.SelectedItem = value;
    }

    private static void SelectComboItemByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem comboBoxItem && (string)comboBoxItem.Tag == tag)
            {
                combo.SelectedItem = comboBoxItem;
                return;
            }
        }
    }
}
