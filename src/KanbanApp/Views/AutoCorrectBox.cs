using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;

namespace KanbanApp.Views;

// Makes a text box auto-correct as the user types (see AutoCorrector). Ctrl+Z straight after a
// correction puts back exactly what was typed, and nothing else is touched.
//
// It watches the text rather than the keys: any insertion that ends in a space or punctuation mark
// (typing, the touch keyboard, a suggestion picked, dictation, a paste) corrects the word before it.
// Watching single typed characters missed text that arrives a word at a time.
public static class AutoCorrectBox
{
    // Returns what to call to fix a slip that ends the box (Save does, since Ctrl+S moves no focus).
    // requireFocus: only a box the user is typing in is corrected, so a task's title being loaded into it
    // (or a template applied) is never changed behind their back; the tests turn it off.
    public static Action Attach(TextBox box, Func<bool> isOn, Func<IReadOnlyDictionary<string, string>> ownEntries, bool requireFocus = true)
    {
        (int Start, string Typed, string Fixed)? last = null;
        var correcting = false;

        box.TextChanged += (_, e) =>
        {
            if (correcting) return; // our own correction
            last = null;

            // Only text put in: one insertion that replaces nothing. Loading a task's title, or typing
            // over a selection, is not typing a word.
            if (!isOn() || e.Changes.Count != 1 || requireFocus && !box.IsKeyboardFocused) return;
            var change = e.Changes.Single();
            if (change.AddedLength == 0 || change.RemovedLength != 0) return;

            var delimiterAt = change.Offset + change.AddedLength - 1;
            if (delimiterAt <= 0 || delimiterAt >= box.Text.Length || !AutoCorrector.IsDelimiter(box.Text[delimiterAt])) return;
            if (!AutoCorrector.TryCorrect(box.Text, delimiterAt, ownEntries(), out var start, out var length, out var replacement)) return;
            if (box.MaxLength > 0 && box.Text.Length - length + replacement.Length > box.MaxLength) return;

            var typed = box.Text.Substring(start, length);
            var after = change.Offset + change.AddedLength; // just past what was put in, where the caret belongs
            correcting = true;
            try
            {
                box.Select(start, length);
                box.SelectedText = replacement;
                box.CaretIndex = after + replacement.Length - length; // still after what was typed
            }
            finally
            {
                correcting = false;
            }
            last = (start, typed, replacement);
        };

        // A slip that ends the box has no space typed after it, so it is fixed when the user moves on or saves.
        void FixTheEnd()
        {
            last = null;
            if (!isOn() || !AutoCorrector.TryCorrect(box.Text, box.Text.Length, ownEntries(), out var start, out var length, out var replacement)) return;
            if (box.MaxLength > 0 && box.Text.Length - length + replacement.Length > box.MaxLength) return;

            correcting = true;
            try
            {
                box.Select(start, length);
                box.SelectedText = replacement;
            }
            finally
            {
                correcting = false;
            }
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
                    correcting = true;
                    try
                    {
                        box.Select(done.Start, done.Fixed.Length);
                        box.SelectedText = done.Typed;
                        box.CaretIndex = caret;
                    }
                    finally
                    {
                        correcting = false;
                    }
                    e.Handled = true;
                }
                return;
            }
            last = null;
        };

        return FixTheEnd;
    }
}
