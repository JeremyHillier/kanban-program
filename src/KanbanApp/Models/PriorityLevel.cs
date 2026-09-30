namespace KanbanApp.Models;

// One entry in the task file's priority list: its name (what a task stores) and the key of its
// colour in PriorityColors.
public sealed record PriorityLevel(string Name, string Color);
