using System.Windows.Controls.Primitives;

namespace KanbanApp.Views;

// The grips under the Task and Notes boxes: drag one to make its box taller or shorter. The heights are
// remembered in the task file (TaskBoxHeights), so the window opens the same way next time.
public partial class AddTaskWindow
{
    private const double SmallestBoxHeight = 40;
    private const double TallestBoxHeight = 600;

    private void TaskGrip_DragDelta(object sender, DragDeltaEventArgs e) =>
        DetailsTextBox.Height = Math.Clamp(DetailsTextBox.Height + e.VerticalChange, SmallestBoxHeight, TallestBoxHeight);

    private void NotesGrip_DragDelta(object sender, DragDeltaEventArgs e) =>
        NotesEditor.BoxHeight = Math.Clamp(NotesEditor.BoxHeight + e.VerticalChange, SmallestBoxHeight, TallestBoxHeight);

    private void Grip_DragCompleted(object sender, DragCompletedEventArgs e) =>
        _viewModel.SaveTaskBoxHeights(DetailsTextBox.Height, NotesEditor.BoxHeight);

    private void RestoreBoxHeights()
    {
        if (_viewModel.TaskBoxHeights is not { } heights) return;
        DetailsTextBox.Height = Math.Clamp(heights.Task, SmallestBoxHeight, TallestBoxHeight);
        NotesEditor.BoxHeight = Math.Clamp(heights.Notes, SmallestBoxHeight, TallestBoxHeight);
    }
}
