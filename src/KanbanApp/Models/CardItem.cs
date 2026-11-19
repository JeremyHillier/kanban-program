namespace KanbanApp.Models;

public class CardItem
{
    public int Id { get; set; }
    public int ColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? ProjectId { get; set; }
    public int? GoalId { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string Priority { get; set; } = "Normal";
    public DateTime? DueDate { get; set; }
    // Optional time of day ("HH:mm", 24-hour) on top of DueDate - drives the while-running time alert.
    public string? DueTime { get; set; }

    // Optional "not before" date: the task can't usefully be started until then. Date only.
    public DateTime? StartDate { get; set; }

    // Who or what the task is blocked by, in the user's own words. Empty or null means it isn't waiting.
    public string? WaitingOn { get; set; }
    public DateTime? CompletedAt { get; set; } // set while the task is in Done (or archived from it)
    public int? WhoId { get; set; } // the lead: always the first of PeopleIds
    public List<int> PeopleIds { get; set; } = [];
    public DateTime? LastUpdated { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public bool NextOccurrenceSpawned { get; set; }
    // How many times the task still happens, counting this one; null repeats with no end.
    public int? RecurrencesLeft { get; set; }

    // The task's permanent identity when shared: given the first time it is emailed, carried in the
    // Excel file, kept by whoever imports it. Null for a task never shared.
    public string? ShareId { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsImported { get; set; }
    public bool ForceEditOnComplete { get; set; }
    public string? Notes { get; set; }
    // A background colour for the card on the board, one of CardColors by key; null for the ordinary card colour.
    public string? Color { get; set; }

    // A Quick Add (Ctrl+Alt+N) task not yet reviewed in the full task window; cleared only by the user (Mark Reviewed).
    public bool NeedsReview { get; set; }
    public string? WebsiteUrl { get; set; }
    // What the task holds in the task file's custom fields, by field slot (1 to 10); see CustomFieldRules.
    public Dictionary<int, string> CustomValues { get; set; } = [];
    public List<int> FlagIds { get; set; } = [];
    public List<SubTaskItem> SubTasks { get; set; } = [];
    public List<CardAttachment> Attachments { get; set; } = [];
}
