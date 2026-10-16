using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// A task's link (stored in Cards.WebsiteUrl, shown as "Link"). Older copies of the app only accept a
// web or email address there and refuse to save a task whose link is a file or folder, so the task
// file is stamped with the format that knows them the first time such a link is saved.
public partial class MainViewModel
{
    private void StampPathLink(string? link)
    {
        if (UrlLauncher.IsPathLink(link)) _db.RaiseFileFormat(DatabaseService.PathLinkFileFormat);
    }
}
