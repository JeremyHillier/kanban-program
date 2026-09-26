namespace KanbanApp.Services;

// What a message is about, which sets its mark: information, a question, a warning or an error.
public enum DialogTone
{
    Info,
    Question,
    Warning,
    Error,
}

// Which button was chosen. Closing the window (Esc, or its X) counts as the safest one there is:
// Cancel, else the second button, else the only one.
public enum DialogChoice
{
    Yes,
    No,
    Cancel,
}

// One message in the app's message window (Views/MessageWindow). The text is laid out the same way
// everywhere:
//   - the first paragraph is the point of the message, in bold: what happened, or the question;
//   - paragraphs are separated by a blank line;
//   - a line starting "• " is a bullet, its wrapped lines kept clear of the bullet;
//   - Detail (a file path, or an error from Windows) goes in a box of its own, where it can be
//     selected and copied.
// Buttons are named for what they do ("Delete", "Keep Them"), never Yes and No.
public sealed record DialogMessage(string Title, string Text)
{
    public DialogTone Tone { get; init; } = DialogTone.Info;

    // A path or an error message, shown apart from the text.
    public string? Detail { get; init; }

    // The main button: it does what the message asks about.
    public string Yes { get; init; } = "OK";

    // A second button, or none when the message only tells.
    public string? No { get; init; }

    // A third button, for the rare question with two ways forward and a way out.
    public string? Cancel { get; init; }

    // The main button deletes, overwrites or throws work away: it is red, and Enter chooses the safest
    // button instead, so a quick Enter never does the damage...
    public bool IsDanger { get; init; }

    // ...unless the damage is undoable and the question is one people answer many times a day (deleting
    // a task, which Undo brings back): then Enter still means the main button.
    public bool EnterChoosesMain { get; init; }

    // A question: the main button, and Cancel.
    public static DialogMessage Ask(string title, string text, string yes, string no = "Cancel") =>
        new(title, text) { Tone = DialogTone.Question, Yes = yes, No = no };

    // A question whose main button deletes, replaces or throws something away.
    public static DialogMessage AskDanger(string title, string text, string yes, string no = "Cancel") =>
        new(title, text) { Tone = DialogTone.Warning, Yes = yes, No = no, IsDanger = true };

    // What the message says, as plain text: for the clipboard, and for tests.
    public override string ToString() => Detail is { Length: > 0 } detail ? $"{Text}\n\n{detail}" : Text;
}
