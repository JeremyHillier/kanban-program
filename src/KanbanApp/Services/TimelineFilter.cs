using System.Text.Json;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Which tasks the Timeline shows, by who they are for, their priority, their flags, and the project and goal they
// belong to. Within one list a task matches if it has any of the ticked entries; across lists it must match each list
// that has something ticked. An empty filter shows everything. Everything but priorities is held by id (a rename
// changes nothing); priorities by name. A ticked project or goal also keeps the other projects' and goals' rows off
// the Timeline, not just their tasks (RowShown).
public sealed record TimelineFilter(
    IReadOnlySet<int> PersonIds, IReadOnlySet<string> Priorities, IReadOnlySet<int> FlagIds,
    IReadOnlySet<int> ProjectIds, IReadOnlySet<int> GoalIds)
{
    public static readonly TimelineFilter None = new(
        new HashSet<int>(), new HashSet<string>(StringComparer.Ordinal), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    public bool IsEmpty => Count == 0;

    public int Count => PersonIds.Count + Priorities.Count + FlagIds.Count + ProjectIds.Count + GoalIds.Count;

    public bool Matches(CardViewModel card) =>
        (PersonIds.Count == 0 || card.People.Any(p => PersonIds.Contains(p.Id)))
        && (Priorities.Count == 0 || Priorities.Contains(card.Priority))
        && (FlagIds.Count == 0 || card.Flags.Any(f => FlagIds.Contains(f.Id)))
        && (ProjectIds.Count == 0 || (card.ProjectId is { } project && ProjectIds.Contains(project)))
        && (GoalIds.Count == 0 || (card.GoalId is { } goal && GoalIds.Contains(goal)));

    // Whether a project's or goal's own row (its target, milestones and bar) may show. Null is "No Project" / "No Goal".
    public bool RowShown(IPlannedItem? item) => item is null
        ? (item is null && ProjectIds.Count == 0 && GoalIds.Count == 0)
        : item.Owner == Models.MilestoneOwner.Goal ? GoalIds.Count == 0 || GoalIds.Contains(item.Id) : ProjectIds.Count == 0 || ProjectIds.Contains(item.Id);

    // "Project: Website; Goal: Grow; Who: Sam, Pat; Priority: High; Flags: Urgent", from the names now in the lists
    // (an entry deleted since is left out).
    public string Describe(IEnumerable<PersonViewModel> people, IEnumerable<FlagViewModel> flags,
        IEnumerable<ProjectViewModel>? projects = null, IEnumerable<GoalViewModel>? goals = null)
    {
        var parts = new List<string>();
        var projectNames = (projects ?? []).Where(p => ProjectIds.Contains(p.Id)).Select(p => p.Name).ToList();
        if (projectNames.Count > 0) parts.Add($"Project: {string.Join(", ", projectNames)}");
        var goalNames = (goals ?? []).Where(g => GoalIds.Contains(g.Id)).Select(g => g.Name).ToList();
        if (goalNames.Count > 0) parts.Add($"Goal: {string.Join(", ", goalNames)}");
        var names = people.Where(p => PersonIds.Contains(p.Id)).Select(p => p.Name).ToList();
        if (names.Count > 0) parts.Add($"Who: {string.Join(", ", names)}");
        if (Priorities.Count > 0) parts.Add($"Priority: {string.Join(", ", Priorities.Order(StringComparer.OrdinalIgnoreCase))}");
        var flagNames = flags.Where(f => FlagIds.Contains(f.Id)).Select(f => f.Name).ToList();
        if (flagNames.Count > 0) parts.Add($"Flags: {string.Join(", ", flagNames)}");
        return string.Join("; ", parts);
    }

    private sealed record Saved(int[]? People, string[]? Priorities, int[]? Flags, int[]? Projects = null, int[]? Goals = null);

    public string ToJson() => JsonSerializer.Serialize(new Saved(
        [.. PersonIds.Order()], [.. Priorities.Order(StringComparer.Ordinal)], [.. FlagIds.Order()], [.. ProjectIds.Order()], [.. GoalIds.Order()]));

    // Anything unreadable is no filter; an older saved filter has no projects or goals.
    public static TimelineFilter FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return None;

        try
        {
            var saved = JsonSerializer.Deserialize<Saved>(json);
            return saved is null ? None : new TimelineFilter(
                new HashSet<int>(saved.People ?? []), new HashSet<string>(saved.Priorities ?? [], StringComparer.Ordinal), new HashSet<int>(saved.Flags ?? []),
                new HashSet<int>(saved.Projects ?? []), new HashSet<int>(saved.Goals ?? []));
        }
        catch (JsonException)
        {
            return None;
        }
    }
}
