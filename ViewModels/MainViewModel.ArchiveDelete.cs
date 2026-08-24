using System.IO;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Archiving Done tasks, listing/reactivating archived or deleted tasks, and permanently erasing
// a task (only ever called from the Archived/Deleted list views, where the card is already off
// the live board).
public partial class MainViewModel
{
    public void ArchiveDoneTasks()
    {
        var doneColumn = Columns.FirstOrDefault(c => c.Name == "Done");
        if (doneColumn is null) return;

        foreach (var card in doneColumn.Cards.ToList())
        {
            ReconcileAttachmentLocations(card, "Archived");
            _db.ArchiveCard(card.Id, card.Title, doneColumn.Name);
            doneColumn.Cards.Remove(card);
        }

        RefreshDashboardStats();
    }

    public List<ArchivedCardInfo> GetArchivedCards() => _db.GetArchivedCards();

    public List<DeletedCardInfo> GetDeletedCards() => _db.GetDeletedCards();

    public void ReactivateCard(int cardId, string cardTitle)
    {
        _db.ReactivateCard(cardId, cardTitle);
        Load();
        RefreshDashboardStats();

        var reactivatedCard = Columns.SelectMany(c => c.Cards).FirstOrDefault(c => c.Id == cardId);
        if (reactivatedCard is not null) ReconcileAttachmentLocations(reactivatedCard, null);
    }

    // Only called from the Archived/Deleted list views - the card is already off the live board,
    // so there's no Columns/Cards collection to update here, unlike DeleteCard.
    public void PermanentlyDeleteCard(int cardId, string cardTitle, string sourceListName)
    {
        var attachmentsDir = Path.GetFullPath(AttachmentsDir);
        foreach (var filePath in _db.GetCardAttachmentPaths(cardId))
        {
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                if (!fullPath.StartsWith(attachmentsDir, StringComparison.OrdinalIgnoreCase)) continue;
                if (_db.IsAttachmentPathReferencedElsewhere(fullPath, cardId)) continue;
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch
            {
                // Best-effort cleanup; leave the file if it can't be removed.
            }
        }

        _db.PermanentlyDeleteCard(cardId, cardTitle, sourceListName);
    }
}
