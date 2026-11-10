namespace KanbanApp.Models;

// One line of a task's history (a row of CardHistory): what happened, when, and on which PC (null for rows
// written before the PC was recorded).
public sealed record CardHistoryRecord(DateTime When, string Event, string Details, string? Computer);
