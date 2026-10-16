namespace KanbanApp.Models;

// What this board knows about a task shared by email, kept by its Task ID (CardItem.ShareId).
// SenderName/SenderEmail: who sent it here, so it can be sent back; empty for a task that began on this board.
// Fingerprint: what the task looked like the last time it went out or came in (see ShareSync), so a
// returned copy can be told apart from a change made on both sides.
public sealed record SharedTaskInfo(string ShareId, string? SenderName, string? SenderEmail, string? Fingerprint)
{
    public bool HasSender => !string.IsNullOrWhiteSpace(SenderName) || !string.IsNullOrWhiteSpace(SenderEmail);

    // "Sam Lee", or the address when no name came with it.
    public string SenderLabel => !string.IsNullOrWhiteSpace(SenderName) ? SenderName!.Trim() : SenderEmail?.Trim() ?? string.Empty;
}
