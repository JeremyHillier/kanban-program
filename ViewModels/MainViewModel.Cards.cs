namespace KanbanApp.ViewModels;

// Core card CRUD: create, full edit, the board's quick-edit setters (priority/due date/who/
// project/flags/sub-tasks), delete, and move between columns (including the recurring-task
// next-occurrence spawn hookup in MoveCard).
public partial class MainViewModel
{
    public CardViewModel AddCard(string title, ColumnViewModel column, ProjectViewModel? project, string priority, DateTime? dueDate, PersonViewModel? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags = null, List<SubTaskViewModel>? subTasks = null,
        string? notes = null, bool isImported = false, List<AttachmentViewModel>? attachments = null, bool forceEditOnComplete = false)
    {
        flags ??= [];
        subTasks ??= [];
        attachments ??= [];
        var card = _db.AddCard(column.Id, title.Trim(), project?.Id, column.Name, priority, dueDate, who?.Id, isRecurring, recurrencePattern, goal?.Id, notes, isImported, forceEditOnComplete);
        _db.SetCardFlags(card.Id, flags.Select(f => f.Id));
        var subTaskItems = _db.SetCardSubTasks(card.Id, subTasks.Select(s => (s.Title, s.IsDone)).ToList());
        var attachmentItems = _db.SetCardAttachments(card.Id, attachments.Select(a => (a.FilePath, a.DisplayName, a.AddedDate)).ToList());
        var cardVm = new CardViewModel(card)
        {
            ProjectName = project?.Name ?? "No Project",
            GoalName = goal?.Name ?? "No Goal",
            WhoName = who?.Name ?? "Unassigned",
            Flags = flags,
            SubTasks = subTaskItems.Select(s => new SubTaskViewModel(s)).ToList(),
            Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList(),
            LastUpdated = card.LastUpdated
        };
        column.Cards.Add(cardVm);

        cardVm.IsVisible = MatchesFilters(cardVm);
        ApplySort();
        RefreshDashboardStats();

        return cardVm;
    }

    public void EditCard(CardViewModel card, string title, ColumnViewModel newColumn, ProjectViewModel? project, string priority, DateTime? dueDate, PersonViewModel? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags = null, List<SubTaskViewModel>? subTasks = null,
        string? notes = null, List<AttachmentViewModel>? attachments = null, bool forceEditOnComplete = false)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        flags ??= [];
        subTasks ??= [];
        attachments ??= [];
        var previousAttachments = card.Attachments;

        card.Title = title.Trim();
        card.ProjectId = project?.Id;
        card.ProjectName = project?.Name ?? "No Project";
        card.Priority = priority;
        card.DueDate = dueDate;
        card.WhoId = who?.Id;
        card.WhoName = who?.Name ?? "Unassigned";
        card.IsRecurring = isRecurring;
        card.RecurrencePattern = recurrencePattern;
        card.GoalId = goal?.Id;
        card.GoalName = goal?.Name ?? "No Goal";
        card.Flags = flags;
        card.Notes = notes;
        card.ForceEditOnComplete = forceEditOnComplete;

        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, project?.Id, priority, dueDate, who?.Id, isRecurring, recurrencePattern, goal?.Id, notes, forceEditOnComplete);
        _db.SetCardFlags(card.Id, flags.Select(f => f.Id));
        var subTaskItems = _db.SetCardSubTasks(card.Id, subTasks.Select(s => (s.Title, s.IsDone)).ToList());
        card.SubTasks = subTaskItems.Select(s => new SubTaskViewModel(s)).ToList();
        var attachmentItems = _db.SetCardAttachments(card.Id, attachments.Select(a => (a.FilePath, a.DisplayName, a.AddedDate)).ToList());
        card.Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList();
        DeleteOrphanedAttachmentFiles(card.Id, previousAttachments, attachments);

        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is not null && sourceColumn != newColumn)
        {
            MoveCard(card, newColumn);
        }

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    public void AddAttachmentToCard(CardViewModel card, string filePath, string displayName)
    {
        var updatedAttachments = card.Attachments
            .Select(a => (a.FilePath, a.DisplayName, a.AddedDate))
            .Append((filePath, displayName, DateTime.Now))
            .ToList();

        var attachmentItems = _db.SetCardAttachments(card.Id, updatedAttachments);
        card.Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList();
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, card.Priority, card.DueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);
    }

    public void SetSubTaskDone(CardViewModel card, SubTaskViewModel subTask, bool isDone)
    {
        subTask.IsDone = isDone;
        _db.SetSubTaskDone(subTask.Id, isDone);
        card.RefreshSubTaskProgress();
    }

    public void AddFlagToCard(CardViewModel card, FlagViewModel flag)
    {
        if (card.Flags.Any(f => f.Id == flag.Id)) return;

        card.Flags = card.Flags.Append(flag).ToList();
        _db.SetCardFlags(card.Id, card.Flags.Select(f => f.Id));
    }

    public void SetCardPriority(CardViewModel card, string priority)
    {
        if (card.Priority == priority) return;

        card.Priority = priority;
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, priority, card.DueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetCardDueDate(CardViewModel card, DateTime? dueDate)
    {
        if (card.DueDate == dueDate) return;

        card.DueDate = dueDate;
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, card.Priority, dueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetCardWho(CardViewModel card, PersonViewModel? who)
    {
        if (card.WhoId == who?.Id) return;

        card.WhoId = who?.Id;
        card.WhoName = who?.Name ?? "Unassigned";
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, card.Priority, card.DueDate, who?.Id,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetCardProject(CardViewModel card, ProjectViewModel project)
    {
        if (card.ProjectId == project.Id) return;

        card.ProjectId = project.Id;
        card.ProjectName = project.Name;
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, project.Id, card.Priority, card.DueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete);

        card.IsVisible = MatchesFilters(card);
        ApplySort();
        RefreshDashboardStats();
    }

    private void DeleteCard(CardViewModel? card)
    {
        if (card is null) return;

        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        ReconcileAttachmentLocations(card, "Deleted");
        column?.Cards.Remove(card);
        _db.DeleteCard(card.Id, card.Title, column?.Name ?? "Unknown");
        RefreshDashboardStats();
    }

    private void MoveCard(CardViewModel card, ColumnViewModel targetColumn)
    {
        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is null || sourceColumn == targetColumn) return;

        sourceColumn.Cards.Remove(card);
        card.ColumnId = targetColumn.Id;
        targetColumn.Cards.Add(card);

        card.LastUpdated = _db.MoveCard(card.Id, targetColumn.Id, card.Title, sourceColumn.Name, targetColumn.Name);
        ReconcileAttachmentLocations(card, targetColumn.Name == "Done" ? "Done" : null);

        if (targetColumn.Name == "Done" && card.IsRecurring && !string.IsNullOrWhiteSpace(card.RecurrencePattern) && !card.NextOccurrenceSpawned)
        {
            SpawnNextOccurrence(card);
            card.NextOccurrenceSpawned = true;
            _db.MarkNextOccurrenceSpawned(card.Id);
        }

        ApplySort();
        RefreshDashboardStats();
    }
}
