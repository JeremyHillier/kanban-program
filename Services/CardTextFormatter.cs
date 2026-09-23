using System.Text;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// The plain-text version of a task that the board's right-click "Copy as Text" puts on the
// clipboard, for pasting into a chat, a document, or an email written by hand. It lists every
// field that has a value and leaves out the ones that don't, so a bare task copies as just its
// title and the few fields every task has. Due-date and goal wording is shared with the plain-text
// email so the two can't drift apart.
internal static class CardTextFormatter
{
    public static string Format(CardViewModel card, string columnName)
    {
        var sb = new StringBuilder();
        sb.Append(card.Title).Append("\r\n\r\n");
        sb.Append("Status: ").Append(columnName).Append("\r\n");
        sb.Append("Project: ").Append(card.ProjectName).Append("\r\n");
        sb.Append("Priority: ").Append(card.Priority).Append("\r\n");
        if (card.IsWaiting) sb.Append("Waiting on: ").Append(card.WaitingOn).Append("\r\n");
        if (card.StartDate.HasValue) sb.Append("Start: ").Append(OutlookEmailHelper.FormatStart(card)).Append("\r\n");
        if (card.DueDate.HasValue) sb.Append("Due: ").Append(OutlookEmailHelper.FormatDue(card)).Append("\r\n");
        if (card.WhoId.HasValue) sb.Append("Assigned to: ").Append(card.WhoName).Append("\r\n");
        if (OutlookEmailHelper.HasGoal(card)) sb.Append("Goal: ").Append(card.GoalName).Append("\r\n");
        if (card.Flags.Count > 0) sb.Append("Flags: ").Append(string.Join(", ", card.Flags.Select(f => f.Name))).Append("\r\n");
        if (card.IsRecurring && !string.IsNullOrWhiteSpace(card.RecurrencePattern)) sb.Append("Repeats: ").Append(card.RecurrencePattern).Append(RepeatsLeftText(card.RecurrencesLeft)).Append("\r\n");
        if (card.CompletedFullDisplay is { } completed) sb.Append(completed).Append("\r\n");
        if (!string.IsNullOrWhiteSpace(card.WebsiteUrl)) sb.Append("Website: ").Append(card.WebsiteUrl.Trim()).Append("\r\n");

        if (!string.IsNullOrWhiteSpace(card.Notes))
        {
            sb.Append("\r\nNotes:\r\n").Append(card.Notes.Replace("\r\n", "\n").Replace("\n", "\r\n")).Append("\r\n");
        }

        if (card.SubTasks.Count > 0)
        {
            sb.Append("\r\nSub-tasks:\r\n");
            foreach (var subTask in card.SubTasks)
            {
                sb.Append(subTask.IsDone ? "[x] " : "[ ] ").Append(subTask.Title).Append("\r\n");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // After the pattern: nothing for a task with no end, otherwise how many are still to come.
    internal static string RepeatsLeftText(int? recurrencesLeft) => recurrencesLeft switch
    {
        null => string.Empty,
        <= 1 => " (this is the last one)",
        var left => $" ({left - 1} more after this one)"
    };
}
