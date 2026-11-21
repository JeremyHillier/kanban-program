namespace KanbanApp.Models;

// A task's sub-tasks are a checklist (tick any of them) or, with "Pick one", a set of choices: at most one is picked, stored as the one
// sub-task marked done. These are the rules every part of the app shares for the second kind.
public static class SubTaskRules
{
    // Keeps only the first picked one, for a list that arrived with several (an Excel file, or switching a checklist to pick one).
    public static List<(string Title, bool IsDone)> KeepFirstPicked(IEnumerable<(string Title, bool IsDone)> subTasks)
    {
        var picked = false;
        var result = new List<(string Title, bool IsDone)>();
        foreach (var (title, isDone) in subTasks)
        {
            result.Add((title, isDone && !picked));
            picked |= isDone;
        }
        return result;
    }

    // How a choice reads on a card and in a report: "Chosen: Option B", or "Chosen: none yet".
    public static string ChoiceText(IEnumerable<(string Title, bool IsDone)> subTasks) =>
        subTasks.Where(s => s.IsDone).Select(s => s.Title).FirstOrDefault() is { } title ? $"Chosen: {title}" : "Chosen: none yet";
}
