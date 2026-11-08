using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Copies tasks into another task file that is not open. Projects, goals, people and flags are matched by
// name there (and added when missing); the column is matched by its own name or, for one the user added, by its
// label there, else the first one. Attachments,
// links to other tasks and rich-text formatting of the notes are not copied.
public static class TaskCopyService
{
    public static int Copy(IEnumerable<(CardViewModel Card, string ColumnName)> cards, string targetPath)
    {
        var db = new DatabaseService(targetPath);
        var projects = db.GetProjects();
        var goals = db.GetGoals();
        var people = db.GetPeople();
        var flags = db.GetFlags();
        var columns = db.GetColumns();

        var count = 0;
        foreach (var (card, columnName) in cards)
        {
            int? projectId = string.IsNullOrWhiteSpace(card.ProjectName) || card.ProjectName == "No Project" ? null
                : (projects.FirstOrDefault(p => string.Equals(p.Name, card.ProjectName, StringComparison.CurrentCultureIgnoreCase)) ?? Add(projects, db.AddProject(card.ProjectName))).Id;
            int? goalId = string.IsNullOrWhiteSpace(card.GoalName) || card.GoalName == "No Goal" ? null
                : (goals.FirstOrDefault(g => string.Equals(g.Name, card.GoalName, StringComparison.CurrentCultureIgnoreCase)) ?? Add(goals, db.AddGoal(card.GoalName))).Id;

            var personIds = card.People.Select(p => (people.FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.CurrentCultureIgnoreCase)) ?? Add(people, db.AddPerson(p.Name))).Id).ToList();
            var flagIds = card.Flags.Select(f => (flags.FirstOrDefault(x => string.Equals(x.Name, f.Name, StringComparison.CurrentCultureIgnoreCase)) ?? Add(flags, db.AddFlag(f.Name))).Id).ToList();
            var column = columns.FirstOrDefault(c => c.Name == columnName)
                ?? columns.FirstOrDefault(c => c.IsCustom && string.Equals(c.DisplayName, columnName, StringComparison.CurrentCultureIgnoreCase))
                ?? columns[0];

            var added = db.AddCard(column.Id, card.Title, projectId, column.Name, card.Priority, card.DueDate, personIds.Count > 0 ? personIds[0] : null,
                card.IsRecurring, card.RecurrencePattern, goalId, card.Notes, false, card.ForceEditOnComplete, card.WebsiteUrl, card.DueTime, card.StartDate,
                card.WaitingOn, card.RecurrencesLeft);
            db.SetCardPeople(added.Id, personIds);
            db.SetCardFlags(added.Id, flagIds);
            db.SetCardSubTasks(added.Id, card.SubTasks.Select(s => (s.Title, s.IsDone)).ToList());
            if (card.Color is not null) db.SetCardColor(added.Id, card.Color);
            count++;
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        return count;
    }

    private static T Add<T>(List<T> list, T item)
    {
        list.Add(item);
        return item;
    }
}
