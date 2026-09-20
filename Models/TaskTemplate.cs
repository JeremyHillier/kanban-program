namespace KanbanApp.Models;

// A saved starting point for new tasks: everything about a task worth reusing, and nothing tied to
// one particular occurrence of it. So no column, attachments, waiting-on or completion state, and
// dates are kept as "so many days from the day the template is used" rather than as dates.
public class TaskTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public string Priority { get; set; } = "Normal";
    public int? WhoId { get; set; } // the lead
    // Everyone, lead first. Empty in a template saved before tasks could have several people,
    // which has just WhoId - AllPeopleIds covers both.
    public List<int> PeopleIds { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<int> AllPeopleIds => PeopleIds.Count > 0 ? PeopleIds : WhoId is { } lead ? [lead] : [];
    public int? GoalId { get; set; }
    public List<int> FlagIds { get; set; } = [];
    public List<string> SubTasks { get; set; } = [];
    public string? Notes { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public bool ForceEditOnComplete { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? DueTime { get; set; }

    // Null means the template sets no date. Never negative: a date already past when the template
    // was saved isn't a lead time worth keeping.
    public int? DueInDays { get; set; }
    public int? StartInDays { get; set; }

    // Days from "today" for a date, or null for no date or one already gone by.
    public static int? DaysFromToday(DateTime? date) =>
        date is { } d && d.Date >= DateTime.Today ? (d.Date - DateTime.Today).Days : null;

    public DateTime? DueDateFromToday => DueInDays is { } days ? DateTime.Today.AddDays(days) : null;
    public DateTime? StartDateFromToday => StartInDays is { } days ? DateTime.Today.AddDays(days) : null;
}
