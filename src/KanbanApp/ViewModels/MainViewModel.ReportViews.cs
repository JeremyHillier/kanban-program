using System.Collections.ObjectModel;
using System.Text.Json;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// Named, full Report Builder snapshots (see SavedReportView), persisted as one JSON array in the
// settings table - unbounded count, unlike the fixed ten Alt+0-9 CustomFilter slots.
public partial class MainViewModel
{
    private const string SavedReportViewsKey = "SavedReportViews";

    public ObservableCollection<SavedReportView> SavedReportViews { get; } = [];

    private void LoadSavedReportViews()
    {
        SavedReportViews.Clear();

        var json = _db.GetSetting(SavedReportViewsKey);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var views = JsonSerializer.Deserialize<List<SavedReportView>>(json);
                foreach (var view in (views ?? []).OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
                {
                    SavedReportViews.Add(view);
                }
            }
            catch
            {
                // A corrupted setting shouldn't take the app down - treat it as no saved views.
            }
        }

        AddStandardReportViews();
    }

    private const string StandardReportViewsAddedKey = "StandardReportViewsAdded";

    // A starting set of reports, added once to each task file: new ones, and ones from before the
    // set existed. They are ordinary saved views from then on - change or delete them, and they are
    // not put back. A name already in use is left as it is.
    private void AddStandardReportViews()
    {
        if (_db.GetFlag(StandardReportViewsAddedKey, false)) return;

        foreach (var view in StandardReportViews(Columns.Select(c => c.Name).ToList()))
        {
            if (SavedReportViews.Any(v => string.Equals(v.Name, view.Name, StringComparison.OrdinalIgnoreCase))) continue;
            SaveReportView(view);
        }
        _db.SetFlag(StandardReportViewsAddedKey, true);
    }

    internal static List<SavedReportView> StandardReportViews(IReadOnlyList<string> columnNames)
    {
        var open = columnNames.Where(n => n != "Done").ToList();
        SavedReportView OpenTasks(string name, string due, string groupBy, string sort1, string sort2) => new()
        {
            Name = name, Title = name, IncludedColumns = open, Due = due, GroupBy = groupBy,
            SortLevel1 = sort1, SortLevel2 = sort2, IncludeNotes = false, IncludeSubTasks = false
        };
        SavedReportView Statistics(string name, string period, string breakdown, string overTime) => new()
        {
            Name = name, Title = name, ReportType = SavedReportView.StatisticsType, IncludedColumns = [.. columnNames],
            StatsPeriod = period, StatsBreakdown = breakdown, StatsOverTime = overTime
        };

        return
        [
            OpenTasks("Overdue and Due Today", "Today", "Project", "Due Date", "Priority"),
            OpenTasks("Due Within a Week", "Within a Week", "Who", "Due Date", "Priority"),
            OpenTasks("Open Tasks by Person", "All", "Who", "Due Date", "Priority"),
            OpenTasks("Open Tasks by Project", "All", "Project", "Priority", "Due Date"),
            Statistics("Statistics: Last 30 Days", StatisticsPeriods.Last30Days, "Project", "Week"),
            Statistics("Statistics: Last Month by Person", StatisticsPeriods.LastMonth, "Who", "Week"),
            Statistics("Statistics: This Year by Project", StatisticsPeriods.ThisYear, "Project", "Month")
        ];
    }

    private void PersistSavedReportViews() =>
        _db.SetSetting(SavedReportViewsKey, JsonSerializer.Serialize(SavedReportViews.ToList()));

    // Overwrites an existing view of the same name, or adds a new one, keeping the list sorted.
    public void SaveReportView(SavedReportView view)
    {
        var existingIndex = -1;
        for (var i = 0; i < SavedReportViews.Count; i++)
        {
            if (string.Equals(SavedReportViews[i].Name, view.Name, StringComparison.OrdinalIgnoreCase)) { existingIndex = i; break; }
        }

        if (existingIndex >= 0)
        {
            SavedReportViews[existingIndex] = view;
        }
        else
        {
            var insertAt = 0;
            while (insertAt < SavedReportViews.Count &&
                   string.Compare(SavedReportViews[insertAt].Name, view.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertAt++;
            }
            SavedReportViews.Insert(insertAt, view);
        }

        PersistSavedReportViews();
    }

    public void DeleteReportView(string name)
    {
        var existing = SavedReportViews.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return;

        SavedReportViews.Remove(existing);
        PersistSavedReportViews();
    }
}

// Quick Report remembers which saved view was run last.
public partial class MainViewModel
{
    // When each task was added and every move it made, for the statistics report (TaskStatistics).
    public Dictionary<int, List<TaskEvent>> GetStatisticsHistory() => _db.GetStatisticsHistory();

    public string? QuickReportLastView => _db.GetSetting("QuickReportLastView");

    public void SetQuickReportLastView(string name) => _db.SetSetting("QuickReportLastView", name);
}
