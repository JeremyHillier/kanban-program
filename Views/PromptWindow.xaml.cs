using System.Windows;

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

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var value = ValueTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;

        Value = value;
        DialogResult = true;
        Close();
    }
}
