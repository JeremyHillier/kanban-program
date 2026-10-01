namespace KanbanApp.Models;

// One time a task was emailed: when, and the detail recorded ("to sam@example.com, in Outlook").
public sealed record CardEmailRecord(DateTime When, string Details);
