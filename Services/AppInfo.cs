namespace KanbanApp.Services;

// Single source for the ownership strings shown around the app (splash, sidebar, About dialog, and
// the footer stamped into every other dialog by Theming.DialogCopyright). Previously copy-pasted
// into each of those places, which is exactly the kind of thing that drifts once one gets edited.
public static class AppInfo
{
    public const string Company = "Jeremy Hillier Consulting Inc";
    public const string Copyright = $"© {Company}";

    // Shown, with a maple leaf, in the header and on the About screen.
    public const string MadeIn = "Designed in Canada";

    // Where Report a Problem sends its email. Customers see this address.
    public const string SupportEmail = "jeremy.hillier@gmail.com";

    // Where people are sent to get a newer version. The update check never uses a link from the
    // internet reply itself - only this one.
    public const string DownloadPageUrl = "https://hillierconsulting.ca/kanban.html";
}
