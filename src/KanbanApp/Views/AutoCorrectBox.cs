using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;

namespace KanbanApp.Views;

// Makes a text box auto-correct as the user types (see AutoCorrector). Ctrl+Z straight after a
// correction puts back exactly what was typed, and nothing else is touched.
public static class AutoCorrectBox
{
    // Returns what to call to fix a slip that ends the box (Save does, since Ctrl+S moves no focus).
    public static Action Attach(TextBox box, Func<bool> isOn, Func<IReadOnlyDictionary<string, string>> ownEntries)
    {
        (int Start, string Typed, string Fixed)? last = null;

        box.PreviewTextInput += (_, e) =>
        {
            last = null;
            if (e.Text.Length != 1 || !AutoCorrector.IsDelimiter(e.Text[0]) || box.SelectionLength > 0 || !isOn()) return;
            if (!AutoCorrector.TryCorrect(box.Text, box.CaretIndex, ownEntries(), out var start, out var length, out var replacement)) return;
            if (box.MaxLength > 0 && box.Text.Length - length + replacement.Length + 1 > box.MaxLength) return;

            var typed = box.Text.Substring(start, length);
            box.Select(start, length);
            box.SelectedText = replacement;
            box.CaretIndex = start + replacement.Length; // the typed space or punctuation goes in after it
            last = (start, typed, replacement);
        };

        // A slip that ends the box has no space typed after it, so it is fixed when the user moves on or saves.
        void FixTheEnd()
        {
            last = null;
            if (!isOn() || !AutoCorrector.TryCorrect(box.Text, box.Text.Length, ownEntries(), out var start, out var length, out var replacement)) return;
            if (box.MaxLength > 0 && box.Text.Length - length + replacement.Length > box.MaxLength) return;

            box.Select(start, length);
            box.SelectedText = replacement;
        }
        box.LostFocus += (_, _) => FixTheEnd();

        box.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt) return;

            if (last is { } done && key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                last = null;
                var end = done.Start + done.Fixed.Length;
                if (box.CaretIndex == end + 1 && box.Text.Length > end && string.CompareOrdinal(box.Text, done.Start, done.Fixed, 0, done.Fixed.Length) == 0)
                {
                    var caret = done.Start + done.Typed.Length + 1;
                    box.Select(done.Start, done.Fixed.Length);
                    box.SelectedText = done.Typed;
                    box.CaretIndex = caret;
                    e.Handled = true;
                }
                return;
            }
            last = null;
        };

        return FixTheEnd;
    }
}
