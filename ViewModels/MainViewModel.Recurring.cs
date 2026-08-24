using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Spawning a recurring task's next occurrence (triggered from MoveCard in MainViewModel.Cards.cs
// when a recurring card that hasn't spawned yet lands in Done) and the due-date math per pattern.
public partial class MainViewModel
{
    private void SpawnNextOccurrence(CardViewModel completedCard)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do");
        if (toDoColumn is null) return;

        var nextDueDate = CalculateNextDueDate(completedCard.DueDate ?? DateTime.Today, completedCard.RecurrencePattern!);
        var project = Projects.FirstOrDefault(p => p.Id == completedCard.ProjectId);
        var goal = Goals.FirstOrDefault(g => g.Id == completedCard.GoalId);
        var who = People.FirstOrDefault(p => p.Id == completedCard.WhoId);
        var freshSubTasks = completedCard.SubTasks
            .Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = false }))
            .ToList();

        AddCard(completedCard.Title, toDoColumn, project, completedCard.Priority, nextDueDate, who,
            true, completedCard.RecurrencePattern, goal, completedCard.Flags, freshSubTasks, completedCard.Notes,
            forceEditOnComplete: completedCard.ForceEditOnComplete);
    }

    private static DateTime CalculateNextDueDate(DateTime anchor, string pattern) => pattern switch
    {
        "Daily" => anchor.AddDays(1),
        "Weekday" => NextWeekday(anchor),
        "Weekly" => anchor.AddDays(7),
        "Bi-Weekly" => anchor.AddDays(14),
        "Monthly" => anchor.AddMonths(1),
        "Bi-Monthly" => anchor.AddMonths(2),
        "Quarterly" => anchor.AddMonths(3),
        "Annually" => anchor.AddYears(1),
        _ => anchor.AddDays(1)
    };

    private static DateTime NextWeekday(DateTime date)
    {
        var next = date.AddDays(1);
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }
        return next;
    }
}
