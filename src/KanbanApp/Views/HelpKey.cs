using System.Windows;
using System.Windows.Input;

namespace KanbanApp.Views;

// F1 opens Help, as it does across Windows. Every window with a Help button binds it here, so
// the key works the same way in each of them.
public static class HelpKey
{
    public static void Bind(Window window, Action openHelp)
    {
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.F1 || Keyboard.Modifiers != ModifierKeys.None) return;
            openHelp();
            e.Handled = true;
        };
    }
}
