using System.Collections.ObjectModel;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Task templates: saved starting points for new tasks (see TaskTemplate). Kept in name order.
// A template only refers to projects, people, goals and flags by id, so one that has been deleted
// since simply doesn't get filled in when the template is used.
public partial class MainViewModel
{
    public ObservableCollection<TaskTemplate> TaskTemplates { get; } = [];

    private void LoadTaskTemplates()
    {
        TaskTemplates.Clear();
        foreach (var template in _db.GetTaskTemplates()) TaskTemplates.Add(template);
    }

    public TaskTemplate? FindTaskTemplate(string name) =>
        TaskTemplates.FirstOrDefault(t => string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    public static TaskTemplate TemplateFromCard(CardViewModel card) => new()
    {
        Title = card.Title,
        ProjectId = card.ProjectId,
        Priority = card.Priority,
        WhoId = card.WhoId,
        PeopleIds = card.People.Select(p => p.Id).ToList(),
        GoalId = card.GoalId,
        FlagIds = card.Flags.Select(f => f.Id).ToList(),
        SubTasks = card.SubTasks.Select(s => s.Title).ToList(),
        Notes = card.Notes,
        IsRecurring = card.IsRecurring,
        RecurrencePattern = card.IsRecurring ? card.RecurrencePattern : null,
        RecurrenceCount = card.IsRecurring ? card.RecurrencesLeft : null,
        ForceEditOnComplete = card.ForceEditOnComplete,
        WebsiteUrl = card.WebsiteUrl,
        DueTime = card.DueDate is null ? null : card.DueTime,
        DueInDays = TaskTemplate.DaysFromToday(card.DueDate),
        StartInDays = TaskTemplate.DaysFromToday(card.StartDate)
    };

    // Saves under the given name, replacing a template that already has it (any capitalisation).
    // Returns the saved template.
    public TaskTemplate SaveTaskTemplate(string name, TaskTemplate template)
    {
        template.Name = name.Trim();
        template.Id = FindTaskTemplate(template.Name)?.Id ?? 0;
        _db.SaveTaskTemplate(template);
        LoadTaskTemplates();
        return TaskTemplates.First(t => t.Id == template.Id);
    }

    // False when another template already has that name.
    public bool RenameTaskTemplate(TaskTemplate template, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0) return false;
        if (FindTaskTemplate(newName) is { } existing && existing.Id != template.Id) return false;

        template.Name = newName;
        _db.SaveTaskTemplate(template);
        LoadTaskTemplates();
        return true;
    }

    public void DeleteTaskTemplate(TaskTemplate template)
    {
        _db.DeleteTaskTemplate(template.Id);
        LoadTaskTemplates();
    }
}
