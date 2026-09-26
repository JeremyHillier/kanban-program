using System.Windows;
using KanbanApp.Views;

namespace KanbanApp.Services;

// Shows messages in the app's own message window. Every message and question in the app goes
// through here, so they all look and read the same way, in both themes.
public static class Dialogs
{
    // Shows the message over the owner (or in the middle of the screen when there is none yet) and
    // waits for a button.
    public static DialogChoice Show(Window? owner, DialogMessage message)
    {
        var window = new MessageWindow(message);
        if (owner is { IsVisible: true }) window.Owner = owner;
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // The app closes when its last window closes. A message shown before the board is up (the
        // newer-task-file question, "already running") would otherwise end the app as it closed.
        var app = Application.Current;
        var shutdownMode = app?.ShutdownMode;
        if (app is not null && app.ShutdownMode != ShutdownMode.OnExplicitShutdown) app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            window.ShowDialog();
        }
        finally
        {
            if (app is not null && shutdownMode is { } mode) app.ShutdownMode = mode;
        }
        return window.Choice;
    }

    // Whether the main button was chosen.
    public static bool Confirm(Window? owner, DialogMessage message) => Show(owner, message) == DialogChoice.Yes;

    // A message with only an OK button.
    public static void Tell(Window? owner, string title, string text, DialogTone tone = DialogTone.Info, string? detail = null) =>
        Show(owner, new DialogMessage(title, text) { Tone = tone, Detail = detail });
}
