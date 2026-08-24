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

        foreach (var row in rows)
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

            PersonViewModel? who = null;
            if (!string.IsNullOrWhiteSpace(row.Who))
            {
                var name = row.Who.Trim();
                who = People.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (who is null)
                {
                    AddPerson(name);
                    who = People.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            var cardVm = AddCard(row.Title.Trim(), column, project, priority, row.DueDate, who,
                false, null, goal, isImported: true);
            created.Add(cardVm);
        }

        return created;
    }
}
