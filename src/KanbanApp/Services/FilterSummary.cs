using System.Globalization;
using KanbanApp.Models;

namespace KanbanApp.Services;

// What the board is being narrowed by, in words, for the line under the banner's title. Every
// kind of filter is named: the day buttons, the due-date range, the lists, goal, flag, waiting,
// keyword, and Hide Future (a view setting rather than a filter, but it hides tasks all the same).
public static class FilterSummary
{
    public const string Separator = "  ·  ";

    // One entry per filter in force, in the order the button column shows them. Empty when
    // nothing is hiding anything.
    public static List<string> Parts(CustomFilter state, bool hideFuture)
    {
        var parts = new List<string>();

        if (state.Due == "Waiting On") parts.Add("Waiting on anything");
        else if (state.Due != "All") parts.Add(state.Due);

        var from = Day(state.DueFrom);
        var to = Day(state.DueTo);
        if (from is not null && to is not null) parts.Add($"Due {from} to {to}");
        else if (from is not null) parts.Add($"Due from {from}");
        else if (to is not null) parts.Add($"Due up to {to}");

        if (state.Project.Count > 0) parts.Add($"Project: {string.Join(", ", state.Project)}");
        if (state.Priority.Count > 0) parts.Add($"Priority: {string.Join(", ", state.Priority)}");
        if (state.Who.Count > 0) parts.Add($"Who: {string.Join(", ", state.Who)}");
        if (state.Goal != "All") parts.Add($"Goal: {state.Goal}");
        if (state.Flag != "All") parts.Add($"Flag: {state.Flag}");
        if (state.WaitingOn != "All")
        {
            parts.Add(state.WaitingOn switch
            {
                "Any" => "Waiting on anything",
                "Unassigned" => "Not waiting",
                var answer => $"Waiting on: {answer}"
            });
        }
        if (!string.IsNullOrWhiteSpace(state.Keyword)) parts.Add($"Keyword: \"{state.Keyword.Trim()}\"");
        if (hideFuture) parts.Add("Future tasks hidden");

        return parts.Distinct().ToList(); // the Waiting On button and the Waiting list can both say "anything"
    }

    // The whole line: a saved filter's name first when the board matches one, then the parts, then
    // how many tasks are showing. Empty when nothing is filtering.
    public static string Line(CustomFilter state, bool hideFuture, string? savedFilterLabel, int shown, int total)
    {
        var parts = Parts(state, hideFuture);
        if (parts.Count == 0) return string.Empty;

        var what = string.Join(Separator, parts);
        if (savedFilterLabel is not null) what = $"{savedFilterLabel}: {what}";
        return $"{what}{Separator}{shown} of {total} {(total == 1 ? "task" : "tasks")}";
    }

    private static string? Day(string? stored) =>
        DateTime.TryParseExact(stored, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString(date.Year == DateTime.Today.Year ? "MMM d" : "MMM d, yyyy")
            : null;

    // Whether two filters narrow the board the same way (names and lists in any order).
    public static bool Same(CustomFilter a, CustomFilter b) =>
        SameSet(a.Project, b.Project) && SameSet(a.Priority, b.Priority) && SameSet(a.Who, b.Who)
        && a.Goal == b.Goal && a.Flag == b.Flag && a.WaitingOn == b.WaitingOn && a.Due == b.Due
        && (a.DueFrom ?? "") == (b.DueFrom ?? "") && (a.DueTo ?? "") == (b.DueTo ?? "")
        && (a.Keyword ?? "").Trim() == (b.Keyword ?? "").Trim();

    private static bool SameSet(List<string> a, List<string> b) => a.Count == b.Count && !a.Except(b).Any();
}
