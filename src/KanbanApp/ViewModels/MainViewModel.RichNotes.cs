using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// The formatting of a task's Notes (bold, italic, tables...), shown only in the task window. The plain Notes stay what
// everything else reads; this is the overlay beside them (see DatabaseService.RichNotes).
public partial class MainViewModel
{
    // The formatted notes for the task window, or null when there is none or it no longer matches the plain Notes.
    public List<RichBlock>? RichNotesOf(CardViewModel card) => RichNotes.Deserialize(_db.GetRichNotes(card.Id, card.Notes));

    // Keeps the formatting of what the window saved (its plain text is what went into Notes). Notes with nothing but
    // plain paragraphs keep no formatting at all.
    public void SaveRichNotes(CardViewModel card, IReadOnlyList<RichBlock>? blocks)
    {
        var keep = blocks is { Count: > 0 } && !RichNotes.IsPlain(blocks);
        _db.SetRichNotes(card.Id, card.Notes ?? string.Empty, keep ? RichNotes.Serialize(blocks!) : null);
    }
}
