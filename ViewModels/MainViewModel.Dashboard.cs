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

        return _db.GetCards(archivedOnly: true).Select(card =>
        {
            var cardVm = new CardViewModel(card)
            {
                ProjectName = ResolveProjectName(card.ProjectId),
                GoalName = ResolveGoalName(card.GoalId),
                WhoName = ResolveWhoName(card.WhoId),
                Flags = ResolveFlags(card.FlagIds),
                SubTasks = card.SubTasks.Select(s => new SubTaskViewModel(s)).ToList(),
                Attachments = card.Attachments.Select(a => new AttachmentViewModel(a)).ToList(),
                LastUpdated = card.LastUpdated
            };
            var columnName = displayNameById.GetValueOrDefault(card.ColumnId, "Unknown");
            return (cardVm, columnName);
        }).ToList();
    }

    public List<CardViewModel> GetDueReminders() =>
        Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards)
            .Where(c => c.DueDate is not null && c.DueDate.Value.Date <= DateTime.Today)
            .OrderBy(c => c.DueDate)
            .ThenBy(c => PriorityRank(c.Priority))
            .ThenBy(c => c.Title)
            .ToList();

    private void RefreshDashboardStats()
    {
        OnPropertyChanged(nameof(OpenTaskCount));
        OnPropertyChanged(nameof(OverdueCount));
        OnPropertyChanged(nameof(DueTodayCount));
        OnPropertyChanged(nameof(DueThisWeekCount));

        foreach (var column in Columns)
        {
            var canBeOverdue = column.Name != "Done";
            foreach (var card in column.Cards)
            {
                card.IsOverdue = canBeOverdue && card.DueDate is not null && card.DueDate.Value.Date < DateTime.Today;
            }
        }
    }
}
