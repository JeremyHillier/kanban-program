namespace KanbanApp.ViewModels;

// Which view the Add / Edit Task window opens in: the full form, or the short one with project, goal, task, due
// date, recurring and who. The window's own button switches that one window; this is where it starts.
public partial class MainViewModel
{
    public bool SimplifiedTaskWindow => _db.GetFlag("SimplifiedTaskWindow", false);

    public void SetSimplifiedTaskWindow(bool simplified) => _db.SetFlag("SimplifiedTaskWindow", simplified);
}
