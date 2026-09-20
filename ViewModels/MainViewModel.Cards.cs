using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Core card CRUD: create, full edit, the board's quick-edit setters (priority/due date/who/
// project/flags/sub-tasks), delete, and move between columns (including the recurring-task
// next-occurrence spawn hookup in MoveCard).
public partial class MainViewModel
{
    public CardViewModel AddCard(string title, ColumnViewModel column, ProjectViewModel? project, string priority, DateTime? dueDate, PersonViewModel? who,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags = null, List<SubTaskViewModel>? subTasks = null,
        string? notes = null, bool isImported = false, List<AttachmentViewModel>? attachments = null, bool forceEditOnComplete = false,
        string? websiteUrl = null, string? dueTime = null, DateTime? startDate = null, string? waitingOn = null,
        IReadOnlyList<PersonViewModel>? people = null)
    {
        flags ??= [];
        subTasks ??= [];
        attachments ??= [];
        if (dueDate is null) dueTime = null;
        // `people` is everyone, lead first; callers with just one person pass `who` and leave it out.
        people ??= who is null ? [] : [who];
        using var undo = RecordUndo(DescribeAction("Add", title.Trim()), []);
        var card = _db.AddCard(column.Id, title.Trim(), project?.Id, column.Name, priority, dueDate, people.FirstOrDefault()?.Id, isRecurring, recurrencePattern, goal?.Id, notes, isImported, forceEditOnComplete, websiteUrl, dueTime, startDate,
            string.IsNullOrWhiteSpace(waitingOn) ? null : waitingOn.Trim());
        RememberWaitingOn(waitingOn);
        _db.SetCardFlags(card.Id, flags.Select(f => f.Id));
        _db.SetCardPeople(card.Id, people.Select(p => p.Id));
        var subTaskItems = _db.SetCardSubTasks(card.Id, subTasks.Select(s => (s.Title, s.IsDone)).ToList());
        var attachmentItems = _db.SetCardAttachments(card.Id, attachments.Select(a => (a.FilePath, a.DisplayName, a.AddedDate)).ToList());
        var cardVm = new CardViewModel(card)
        {
            ProjectName = project?.Name ?? "No Project",
            GoalName = goal?.Name ?? "No Goal",
            People = people,
            Flags = flags,
            SubTasks = subTaskItems.Select(s => new SubTaskViewModel(s)).ToList(),
            Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList(),
            LastUpdated = card.LastUpdated
        };
        column.Cards.Add(cardVm);
        _recording?.CreatedIds.Add(cardVm.Id);

        RefreshAfterCardChange(cardVm);

        return cardVm;
    }

    // Deliberately no default values: every field is overwritten, so a caller that left one out
    // would silently clear it on save. Without defaults, forgetting a field is a compile error.
    public void EditCard(CardViewModel card, string title, ColumnViewModel newColumn, ProjectViewModel? project, string priority, DateTime? dueDate, IReadOnlyList<PersonViewModel>? people,
        bool isRecurring, string? recurrencePattern, GoalViewModel? goal, List<FlagViewModel>? flags, List<SubTaskViewModel>? subTasks,
        string? notes, List<AttachmentViewModel>? attachments, bool forceEditOnComplete,
        string? websiteUrl, string? dueTime, DateTime? startDate, string? waitingOn)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        using var undo = RecordUndo(DescribeAction("Edit", [card]), [card]);

        flags ??= [];
        subTasks ??= [];
        attachments ??= [];
        var previousAttachments = card.Attachments;

        card.Title = title.Trim();
        card.ProjectId = project?.Id;
        card.ProjectName = project?.Name ?? "No Project";
        card.Priority = priority;
        card.DueDate = dueDate;
        card.DueTime = dueDate is null ? null : dueTime;
        card.StartDate = startDate;
        card.WaitingOn = waitingOn;
        RememberWaitingOn(waitingOn);
        card.People = people ?? [];
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
        PersistPeople(card);
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
        using var undo = RecordUndo(DescribeAction(isDone ? "Tick a sub-task of" : "Untick a sub-task of", [card]), [card]);
        subTask.IsDone = isDone;
        _db.SetSubTaskDone(subTask.Id, isDone);
        card.RefreshSubTaskProgress();
    }

    public void AddFlagToCard(CardViewModel card, FlagViewModel flag)
    {
        if (card.Flags.Any(f => f.Id == flag.Id)) return;

        using var undo = RecordUndo(DescribeAction("Flag", [card]), [card]);
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
            card.IsRecurring, card.RecurrencePattern, card.GoalId, card.Notes, card.ForceEditOnComplete, card.WebsiteUrl, card.DueTime, card.StartDate, card.WaitingOn);
    }

    // The follow-up every card change shares: re-test the changed card against the active filters,
    // re-apply the sort, and refresh the dashboard counts and overdue highlighting.
    private void RefreshAfterCardChange(CardViewModel? changed = null)
    {
        // The column's live list picks the change up by itself (see ColumnViewModel.CardsView).
        if (changed is not null) SetCardVisible(changed, MatchesFilters(changed));
        ApplySort();
        RefreshDashboardStats();
    }

    public void SetCardPriority(CardViewModel card, string priority)
    {
        if (card.Priority == priority) return;

        using var undo = RecordUndo(DescribeAction("Change priority of", [card]), [card]);
        card.Priority = priority;
        PersistCard(card);
        RefreshAfterCardChange(card);
    }

    public void SetCardDueDate(CardViewModel card, DateTime? dueDate)
    {
        if (card.DueDate == dueDate) return;

        using var undo = RecordUndo(DescribeAction("Change due date of", [card]), [card]);
        card.DueDate = dueDate;
        if (dueDate is null) card.DueTime = null;
        if (dueDate is not null && card.StartDate > dueDate) card.StartDate = dueDate; // a start can never be after the due date
        PersistCard(card);
        RefreshAfterCardChange(card);
    }

    // Makes this one person the whole assignment (or nobody, for null).
    public void SetCardWho(CardViewModel card, PersonViewModel? who) => SetCardPeople(card, who is null ? [] : [who]);

    // Everyone the task is assigned to, lead first.
    public void SetCardPeople(CardViewModel card, IReadOnlyList<PersonViewModel> people)
    {
        if (card.People.Select(p => p.Id).SequenceEqual(people.Select(p => p.Id))) return;

        using var undo = RecordUndo(DescribeAction("Reassign", [card]), [card]);
        card.People = people;
        PersistCard(card);
        PersistPeople(card);
        RefreshAfterCardChange(card);
    }

    // Ticks one person on or off a task, as the card's Assigned menu does. A newly ticked person
    // goes on the end, so the lead only changes when the lead is the one taken off.
    public void ToggleCardPerson(CardViewModel card, PersonViewModel person) =>
        SetCardPeople(card, card.IsAssignedTo(person.Id)
            ? card.People.Where(p => p.Id != person.Id).ToList()
            : [.. card.People, person]);

    private void PersistPeople(CardViewModel card) => _db.SetCardPeople(card.Id, card.People.Select(p => p.Id));

    // Blank or null clears it.
    public void SetCardWaitingOn(CardViewModel card, string? waitingOn)
    {
        var cleaned = string.IsNullOrWhiteSpace(waitingOn) ? null : waitingOn.Trim();
        if (card.WaitingOn == cleaned) return;

        using var undo = RecordUndo(DescribeAction(cleaned is null ? "Clear waiting-on of" : "Set waiting-on of", [card]), [card]);
        card.WaitingOn = cleaned;
        RememberWaitingOn(cleaned);
        PersistCard(card);
        RefreshAfterCardChange(card);
    }

    public void SetCardProject(CardViewModel card, ProjectViewModel project)
    {
        if (card.ProjectId == project.Id) return;

        using var undo = RecordUndo(DescribeAction("Change project of", [card]), [card]);
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

        using var undo = RecordUndo(DescribeAction("Delete", [card]), [card]);

        if (spawnNextOccurrence && card.IsRecurring && !string.IsNullOrWhiteSpace(card.RecurrencePattern) && !card.NextOccurrenceSpawned)
        {
            SpawnNextOccurrence(card);
        }

        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        ReconcileAttachmentLocations(card, "Deleted");
        column?.Cards.Remove(card);
        _recording?.Removed.TryAdd(card.Id, card);
        _db.DeleteCard(card.Id, card.Title, column?.Name ?? "Unknown");
        RefreshDashboardStats();
    }

    // A copy of the card in the same column with every field carried over, for starting a similar
    // task without retyping it. Sub-tasks come across unticked, since the copy is new work. Attached
    // files don't: each file lives in its own task's folder and is moved or removed along with that
    // task, so two tasks sharing one would lose it when either was archived or deleted.
    public CardViewModel? DuplicateCard(CardViewModel card)
    {
        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (column is null) return null;

        using var undo = RecordUndo(DescribeAction("Duplicate", [card]), []);

        var freshSubTasks = card.SubTasks
            .Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = false }))
            .ToList();

        return AddCard($"{card.Title} (copy)", column,
            Projects.FirstOrDefault(p => p.Id == card.ProjectId), card.Priority, card.DueDate,
            card.People.FirstOrDefault(), card.IsRecurring, card.RecurrencePattern,
            Goals.FirstOrDefault(g => g.Id == card.GoalId), [.. card.Flags], freshSubTasks, card.Notes,
            forceEditOnComplete: card.ForceEditOnComplete, websiteUrl: card.WebsiteUrl, dueTime: card.DueTime, startDate: card.StartDate,
            waitingOn: card.WaitingOn, people: [.. card.People]);
    }

    private void MoveCard(CardViewModel card, ColumnViewModel targetColumn)
    {
        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is null || sourceColumn == targetColumn) return;

        using var undo = RecordUndo(DescribeAction("Move", [card], $"to {targetColumn.DisplayName}"), [card]);

        sourceColumn.Cards.Remove(card);
        card.ColumnId = targetColumn.Id;
        targetColumn.Cards.Add(card);

        card.LastUpdated = _db.MoveCard(card.Id, targetColumn.Id, card.Title, sourceColumn.Name, targetColumn.Name);
        card.CompletedAt = targetColumn.Name == "Done" ? card.LastUpdated : null;
        ReconcileAttachmentLocations(card, targetColumn.Name == "Done" ? "Done" : null);

        // A finished task isn't waiting on anything any more.
        if (targetColumn.Name == "Done" && card.IsWaiting)
        {
            card.WaitingOn = null;
            PersistCard(card);
        }

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
