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

            var priority = row.Priority?.Trim() switch
            {
                { } p when string.Equals(p, "High", StringComparison.OrdinalIgnoreCase) => "High",
                { } p when string.Equals(p, "Medium", StringComparison.OrdinalIgnoreCase) => "Medium",
                { } p when string.Equals(p, "Low", StringComparison.OrdinalIgnoreCase) => "Low",
                _ => "Normal"
            };

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

            var cardVm = AddCard(row.Title.Trim(), column, project, priority, row.DueDate, people.FirstOrDefault(),
                false, null, goal, isImported: true, startDate: StartNoLaterThanDue(row.StartDate, row.DueDate),
                waitingOn: row.WaitingOn, people: people);
            created.Add(cardVm);
        }

        return created;
    }

    // "Sam Lee; Priya Patel" -> the names, trimmed, blanks and repeats dropped. A name with a comma
    // in it ("Lee, Sam") stays whole, which is why the separator is a semicolon.
    internal static List<string> SplitPeopleNames(string? cell) =>
        (cell ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // A spreadsheet can say anything. A start date after the due date makes no sense on the board
    // (the task dialog refuses it), so it is pulled back to the due date rather than rejected.
    private static DateTime? StartNoLaterThanDue(DateTime? startDate, DateTime? dueDate) =>
        startDate is not null && dueDate is not null && startDate.Value.Date > dueDate.Value.Date ? dueDate.Value.Date : startDate?.Date;
}
