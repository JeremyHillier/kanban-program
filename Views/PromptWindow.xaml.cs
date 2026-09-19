using System.Windows;
using KanbanApp.Services;

namespace KanbanApp.Views;

public partial class PromptWindow : Window
{
    private readonly string _initialValue;

    public string Value { get; private set; } = string.Empty;

    // initialValue / okText are for changing an existing value (the card's Waiting On) rather than
    // adding a new one: the box starts filled in and selected, and the button says Save.
    public PromptWindow(string title, string label, string? initialValue = null, string okText = "Add")
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = label;
        OkButton.Content = okText;
        _initialValue = initialValue ?? string.Empty;
        ValueTextBox.Text = _initialValue;
        ValueTextBox.SelectAll();
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
