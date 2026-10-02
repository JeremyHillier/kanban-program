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

    public List<CardViewModel> ImportCards(IEnumerable<ImportedTaskRow> rows)
    {
        var toDoColumn = Columns.FirstOrDefault(c => c.Name == "To Do") ?? Columns.First();
        var created = new List<CardViewModel>();
        var importing = rows.Where(r => !string.IsNullOrWhiteSpace(r.Title)).ToList();

        // One Undo step for the whole import. It takes the tasks away again; any project, goal,
        // person or flag the import added to the lists stays.
        using var undo = RecordUndo($"Import {importing.Count} task{(importing.Count == 1 ? "" : "s")}", []);

        foreach (var row in importing)
        {
            if (string.IsNullOrWhiteSpace(row.Title)) continue;

            var column = toDoColumn;
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

            var cardVm = AddCard(row.Title.Trim(), column, project, priority, row.DueDate, people.FirstOrDefault(),
                pattern is not null, pattern, goal, flags, subTasks,
                notes: string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(), isImported: true,
                websiteUrl: SafeWebsite(row.WebsiteUrl), dueTime: row.DueDate is null ? null : row.DueTime,
                startDate: StartNoLaterThanDue(row.StartDate, row.DueDate),
                waitingOn: row.WaitingOn, people: people,
                recurrencesLeft: pattern is not null && row.RecurrenceCount is > 0 ? row.RecurrenceCount : null);
            created.Add(cardVm);
        }

        return created;
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
