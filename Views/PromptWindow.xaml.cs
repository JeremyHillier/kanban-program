using System.Windows;
using KanbanApp.Services;

namespace KanbanApp.Views;

public partial class PromptWindow : Window
{
    public string Value { get; private set; } = string.Empty;

    public PromptWindow(string title, string label)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = label;
        ValueTextBox.Focus();
    }

    // The box starts empty, so anything typed into it is the unsaved work.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || DialogResult == true || string.IsNullOrWhiteSpace(ValueTextBox.Text)) return;

        if (!UnsavedChangesGuard.ConfirmDiscard(this))
        {
            e.Cancel = true;
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var value = ValueTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;

        Value = value;
        DialogResult = true;
        Close();
    }
}
