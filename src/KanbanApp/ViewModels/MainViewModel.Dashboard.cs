using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Header-strip stats (open/overdue/due-today/due-this-week counts), the due-date reminders list,
// and the archived-tasks report-row projection used by Report Builder.
public partial class MainViewModel
{
    public int OpenTaskCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards).Count();

    public int OverdueCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate is not null && c.DueDate.Value.Date < DateTime.Today);

    public int DueTodayCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate?.Date == DateTime.Today);

    public int DueThisWeekCount => Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
        .Count(c => c.DueDate is not null && c.DueDate.Value.Date >= DateTime.Today && c.DueDate.Value.Date <= DateTime.Today.AddDays(7));

    public List<(CardViewModel Card, string ColumnName)> GetArchivedReportRows()
    {
        var displayNameById = Columns.ToDictionary(c => c.Id, c => c.DisplayName);

        // Built the same way as a task on the board (BuildCardViewModel).
        return _db.GetCards(archivedOnly: true)
            .Select(card => (BuildCardViewModel(card), displayNameById.GetValueOrDefault(card.ColumnId, "Unknown")))
            .ToList();
    }

    // When each archived task was completed - archived tasks still count in the Dashboard's
    // "completed per week" and "done in the last 7 days". Tasks archived without finishing have none.
    public List<DateTime> GetArchivedCompletionDates() =>
        _db.GetCards(archivedOnly: true).Where(c => c.CompletedAt is not null).Select(c => c.CompletedAt!.Value).ToList();

    public List<CardViewModel> GetDueReminders() =>
        Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
            .Where(c => c.DueDate is not null && c.DueDate.Value.Date <= DateTime.Today)
            .OrderBy(c => c.DueDate)
            .ThenBy(c => Priorities.Rank(c.Priority))
            .ThenBy(c => c.Title)
            .ToList();

    // Open tasks with a due time whose moment has already arrived - the time-alert timer in
    // MainWindow diffs this against what it has already announced to find the newly due ones.
    public List<CardViewModel> GetCardsPastDueTime() =>
        Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
            .Where(c => c.DueDateTime is { } dueAt && dueAt <= DateTime.Now)
            .OrderBy(c => c.DueDateTime)
            .ThenBy(c => Priorities.Rank(c.Priority))
            .ThenBy(c => c.Title)
            .ToList();

    private void RefreshDashboardStats()
    {
        OnPropertyChanged(nameof(OpenTaskCount));
        OnPropertyChanged(nameof(OverdueCount));
        OnPropertyChanged(nameof(DueTodayCount));
        OnPropertyChanged(nameof(DueThisWeekCount));
        NotifySelectionChanged(); // a deleted, archived or moved card may have been selected
        NotifyHiddenTasksChanged();
        OnPropertyChanged(nameof(FutureTaskCount));
        OnPropertyChanged(nameof(HideFutureButtonLabel));
        OnPropertyChanged(nameof(WaitingOnCount));
        OnPropertyChanged(nameof(WaitingOnButtonLabel));

        foreach (var column in Columns)
        {
            var canBeOverdue = column.Name != "Done";
            foreach (var card in column.Cards)
            {
                card.IsOverdue = canBeOverdue && card.DueDate is not null && card.DueDate.Value.Date < DateTime.Today;
                card.RefreshStartDisplay(); // "Starts ..." depends on today's date too
            }
        }
    }
}
