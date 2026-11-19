using KanbanApp.Models;

namespace KanbanApp.ViewModels;

// Tasks entered with Quick Add (Ctrl+Alt+N) are marked for review: the card shows a Review tag, the button column a
// Review (N) button, and the Quick Entries list and the task window's review strip step through them. The mark goes
// only when the user says so (Mark Reviewed); saving, moving or finishing a task leaves it.
public partial class MainViewModel
{
    // Board tasks still to review, in board order (column by column, top to bottom).
    public List<CardViewModel> QuickEntriesToReview() =>
        Columns.SelectMany(c => c.Cards).Where(c => c.NeedsReview).ToList();

    public int QuickReviewCount => Columns.SelectMany(c => c.Cards).Count(c => c.NeedsReview);

    public bool HasQuickEntriesToReview => QuickReviewCount > 0;

    public string QuickReviewButtonText => $"Review ({QuickReviewCount})";

    public void NotifyQuickReviewChanged()
    {
        OnPropertyChanged(nameof(QuickReviewCount));
        OnPropertyChanged(nameof(HasQuickEntriesToReview));
        OnPropertyChanged(nameof(QuickReviewButtonText));
    }

    public void MarkReviewed(IReadOnlyList<CardViewModel> cards)
    {
        var marked = cards.Where(c => c.NeedsReview).ToList();
        if (marked.Count == 0) return;

        using (RecordUndo(marked.Count == 1 ? DescribeAction("Mark reviewed", marked) : $"Mark {marked.Count} quick entries reviewed", marked))
        {
            foreach (var card in marked)
            {
                card.NeedsReview = false;
                _db.SetCardNeedsReview(card.Id, false);
            }
        }
        NotifyQuickReviewChanged();
    }

    public void MarkReviewed(CardViewModel card) => MarkReviewed([card]);
}
