using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Views;

// The Statistics report type: its options, and which of the window's fields apply to which type.
// A statistics report is run the same way Quick Report runs a saved view (ReportRunner), from the
// window's fields captured as a view, so the two can never disagree.
public partial class ReportBuilderWindow
{
    private const string TaskListTitle = "Kanban Task Report";
    private const string StatisticsTitle = "Task Statistics";

    private bool IsStatistics => StatisticsRadio.IsChecked == true;

    private void SetUpStatisticsOptions()
    {
        foreach (var (key, label) in StatisticsPeriods.All)
        {
            StatsPeriodComboBox.Items.Add(new ComboBoxItem { Content = label, Tag = key });
        }
    }

    private void ResetStatisticsOptions()
    {
        SelectComboItemByTag(StatsPeriodComboBox, StatisticsPeriods.Last30Days);
        SelectComboItemByTag(StatsBreakdownComboBox, "Project");
        SelectComboItemByTag(StatsOverTimeComboBox, "Week");
        StatsSummaryCheckBox.IsChecked = StatsBreakdownCheckBox.IsChecked = StatsOverTimeCheckBox.IsChecked = StatsColumnTimesCheckBox.IsChecked = true;
        StatsChartsCheckBox.IsChecked = true;
        ShowPeriodDates();
    }

    private void ReportType_Checked(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;

        // The default title follows the type; a title the user typed is left alone.
        var title = ReportTitleTextBox.Text.Trim();
        if (IsStatistics && (title.Length == 0 || title == TaskListTitle)) ReportTitleTextBox.Text = StatisticsTitle;
        else if (!IsStatistics && (title.Length == 0 || title == StatisticsTitle)) ReportTitleTextBox.Text = TaskListTitle;

        ShowOptionsForReportType();
    }

    // Statistics count every column, every due date and the archive too, so the task list's choices
    // about those are hidden while it is chosen, as are grouping, sort, notes and sub-tasks.
    private void ShowOptionsForReportType()
    {
        var statistics = IsStatistics;
        var taskListOnly = new FrameworkElement[]
        {
            IncludeColumnsLabel, ColumnsPanel, DueFilterLabel, DueFilterComboBox, DueRangeHeader, DueRangePanel, IncludeNoDueDateCheckBox,
            GroupByLabel, GroupByComboBox, SortOrderLabel, SortLevelsPanel, TaskScopeLabel, TaskScopePanel,
            ArchivedRangeLabel, ArchivedRangePanel, IncludeNotesCheckBox, IncludeSubTasksCheckBox, IncludeSubTaskSummaryCheckBox
        };
        foreach (var element in taskListOnly) element.Visibility = statistics ? Visibility.Collapsed : Visibility.Visible;
        StatisticsOptionsPanel.Visibility = statistics ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StatsPeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowPeriodDates();

    // A chosen period shows its dates (worked out for today) and can't be edited; Custom dates can.
    private void ShowPeriodDates()
    {
        if (StatsPeriodComboBox.SelectedItem is not ComboBoxItem { Tag: string key }) return;

        var custom = key == StatisticsPeriods.Custom;
        StatsFromDatePicker.IsEnabled = StatsToDatePicker.IsEnabled = custom;
        if (custom) return;

        var earliest = key == StatisticsPeriods.AllTime
            ? _viewModel.GetStatisticsHistory().Values.SelectMany(events => events).Where(ev => ev.IsCreated).Select(ev => (DateTime?)ev.At).Min()
            : null;
        var (from, to) = StatisticsPeriods.Resolve(key, DateTime.Today, earliest: earliest);
        StatsFromDatePicker.SelectedDate = from;
        StatsToDatePicker.SelectedDate = to;
    }

    private string SelectedTag(ComboBox combo) => combo.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : string.Empty;

    private void CaptureStatisticsOptions(SavedReportView view)
    {
        view.ReportType = IsStatistics ? SavedReportView.StatisticsType : SavedReportView.TaskListType;
        view.StatsPeriod = SelectedTag(StatsPeriodComboBox);
        var custom = view.StatsPeriod == StatisticsPeriods.Custom;
        view.StatsFrom = custom ? StatsFromDatePicker.SelectedDate?.ToString("yyyy-MM-dd") : null;
        view.StatsTo = custom ? StatsToDatePicker.SelectedDate?.ToString("yyyy-MM-dd") : null;
        view.StatsBreakdown = SelectedTag(StatsBreakdownComboBox);
        view.StatsOverTime = SelectedTag(StatsOverTimeComboBox);
        view.StatsShowSummary = StatsSummaryCheckBox.IsChecked == true;
        view.StatsShowBreakdown = StatsBreakdownCheckBox.IsChecked == true;
        view.StatsShowOverTime = StatsOverTimeCheckBox.IsChecked == true;
        view.StatsShowColumnTimes = StatsColumnTimesCheckBox.IsChecked == true;
        view.StatsShowCharts = StatsChartsCheckBox.IsChecked == true;
    }

    private void ApplyStatisticsOptions(SavedReportView view)
    {
        SelectComboItemByTag(StatsPeriodComboBox, view.StatsPeriod);
        if (view.StatsPeriod == StatisticsPeriods.Custom)
        {
            StatsFromDatePicker.SelectedDate = RelativeDate.Resolve(view.StatsFrom, DateTime.Today);
            StatsToDatePicker.SelectedDate = RelativeDate.Resolve(view.StatsTo, DateTime.Today);
        }
        ShowPeriodDates();
        SelectComboItemByTag(StatsBreakdownComboBox, view.StatsBreakdown);
        SelectComboItemByTag(StatsOverTimeComboBox, view.StatsOverTime);
        StatsSummaryCheckBox.IsChecked = view.StatsShowSummary;
        StatsBreakdownCheckBox.IsChecked = view.StatsShowBreakdown;
        StatsOverTimeCheckBox.IsChecked = view.StatsShowOverTime;
        StatsColumnTimesCheckBox.IsChecked = view.StatsShowColumnTimes;
        StatsChartsCheckBox.IsChecked = view.StatsShowCharts;

        // Checking a radio button that is already checked raises nothing, so the fields are shown here too.
        (view.IsStatistics ? StatisticsRadio : TaskListRadio).IsChecked = true;
        ShowOptionsForReportType();
    }

    // At least one section, or the report would be just its title.
    private bool StatisticsHaveASection()
    {
        if (StatsSummaryCheckBox.IsChecked == true || StatsBreakdownCheckBox.IsChecked == true
            || StatsOverTimeCheckBox.IsChecked == true || StatsColumnTimesCheckBox.IsChecked == true) return true;

        Dialogs.Tell(this, "Nothing to Show", "Tick at least one section to include in the statistics report.", DialogTone.Warning);
        return false;
    }
}
