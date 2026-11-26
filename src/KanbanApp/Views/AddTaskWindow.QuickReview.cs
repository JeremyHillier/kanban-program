using System.Windows;
using System.Windows.Controls;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// What the user chose on the review strip. The window saves and closes like Save and Close; the board then marks the task
// reviewed (Reviewed) and opens the next one (MainWindow.QuickReview.cs).
public enum ReviewStep { None, Previous, Next, Reviewed, Jump }

// The review strip: shown when the board opens a Quick Add task from its review list. It says where this task is in the
// list and steps through it. Every step saves the form first; a form that cannot be saved (no title, a bad date) stays open.
public partial class AddTaskWindow
{
    public ReviewStep ReviewStep { get; private set; }

    // With ReviewStep.Jump: the task chosen in the strip's drop-down.
    public CardViewModel? ReviewJumpTo { get; private set; }

    private bool _fillingReviewList;

    public void ShowReviewStrip(IReadOnlyList<CardViewModel> queue, int index)
    {
        ReviewStrip.Visibility = Visibility.Visible;
        ReviewPositionText.Text = $"Quick entry to review: {index + 1} of {queue.Count}";
        _fillingReviewList = true;
        ReviewListComboBox.ItemsSource = queue;
        ReviewListComboBox.SelectedIndex = index;
        _fillingReviewList = false;
        ReviewPreviousButton.IsEnabled = index > 0;
        ReviewNextButton.IsEnabled = index < queue.Count - 1;
    }

    private void Step(ReviewStep step, CardViewModel? jumpTo = null)
    {
        ReviewStep = step;
        ReviewJumpTo = jumpTo;
        Add_Click(this, new RoutedEventArgs());
        if (DialogResult != true)
        {
            ReviewStep = ReviewStep.None; // the form was not complete, so nothing was saved
            ReviewJumpTo = null;
        }
    }

    private void Reviewed_Click(object sender, RoutedEventArgs e) => Step(ReviewStep.Reviewed);

    private void ReviewNext_Click(object sender, RoutedEventArgs e) => Step(ReviewStep.Next);

    private void ReviewPrevious_Click(object sender, RoutedEventArgs e) => Step(ReviewStep.Previous);

    private void ReviewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_fillingReviewList || ReviewListComboBox.SelectedItem is not CardViewModel target || target == _cardToEdit) return;
        Step(ReviewStep.Jump, target);
        if (ReviewStep != ReviewStep.Jump)
        {
            // Not saved: put the drop-down back on this task.
            _fillingReviewList = true;
            ReviewListComboBox.SelectedItem = _cardToEdit;
            _fillingReviewList = false;
        }
    }
}
