using System.Text.Json.Serialization;
using KanbanApp.Services;

namespace KanbanApp.Models;

// A named, full snapshot of every Report Builder field - broader than a CustomFilter slot (which
// only covers the board's own filters), since it also captures columns, sort/group, scope,
// orientation, and the Notes/Sub-tasks toggles. Stored as a JSON array (unlike CustomFilter's fixed
// ten Alt+0-9 slots) since there's no keyboard-shortcut slot count to respect here.
public class SavedReportView
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = "Kanban Task Report";

    public List<string> IncludedColumns { get; set; } = [];

    public List<string> Project { get; set; } = [];
    public List<string> Priority { get; set; } = [];
    public List<string> Who { get; set; } = [];
    public string Goal { get; set; } = "All";
    public string Flag { get; set; } = "All";
    public string Due { get; set; } = "All";

    public string? DueFrom { get; set; }
    public string? DueTo { get; set; }
    public bool IncludeNoDueDate { get; set; }

    // Names of the saved Alt+0-9 custom filter slots selected for the union-match path, re-matched
    // by name against the live CustomFilters list when the view is applied (the CustomFilter
    // instances themselves aren't stable across sessions).
    public List<string> CustomFilterNames { get; set; } = [];

    public string SortLevel1 { get; set; } = "None";
    public string SortLevel2 { get; set; } = "None";
    public string SortLevel3 { get; set; } = "None";
    public string GroupBy { get; set; } = "None";

    public string ArchiveScope { get; set; } = "BoardOnly";
    public string? ArchivedFrom { get; set; }
    public string? ArchivedTo { get; set; }

    public bool IsLandscape { get; set; }
    public bool IncludeNotes { get; set; } = true;
    public bool IncludeSubTasks { get; set; } = true;
    public bool IncludeSubTaskSummary { get; set; }

    // "TaskList" or "Statistics". A view saved before statistics existed has none, so reads as a
    // task list. The fields below are the statistics report's own options (see TaskStatistics);
    // the filters above apply to both kinds.
    public string ReportType { get; set; } = TaskListType;
    public string StatsPeriod { get; set; } = StatisticsPeriods.Last30Days;
    public string? StatsFrom { get; set; } // custom period only, yyyy-MM-dd
    public string? StatsTo { get; set; }
    public string StatsBreakdown { get; set; } = "Project";
    public string StatsOverTime { get; set; } = "Week";
    public bool StatsShowSummary { get; set; } = true;
    public bool StatsShowBreakdown { get; set; } = true;
    public bool StatsShowOverTime { get; set; } = true;
    public bool StatsShowColumnTimes { get; set; } = true;
    public bool StatsShowCharts { get; set; } = true; // a view saved before charts existed gets them

    public const string TaskListType = "TaskList";
    public const string StatisticsType = "Statistics";

    [JsonIgnore]
    public bool IsStatistics => ReportType == StatisticsType;
}
