namespace KanbanApp.Services;

// Single source for the ownership strings shown around the app (splash, sidebar, About dialog, and
// the footer stamped into every other dialog by Theming.DialogCopyright). Previously copy-pasted
// into each of those places, which is exactly the kind of thing that drifts once one gets edited.
public static class AppInfo
{
    public const string Company = "Jeremy Hillier Consulting Inc";
    public const string Copyright = $"© {Company}";

    // Where Report a Problem sends its email. Customers see this address.
    public const string SupportEmail = "jeremy.hillier@gmail.com";
}
