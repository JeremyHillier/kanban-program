using System.IO;

namespace KanbanApp.ViewModels;

// Keeps attachment files on disk in sync with a card's status: moving them between the
// Attachments folder's Done/Archived/Deleted subfolders, and cleaning up files that are no
// longer referenced by any card.
public partial class MainViewModel
{
    /// <summary>
    /// Moves a card's attachment files (screenshots and linked files alike) into the Attachments
    /// folder's Done/Archived/Deleted subfolder to match its current status, or back to the
    /// Attachments folder root when it's none of those. Skips a file still referenced by another
    /// card (so that card's link never breaks) and any file that's gone missing on disk.
    /// </summary>
    private void ReconcileAttachmentLocations(CardViewModel card, string? statusSubfolder)
    {
        if (card.Attachments.Count == 0) return;

        var destinationDir = string.IsNullOrEmpty(statusSubfolder) ? AttachmentsDir : Path.Combine(AttachmentsDir, statusSubfolder);
        var updated = new List<(string FilePath, string DisplayName, DateTime AddedDate)>();
        var changed = false;

        foreach (var attachment in card.Attachments)
        {
            var currentPath = attachment.FilePath;

            if (!File.Exists(currentPath) || _db.IsAttachmentPathReferencedElsewhere(currentPath, card.Id))
            {
                updated.Add((attachment.FilePath, attachment.DisplayName, attachment.AddedDate));
                continue;
            }

            try
            {
                var newPath = MoveAttachmentFile(currentPath, destinationDir);
                if (!string.Equals(newPath, currentPath, StringComparison.OrdinalIgnoreCase)) changed = true;
                updated.Add((newPath, attachment.DisplayName, attachment.AddedDate));
            }
            catch
            {
                // Best-effort: if the move fails (e.g. file in use), keep the existing reference rather than losing it.
                updated.Add((attachment.FilePath, attachment.DisplayName, attachment.AddedDate));
            }
        }

        if (!changed) return;

        var attachmentItems = _db.SetCardAttachments(card.Id, updated);
        card.Attachments = attachmentItems.Select(a => new AttachmentViewModel(a)).ToList();
    }

    private static string MoveAttachmentFile(string currentPath, string destinationDir)
    {
        var destPath = Path.Combine(destinationDir, Path.GetFileName(currentPath));
        if (string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
        {
            return currentPath;
        }

        Directory.CreateDirectory(destinationDir);

        if (File.Exists(destPath))
        {
            var nameOnly = Path.GetFileNameWithoutExtension(destPath);
            var ext = Path.GetExtension(destPath);
            var counter = 1;
            do
            {
                destPath = Path.Combine(destinationDir, $"{nameOnly}_{counter}{ext}");
                counter++;
            } while (File.Exists(destPath));
        }

        File.Move(currentPath, destPath);
        return destPath;
    }

    private void DeleteOrphanedAttachmentFiles(int cardId, List<AttachmentViewModel> previousAttachments, List<AttachmentViewModel> newAttachments)
    {
        var attachmentsDir = Path.GetFullPath(AttachmentsDir);
        var removed = previousAttachments.Where(old => !newAttachments.Any(a => a.Id != 0 && a.Id == old.Id));

        foreach (var attachment in removed)
        {
            try
            {
                var fullPath = Path.GetFullPath(attachment.FilePath);
                if (!fullPath.StartsWith(attachmentsDir, StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(fullPath)) continue;
                if (_db.IsAttachmentPathReferencedElsewhere(fullPath, cardId)) continue;

                File.Delete(fullPath);
            }
            catch
            {
                // Best-effort cleanup; leave the file if it can't be removed.
            }
        }
    }
}
