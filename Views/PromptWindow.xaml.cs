using System.Windows;
using KanbanApp.Services;

namespace KanbanApp.Views;

public partial class PromptWindow : Window
{
    private readonly string _initialValue;

    public string Value { get; private set; } = string.Empty;

    // initialValue / okText are for changing an existing value (the card's Waiting On) rather than
    // adding a new one: the box starts filled in and selected, and the button says Save.
    // suggestions are past answers to offer while typing (see TextBoxSuggestions); forgetSuggestion
    // lets Shift+Delete take one off that list for good. manageSuggestions puts a "Manage list..."
    // link on the prompt: it opens whatever manages the list (owned by this window) and returns
    // the answers as they stand afterwards.
    public PromptWindow(string title, string label, string? initialValue = null, string okText = "Add",
        IEnumerable<string>? suggestions = null, Action<string>? forgetSuggestion = null,
        Func<Window, IEnumerable<string>>? manageSuggestions = null)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = label;
        OkButton.Content = okText;
        _initialValue = initialValue ?? string.Empty;
        ValueTextBox.Text = _initialValue;
        ValueTextBox.SelectAll();
        ValueTextBox.Focus();

        _forgetSuggestion = forgetSuggestion;
        _manageSuggestions = manageSuggestions;
        ManageLink.Visibility = manageSuggestions is null ? Visibility.Collapsed : Visibility.Visible;
        if (suggestions is not null) AttachSuggestions(suggestions);
    }

    private readonly Action<string>? _forgetSuggestion;
    private readonly Func<Window, IEnumerable<string>>? _manageSuggestions;
    private TextBoxSuggestions? _suggestions;

    private void AttachSuggestions(IEnumerable<string> suggestions)
    {
        if (_suggestions is not null)
        {
            _suggestions.Replace(suggestions);
            return;
        }

        // Shown inside the prompt, under the box: as a drop-down it would open over Cancel and Add.
        _suggestions = TextBoxSuggestions.Attach(ValueTextBox, suggestions, _forgetSuggestion, openWhenEmpty: true, inlineHost: SuggestionsHost);
        if (_suggestions is null) return;

        SuggestionsHost.Visibility = Visibility.Visible;
        ValueTextBox.ToolTip = _forgetSuggestion is null
            ? "Start typing, or press the Down arrow, to pick an earlier answer."
            : "Start typing, or press the Down arrow, to pick an earlier answer. Shift+Delete on a highlighted one forgets it.";
    }

    private void ManageLink_Click(object sender, RoutedEventArgs e)
    {
        if (_manageSuggestions is null) return;

        AttachSuggestions(_manageSuggestions(this).ToList());
        ValueTextBox.Focus();
    }

    // Anything typed that differs from how the box started is the unsaved work.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || DialogResult == true || ValueTextBox.Text.Trim() == _initialValue.Trim()) return;

        if (!UnsavedChangesGuard.ConfirmDiscard(this))
        {
            e.Cancel = true;
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var value = ValueTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value) && _initialValue.Length == 0) return; // emptying an existing value is a real answer: clear it

        Value = value;
        DialogResult = true;
        Close();
    }
}
