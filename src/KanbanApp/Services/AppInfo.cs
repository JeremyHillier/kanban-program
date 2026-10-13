namespace KanbanApp.Services;

// Single source for the ownership strings shown around the app (splash, sidebar, About dialog, and
// the footer stamped into every other dialog by Theming.DialogCopyright). Previously copy-pasted
// into each of those places, which is exactly the kind of thing that drifts once one gets edited.
public static class AppInfo
{
    public const string ProductName = "Kanban Task Board";
    public const string Company = "Jeremy Hillier Consulting Inc";
    public const string Website = "https://hillierconsulting.ca";

    // Never a year behind: the year comes from the clock.
    public static string Copyright => $"© {DateTime.Today.Year} {Company}";

    // While the app is in beta, a small Beta pill sits beside the title in the header. Set this to false
    // when the beta ends, together with "#define Beta" in the installer script and the beta section of
    // the licence; LegalDocumentTests keeps the three in step.
    public const bool IsBeta = true;

    // Shown, with a maple leaf, in the header and on the About screen.
    public const string MadeIn = "Designed in Canada";

    // Support requests and error logs: Report a Problem and the crash prompt send here. Customers see it in
    // About, Help and the licence.
    public const string SupportEmail = "support@hillierconsulting.ca";

    // General questions and information requests, shown beside the support address.
    public const string InfoEmail = "info@hillierconsulting.ca";

    // Where people are sent to get a newer version. The update check never uses a link from the
    // internet reply itself - only this one.
    public const string DownloadPageUrl = "https://hillierconsulting.ca/kanban.html";
}
