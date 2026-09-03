namespace KanbanApp.ViewModels;

// Core card CRUD: create, full edit, the board's quick-edit setters (priority/due date/who/
// project/flags/sub-tasks), delete, and move between columns (including the recurring-task
// next-occurrence spawn hookup in MoveCard).
public partial class MainViewModel
{
    public CardViewModel AddCard(string title, ColumnViewModel column, ProjectViewModel? project, string priority, DateTime? dueDate, PersonViewModel? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags = null, List<SubTaskViewModel>? subTasks = null,
        string? notes = null, bool isImported = false, List<AttachmentViewModel>? attachments = null, bool forceEditOnComplete = false,
        string? websiteUrl = null)
    {
        flags ??= [];
        subTasks ??= [];
        attachments ??= [];
        var card = _db.AddCard(column.Id, title.Trim(), project?.Id, column.Name, priority, dueDate, who?.Id, isRecurring, recurrencePattern, goal?.Id, notes, isImported, forceEditOnComplete, websiteUrl);
        _db.SetCardFlags(card.Id, flags.Select(f => f.Id));
        var subTaskItems = _db.SetCardSubTasks(card.Id, subTasks.Select(s => (s.Title, s.IsDone)).ToList());
        var attachmentItems = _db.SetCardAttachments(card.Id, attachments.Select(a => (a.FilePath, a.DisplayName, a.AddedDate)).ToList());
        var cardVm = new CardViewModel(card)
        {
            ProjectName = project?.Name ?? "No Project",
            GoalName = goal?.Name ?? "No Goal",
            WhoName = who?.Name ?? "Unassigned",
            WhoEmail = who?.Email,
            Flags = flags,
            SubTasks = subTaskItems.Select(s => new SubTaskViewModel(s)).ToList(),
            Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList(),
            LastUpdated = card.LastUpdated
        };
        column.Cards.Add(cardVm);

        RefreshAfterCardChange(cardVm);

        return cardVm;
    }

    public void EditCard(CardViewModel card, string title, ColumnViewModel newColumn, ProjectViewModel? project, string priority, DateTime? dueDate, PersonViewModel? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags = null, List<SubTaskViewModel>? subTasks = null,
        string? notes = null, List<AttachmentViewModel>? attachments = null, bool forceEditOnComplete = false,
        string? websiteUrl = null)
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
        card.WhoEmail = who?.Email;
        card.IsRecurring = isRecurring;
        card.RecurrencePattern = recurrencePattern;
        card.GoalId = goal?.Id;
        card.GoalName = goal?.Name ?? "No Goal";
        card.Flags = flags;
        card.Notes = notes;
        card.ForceEditOnComplete = forceEditOnComplete;
        card.WebsiteUrl = websiteUrl;

        PersistCard(card);
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

        RefreshAfterCardChange(card);
    }

    public void AddAttachmentToCard(CardViewModel card, string filePath, string displayName)
    {
        var updatedAttachments = card.Attachments
            .Select(a => (a.FilePath, a.DisplayName, a.AddedDate))
            .Append((filePath, displayName, DateTime.Now))
            .ToList();

        var attachmentItems = _db.SetCardAttachments(card.Id, updatedAttachments);
        card.Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList();
        PersistCard(card);
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

    // Writes the card's current state back to the database. Every caller below had previously spelled
    // out the same twelve-argument UpdateCard call, each assigning the changed property to the card
    // first and then passing that same value through again - which is how a newly added column can
    // silently get dropped from one path but not the others. Reading straight off the card removes
    // that whole failure mode: a new field is added in one place.
    private void PersistCard(CardViewModel card)
    {
        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, card.ProjectId, card.Priority, card.DueDate, card.WhoId,
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete, card.WebsiteUrl);
    }

    // The follow-up every card change shares: re-test the changed card against the active filters,
    // re-apply the sort, and refresh the dashboard counts and overdue highlighting.
    private void RefreshAfterCardChange(CardViewModel? changed = null)
    {
        if (changed is not null) changed.IsVisible = MatchesFilters(changed);
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetCardPriority(CardViewModel card, string priority)
    {
        if (card.Priority == priority) return;

        card.Priority = priority;
        PersistCard(card);
        RefreshAfterCardChange(card);
    }

    public void SetCardDueDate(CardViewModel card, DateTime? dueDate)
    {
        if (card.DueDate == dueDate) return;

        card.DueDate = dueDate;
        PersistCard(card);
        RefreshAfterCardChange(card);
    }

    public void SetCardWho(CardViewModel card, PersonViewModel? who)
    {
        if (card.WhoId == who?.Id) return;

        card.WhoId = who?.Id;
        card.WhoName = who?.Name ?? "Unassigned";
        card.WhoEmail = who?.Email;
        PersistCard(card);
        RefreshAfterCardChange(card);
    }

    public void SetCardProject(CardViewModel card, ProjectViewModel project)
    {
        if (card.ProjectId == project.Id) return;

        card.ProjectId = project.Id;
        card.ProjectName = project.Name;
        PersistCard(card);
        RefreshAfterCardChange(card);
    }

    // spawnNextOccurrence lets deleting an incomplete recurring task ("skip today, but keep the
    // series going") behave like the MoveCard-to-Done completion path below, without requiring the
    // card to ever actually reach Done. Guarded the same way: only recurring, only if it hasn't
    // already spawned (so completing then later deleting the same card can't double-spawn).
    public void DeleteCard(CardViewModel? card, bool spawnNextOccurrence = false)
    {
        if (card is null) return;

        if (spawnNextOccurrence && card.IsRecurring && !string.IsNullOrWhiteSpace(card.RecurrencePattern) && !card.NextOccurrenceSpawned)
        {
            SpawnNextOccurrence(card);
        }

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

        // No card passed: moving between columns can't change whether a card matches the filters
        // (none of them look at the column), so re-testing it would be wasted work.
        RefreshAfterCardChange();
    }
}
