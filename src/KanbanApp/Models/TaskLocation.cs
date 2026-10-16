namespace KanbanApp.Models;

// Where a task is, for a link to it: on the board, or put away (archived or deleted) and when.
public sealed record TaskLocation(int CardId, string Title, bool IsArchived, bool IsDeleted, DateTime? PutAwayAt)
{
    public bool IsOnBoard => !IsArchived && !IsDeleted;
}
