using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// Excel task import: turning reviewed ImportedTaskRow rows into real cards (auto-creating any
// Project/Goal/Who value that doesn't exist yet), plus the "still marked imported" bookkeeping
// used by the imported-tasks review window.
public partial class MainViewModel
{
    public List<CardViewModel> GetImportedCards() =>
        Columns.SelectMany(c => c.Cards).Where(c => c.IsImported).ToList();

    public void SetCardImported(CardViewModel card, bool isImported)
    {
        if (card.IsImported == isImported) return;

        card.IsImported = isImported;
        _db.SetCardImported(card.Id, isImported);
    }

    // A task already on the board with this share ID (see CardItem.ShareId), or null. Archived and
    // deleted tasks are not looked at: a task sent again after it was dealt with comes back as new.
    public CardViewModel? FindSharedCard(string? shareId) =>
        string.IsNullOrWhiteSpace(shareId) ? null
            : Columns.SelectMany(c => c.Cards).FirstOrDefault(c => string.Equals(c.ShareId, shareId, StringComparison.OrdinalIgnoreCase));

    // The ID a task carries when it is emailed, given the first time it is shared.
    public string EnsureShareId(CardViewModel card)
    {
        if (!string.IsNullOrWhiteSpace(card.ShareId)) return card.ShareId;

        var id = Guid.NewGuid().ToString("N");
        card.ShareId = id;
        _db.SetCardShareId(card.Id, id);
        return id;
    }

    // What the last ImportCards did, for the message the window shows afterwards.
    public ImportSummary LastImport { get; private set; } = ImportSummary.None;

    // A column a file names: by the column's own name (To Do, In Progress, On Hold, Waiting, Done), which
    // is the same on every board whatever it has been renamed to, else by what this board calls it.
    public ColumnViewModel? FindColumnForImport(string? text)
    {
        var name = text?.Trim();
        if (string.IsNullOrEmpty(name)) return null;

        return Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? Columns.FirstOrDefault(c => string.Equals(c.DisplayName, name, StringComparison.OrdinalIgnoreCase));
    }

    private string? ColumnKey(string? text) => FindColumnForImport(text)?.Name ?? text?.Trim();

    // Returns every task the import added or updated. A row carrying the ID of a task already on
    // the board updates that task (everything the file says, except its attachments and the
    // edit-when-done setting, which are this board's own); any other row adds a new task.
    //
    // A returned task that has changed on both sides is a clash (ShareSync): onConflict says what to do,
    // and with none given the file wins, as it always did. Their copy only changes the board when it
    // has something new: one identical to the board's, or still the copy that went out, changes nothing.
    public List<CardViewModel> ImportCards(IEnumerable<ImportedTaskRow> rows, Func<ImportConflict, ConflictChoice>? onConflict = null)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do") ?? Columns.First();
        var imported = new List<CardViewModel>();
        var importing = rows.Where(r => !string.IsNullOrWhiteSpace(r.Title)).ToList();
        int added = 0, updated = 0, unchanged = 0, keptMine = 0, keptBoth = 0;

        // One Undo step for the whole import. It takes new tasks away again and puts updated ones
        // back as they were; any project, goal, person or flag the import added to the lists stays.
        using var undo = RecordUndo($"Import {importing.Count} task{(importing.Count == 1 ? "" : "s")}", []);

        foreach (var row in importing)
        {
            if (string.IsNullOrWhiteSpace(row.Title)) continue;

            var existing = FindSharedCard(row.ShareId);
            var asCopy = false;
            if (existing is not null)
            {
                var mine = OutlookEmailHelper.BuildImportRow(existing, this);
                var mineFingerprint = ShareSync.Fingerprint(mine, ColumnKey);
                var theirFingerprint = ShareSync.Fingerprint(row, ColumnKey);
                var exchanged = _db.GetSharedTask(existing.ShareId)?.Fingerprint;

                if (theirFingerprint == mineFingerprint || exchanged is not null && exchanged == theirFingerprint)
                {
                    unchanged++; // the same task, or the very copy that went out: nothing new in it
                    continue;
                }

                if (exchanged is not null && exchanged != mineFingerprint)
                {
                    var choice = onConflict?.Invoke(new ImportConflict(existing, row, ShareSync.Differences(mine, row, ColumnKey), FromLabel(row)))
                        ?? ConflictChoice.UseTheirs;
                    if (choice == ConflictChoice.KeepMine)
                    {
                        keptMine++;
                        continue;
                    }
                    if (choice == ConflictChoice.KeepBoth)
                    {
                        existing = null;
                        asCopy = true;
                    }
                }
            }

            // A column this board doesn't have leaves an updated task where it is.
            var column = existing is null ? toDoColumn : Columns.FirstOrDefault(c => c.Cards.Contains(existing)) ?? toDoColumn;
            if (FindColumnForImport(row.Category) is { } named) column = named;

            // A priority that is not on this task file's list becomes the default one.
            var priority = Priorities.Resolve(row.Priority);

            ProjectViewModel? project = null;
            if (!string.IsNullOrWhiteSpace(row.Project))
            {
                var name = row.Project.Trim();
                project = Projects.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (project is null)
                {
                    AddProject(name);
                    project = Projects.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            GoalViewModel? goal = null;
            if (!string.IsNullOrWhiteSpace(row.Goal))
            {
                var name = row.Goal.Trim();
                goal = Goals.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
                if (goal is null)
                {
                    AddGoal(name);
                    goal = Goals.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Several people go in the one Who cell, separated by semicolons; the first is the lead.
            var people = new List<PersonViewModel>();
            foreach (var name in SplitPeopleNames(row.Who))
            {
                var person = People.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (person is null)
                {
                    AddPerson(name);
                    person = People.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                }

                if (person is not null && people.All(p => p.Id != person.Id)) people.Add(person);
            }

            // Flags as the Who cell does people: semicolons between them, a new one added to the list.
            var flags = new List<FlagViewModel>();
            foreach (var name in SplitPeopleNames(row.Flags))
            {
                var flag = Flags.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) ?? AddFlag(name).Item;
                if (flag is not null && flags.All(f => f.Id != flag.Id)) flags.Add(flag);
            }

            // A recurring task carries on repeating here. A time needs a date, as on the task screen.
            var pattern = RecurrencePatterns.Find(row.RecurrencePattern);
            var subTasks = row.SubTasks.Select(s => new SubTaskViewModel(new SubTaskItem { Title = s.Title, IsDone = s.IsDone })).ToList();

            var notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim();
            var website = SafeWebsite(row.WebsiteUrl);
            var dueTime = row.DueDate is null ? null : row.DueTime;
            var startDate = StartNoLaterThanDue(row.StartDate, row.DueDate);
            int? recurrencesLeft = pattern is not null && row.RecurrenceCount is > 0 ? row.RecurrenceCount : null;

            if (existing is not null)
            {
                EditCard(existing, row.Title.Trim(), column, project, priority, row.DueDate, people,
                    pattern is not null, pattern, goal, flags, subTasks, notes, existing.Attachments, existing.ForceEditOnComplete,
                    website, dueTime, startDate, row.WaitingOn, recurrencesLeft);
                SetCardImported(existing, true); // listed on the review screen with the new tasks
                RememberExchanged(existing);
                imported.Add(existing);
                updated++;
                continue;
            }

            var title = row.Title.Trim() + (asCopy ? " (theirs)" : "");
            var cardVm = AddCard(title, column, project, priority, row.DueDate, people.FirstOrDefault(),
                pattern is not null, pattern, goal, flags, subTasks,
                notes: notes, isImported: true, websiteUrl: website, dueTime: dueTime, startDate: startDate,
                waitingOn: row.WaitingOn, people: people, recurrencesLeft: recurrencesLeft);

            // Keeps the sender's ID, so a later update of the same task finds this one, and who sent it,
            // so it can be sent back. A copy kept beside the board's own task is a task of its own.
            if (!asCopy && row.ShareId is { } shareId)
            {
                cardVm.ShareId = shareId;
                _db.SetCardShareId(cardVm.Id, shareId);
                if (row.SenderName is not null || row.SenderEmail is not null) _db.SaveSharedSender(shareId, row.SenderName, row.SenderEmail);
                RememberExchanged(cardVm);
            }

            imported.Add(cardVm);
            if (asCopy) keptBoth++; else added++;
        }

        LastImport = new ImportSummary(added, updated, unchanged, keptMine, keptBoth);
        return imported;
    }

    private static string FromLabel(ImportedTaskRow row) =>
        !string.IsNullOrWhiteSpace(row.SenderName) ? $"{row.SenderName.Trim()}'s" : "the returned";

    // What the board's copy looked like when it last went out or came in, kept so a returned copy can
    // be told from a clash. Only for a task that has a Task ID: one never shared has nothing to compare.
    private void RememberExchanged(CardViewModel card)
    {
        if (string.IsNullOrWhiteSpace(card.ShareId)) return;

        _db.SaveSharedFingerprint(card.ShareId, ShareSync.Fingerprint(OutlookEmailHelper.BuildImportRow(card, this), ColumnKey));
    }

    // Who sent this task here, if it was shared with this board; null for a task that began here.
    public SharedTaskInfo? GetSharedTaskInfo(CardViewModel card) => _db.GetSharedTask(card.ShareId);

    // The same, only when there is somebody to send it back to.
    public SharedTaskInfo? SenderOf(CardViewModel card) => GetSharedTaskInfo(card) is { HasSender: true } info ? info : null;

    // "Sam Lee; Priya Patel" -> the names, trimmed, blanks and repeats dropped. A name with a comma
    // in it ("Lee, Sam") stays whole, which is why the separator is a semicolon.
    internal static List<string> SplitPeopleNames(string? cell) =>
        (cell ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // A file can come from anyone: a link that isn't a web address, an email address or a file or folder
    // path (a program or script, say) is left off, the same rule the task screen applies when saving.
    private static string? SafeWebsite(string? text) => Services.UrlLauncher.ValidLink(text);

    // A spreadsheet can say anything. A start date after the due date makes no sense on the board
    // (the task dialog refuses it), so it is pulled back to the due date rather than rejected.
    private static DateTime? StartNoLaterThanDue(DateTime? startDate, DateTime? dueDate) =>
        startDate is not null && dueDate is not null && startDate.Value.Date > dueDate.Value.Date ? dueDate.Value.Date : startDate?.Date;
}
