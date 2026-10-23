using System.Text.Json;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Which tasks the Timeline shows, by who they are for, their priority and their flags. Within one list a task
// matches if it has any of the ticked entries; across lists it must match each list that has something ticked.
// An empty filter shows everything. People and flags are held by id (a rename changes nothing), priorities by name.
public sealed record TimelineFilter(IReadOnlySet<int> PersonIds, IReadOnlySet<string> Priorities, IReadOnlySet<int> FlagIds)
{
    public static readonly TimelineFilter None = new(new HashSet<int>(), new HashSet<string>(StringComparer.Ordinal), new HashSet<int>());

    public bool IsEmpty => PersonIds.Count == 0 && Priorities.Count == 0 && FlagIds.Count == 0;

    public int Count => PersonIds.Count + Priorities.Count + FlagIds.Count;

    public bool Matches(CardViewModel card) =>
        (PersonIds.Count == 0 || card.People.Any(p => PersonIds.Contains(p.Id)))
        && (Priorities.Count == 0 || Priorities.Contains(card.Priority))
        && (FlagIds.Count == 0 || card.Flags.Any(f => FlagIds.Contains(f.Id)));

    // "Who: Sam, Pat; Priority: High; Flags: Urgent", from the names now in the lists (an entry deleted since is left out).
    public string Describe(IEnumerable<PersonViewModel> people, IEnumerable<FlagViewModel> flags)
    {
        var parts = new List<string>();
        var names = people.Where(p => PersonIds.Contains(p.Id)).Select(p => p.Name).ToList();
        if (names.Count > 0) parts.Add($"Who: {string.Join(", ", names)}");
        if (Priorities.Count > 0) parts.Add($"Priority: {string.Join(", ", Priorities.Order(StringComparer.OrdinalIgnoreCase))}");
        var flagNames = flags.Where(f => FlagIds.Contains(f.Id)).Select(f => f.Name).ToList();
        if (flagNames.Count > 0) parts.Add($"Flags: {string.Join(", ", flagNames)}");
        return string.Join("; ", parts);
    }

    private sealed record Saved(int[]? People, string[]? Priorities, int[]? Flags);

    public string ToJson() => JsonSerializer.Serialize(new Saved([.. PersonIds.Order()], [.. Priorities.Order(StringComparer.Ordinal)], [.. FlagIds.Order()]));

    // Anything unreadable is no filter.
    public static TimelineFilter FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return None;

        try
        {
            var saved = JsonSerializer.Deserialize<Saved>(json);
            return saved is null ? None : new TimelineFilter(
                new HashSet<int>(saved.People ?? []), new HashSet<string>(saved.Priorities ?? [], StringComparer.Ordinal), new HashSet<int>(saved.Flags ?? []));
        }
        catch (JsonException)
        {
            return None;
        }
    }
}
