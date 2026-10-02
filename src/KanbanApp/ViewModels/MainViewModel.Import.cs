using KanbanApp.Models;

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

    // Returns every task the import added or updated. A row carrying the ID of a task already on
    // the board updates that task (everything the file says, except its attachments and the
    // edit-when-done setting, which are this board's own); any other row adds a new task.
    public List<CardViewModel> ImportCards(IEnumerable<ImportedTaskRow> rows)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do") ?? Columns.First();
        var imported = new List<CardViewModel>();
        var importing = rows.Where(r => !string.IsNullOrWhiteSpace(r.Title)).ToList();

        // One Undo step for the whole import. It takes new tasks away again and puts updated ones
        // back as they were; any project, goal, person or flag the import added to the lists stays.
        using var undo = RecordUndo($"Import {importing.Count} task{(importing.Count == 1 ? "" : "s")}", []);

        foreach (var row in importing)
        {
            if (string.IsNullOrWhiteSpace(row.Title)) continue;

            var existing = FindSharedCard(row.ShareId);

            // A column this board doesn't have (renamed, say) leaves an updated task where it is.
            var column = existing is null ? toDoColumn : Columns.FirstOrDefault(c => c.Cards.Contains(existing)) ?? toDoColumn;
            if (!string.IsNullOrWhiteSpace(row.Category))
            {
                var match = Columns.FirstOrDefault(c => string.Equals(c.DisplayName, row.Category.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match is not null) column = match;
            }

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
                imported.Add(existing);
                continue;
            }

            var cardVm = AddCard(row.Title.Trim(), column, project, priority, row.DueDate, people.FirstOrDefault(),
                pattern is not null, pattern, goal, flags, subTasks,
                notes: notes, isImported: true, websiteUrl: website, dueTime: dueTime, startDate: startDate,
                waitingOn: row.WaitingOn, people: people, recurrencesLeft: recurrencesLeft);

            // Keeps the sender's ID, so a later update of the same task finds this one.
            if (row.ShareId is { } shareId)
            {
                cardVm.ShareId = shareId;
                _db.SetCardShareId(cardVm.Id, shareId);
            }
            imported.Add(cardVm);
        }

        return imported;
    }

    // "Sam Lee; Priya Patel" -> the names, trimmed, blanks and repeats dropped. A name with a comma
    // in it ("Lee, Sam") stays whole, which is why the separator is a semicolon.
    internal static List<string> SplitPeopleNames(string? cell) =>
        (cell ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // A file can come from anyone: a link that isn't a web or email address (a program, a file share)
    // is left off, the same rule the task screen applies when saving.
    private static string? SafeWebsite(string? text) =>
        !string.IsNullOrWhiteSpace(text) && Services.UrlLauncher.TryNormalize(text, out _) ? text.Trim() : null;

    // A spreadsheet can say anything. A start date after the due date makes no sense on the board
    // (the task dialog refuses it), so it is pulled back to the due date rather than rejected.
    private static DateTime? StartNoLaterThanDue(DateTime? startDate, DateTime? dueDate) =>
        startDate is not null && dueDate is not null && startDate.Value.Date > dueDate.Value.Date ? dueDate.Value.Date : startDate?.Date;
}
