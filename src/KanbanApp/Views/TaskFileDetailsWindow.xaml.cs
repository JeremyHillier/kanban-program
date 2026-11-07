using System.Windows;
using KanbanApp.Services;

namespace KanbanApp.Views;

// A task file's name and description, as the Task Files list shows them (Edit Name); or, for a
// template, the same plus which parts of the file go into it.
public partial class TaskFileDetailsWindow : Window
{
    public string Name => NameBox.Text.Trim();
    public string? Description => string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();

    public TemplateParts Parts => new(
        Projects: ProjectsBox.IsChecked == true, Goals: GoalsBox.IsChecked == true, People: PeopleBox.IsChecked == true,
        Flags: FlagsBox.IsChecked == true, OpenTasks: TasksBox.IsChecked == true, TaskTemplates: TaskTemplatesBox.IsChecked == true);

    public TaskFileDetailsWindow(string title, string intro, string? name, string? description, string okText = "Save", TemplateParts? parts = null)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        Title = title;
        IntroText.Text = intro;
        NameBox.Text = name ?? string.Empty;
        DescriptionBox.Text = description ?? string.Empty;
        OkButton.Content = okText;
        if (parts is not null)
        {
            PartsPanel.Visibility = Visibility.Visible;
            ProjectsBox.IsChecked = parts.Projects;
            GoalsBox.IsChecked = parts.Goals;
            PeopleBox.IsChecked = parts.People;
            FlagsBox.IsChecked = parts.Flags;
            TasksBox.IsChecked = parts.OpenTasks;
            TaskTemplatesBox.IsChecked = parts.TaskTemplates;
        }
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            Dialogs.Tell(this, Title, "Give it a name first.");
            NameBox.Focus();
            return;
        }
        DialogResult = true;
    }
}
