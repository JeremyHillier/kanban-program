using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Rename or delete task templates. Making one happens where the task is (the task screen's Save as
// Template, or a card's right-click menu), so there is no Add here.
public partial class ManageTemplatesWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ManageTemplatesWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        TemplateList.ItemsSource = viewModel.TaskTemplates;
    }

    private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenameButton.IsEnabled = DeleteButton.IsEnabled = TemplateList.SelectedItem is TaskTemplate;
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateList.SelectedItem is not TaskTemplate template) return;

        var dialog = new PromptWindow("Rename Template", "Template name", template.Name, "Save") { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value) || dialog.Value == template.Name) return;

        if (!_viewModel.RenameTaskTemplate(template, dialog.Value))
        {
            Dialogs.Tell(this, "Rename Template", $"The template was not renamed.\n\nThere is already a template called \"{dialog.Value}\".", DialogTone.Warning);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateList.SelectedItem is not TaskTemplate template) return;

        if (Dialogs.Confirm(this, DialogMessage.AskDanger("Delete Template",
                $"Delete the template \"{template.Name}\"?\n\nTasks already made from it are not affected.", "Delete")))
            _viewModel.DeleteTaskTemplate(template);
    }
}
