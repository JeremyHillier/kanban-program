using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;

namespace KanbanApp.Views;

// The Templates tab: task-file templates in the templates folder (TaskFileTemplates).
public partial class TaskFilesWindow
{
    private string TemplatesFolder => _config.TemplatesFolderOrDefault;

    private void RefreshTemplates(string? select = null)
    {
        var templates = TaskFileTemplates.List(TemplatesFolder);
        TemplateList.ItemsSource = templates;
        TemplateList.SelectedItem = select is null ? null : templates.FirstOrDefault(t => AppConfig.ArePathsEqual(t.Path, select));
        TemplatesFolderText.Text = TemplatesFolder;
        TemplateList_SelectionChanged(this, null!);
    }

    private TaskFileTemplate? SelectedTemplate => TemplateList.SelectedItem as TaskFileTemplate;

    private void TemplateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var any = SelectedTemplate is not null;
        CreateFromTemplateButton.IsEnabled = EditTemplateButton.IsEnabled = UpdateTemplateButton.IsEnabled = DuplicateTemplateButton.IsEnabled = DeleteTemplateButton.IsEnabled = any;
    }

    private void TemplateList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CreateFromTemplateButton.IsEnabled) CreateFromTemplate_Click(sender, e);
    }

    private void TemplateList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && DeleteTemplateButton.IsEnabled) DeleteTemplate_Click(sender, e);
        else if (e.Key == Key.F2 && EditTemplateButton.IsEnabled) EditTemplate_Click(sender, e);
        else return;
        e.Handled = true;
    }

    private void NewTemplate_Click(object sender, RoutedEventArgs e) => SaveTemplateFrom(OpenPath, _config.Current.DisplayName);

    // Asks what to call the template and which parts to keep, then makes it from the file. True when made.
    private bool SaveTemplateFrom(string sourcePath, string suggestedName)
    {
        var details = new TaskFileDetailsWindow("Save as Template", $"A template made from \"{suggestedName}\". New task files started from it begin with the parts ticked below.",
            suggestedName, null, "Save Template", TemplateParts.Default) { Owner = this };
        if (details.ShowDialog() != true) return false;

        var path = TaskFileTemplates.PathFor(TemplatesFolder, details.Name);
        if (File.Exists(path) && !Dialogs.Confirm(this, DialogMessage.AskDanger("Replace Template", $"There is already a template called \"{details.Name}\". Replace it?", "Replace"))) return false;

        try
        {
            TaskFileTemplates.Save(sourcePath, path, details.Name, details.Description, details.Parts);
            RefreshTemplates(path);
            return true;
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Save as Template", "The template was not made.", DialogTone.Error, ex.Message);
            return false;
        }
    }

    private void NewFromTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TaskFileTemplates.List(TemplatesFolder).Count == 0)
        {
            Dialogs.Tell(this, "No Templates Yet", "There are no templates yet. On the Templates tab, New Template from Open File makes one from the file that is open now.");
            Tabs.SelectedItem = TemplatesTab;
            return;
        }
        Tabs.SelectedItem = TemplatesTab;
        if (SelectedTemplate is not null) CreateFromTemplate_Click(sender, e);
        else Dialogs.Tell(this, "New from Template", "Pick a template in the list, then Create Task File.");
    }

    private void CreateFromTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTemplate is not { } template) return;
        var path = AskForNewFilePath($"New Task File from \"{template.Name}\"", template.Name);
        if (path is null) return;
        var details = new TaskFileDetailsWindow("New Task File", $"Made from the template \"{template.Name}\". A name for the list here, and a note about what the file is for.",
            Path.GetFileNameWithoutExtension(path), null, "Create") { Owner = this };
        if (details.ShowDialog() != true) return;

        try
        {
            TaskFileTemplates.CreateTaskFile(template.Path, path);
            Register(path, details.Name, details.Description);
            Tabs.SelectedItem = FilesTab;
            OfferToOpen(path, "The new task file is ready.");
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "New from Template", "The task file could not be made.", DialogTone.Error, ex.Message);
        }
    }

    private void EditTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTemplate is not { } template) return;
        var details = new TaskFileDetailsWindow("Edit Template", "The template's name and description.", template.Name, template.Description) { Owner = this };
        if (details.ShowDialog() != true) return;
        try
        {
            var path = template.Path;
            TaskFileTemplates.Rename(path, details.Name, details.Description);
            var wanted = TaskFileTemplates.PathFor(TemplatesFolder, details.Name);
            if (!AppConfig.ArePathsEqual(path, wanted) && !File.Exists(wanted))
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Move(path, wanted);
                path = wanted;
            }
            RefreshTemplates(path);
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Edit Template", "The template was not changed.", DialogTone.Error, ex.Message);
        }
    }

    private void UpdateTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTemplate is not { } template) return;
        if (!Dialogs.Confirm(this, DialogMessage.Ask("Update Template",
                $"Make \"{template.Name}\" again from \"{_config.Current.DisplayName}\", the file that is open now, with the same parts as before?\n\nWhat the template holds now is replaced.", "Update"))) return;
        try
        {
            TaskFileTemplates.Save(OpenPath, template.Path, template.Name, template.Description, TaskFileTemplates.PartsOf(template.Path));
            RefreshTemplates(template.Path);
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Update Template", "The template was not updated.", DialogTone.Error, ex.Message);
        }
    }

    private void DuplicateTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTemplate is not { } template) return;
        var details = new TaskFileDetailsWindow("Duplicate Template", "A copy of the template under a new name.", template.Name + " copy", template.Description, "Duplicate") { Owner = this };
        if (details.ShowDialog() != true) return;
        var path = TaskFileTemplates.PathFor(TemplatesFolder, details.Name);
        if (File.Exists(path))
        {
            Dialogs.Tell(this, "Duplicate Template", $"There is already a template called \"{details.Name}\".", DialogTone.Warning);
            return;
        }
        try
        {
            File.Copy(template.Path, path);
            TaskFileTemplates.Rename(path, details.Name, details.Description);
            RefreshTemplates(path);
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Duplicate Template", "The template was not copied.", DialogTone.Error, ex.Message);
        }
    }

    private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTemplate is not { } template) return;
        if (!Dialogs.Confirm(this, DialogMessage.AskDanger("Delete Template", $"Delete the template \"{template.Name}\"?\n\nIt goes to the Recycle Bin. Task files already made from it are not affected.", "Delete"))) return;
        try
        {
            TaskFileService.SendToRecycleBin(template.Path);
            RefreshTemplates();
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Delete Template", "The template was not deleted.", DialogTone.Error, ex.Message);
        }
    }

    private void ChangeTemplatesFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Templates Folder", InitialDirectory = Directory.Exists(TemplatesFolder) ? TemplatesFolder : null };
        if (dialog.ShowDialog(this) != true) return;
        _config.TemplatesFolder = AppConfig.ArePathsEqual(dialog.FolderName, AppConfig.DefaultTemplatesFolder) ? null : dialog.FolderName;
        _config.Save();
        RefreshTemplates();
    }

    private void OpenTemplatesFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(TemplatesFolder);
        Process.Start(new ProcessStartInfo(TemplatesFolder) { UseShellExecute = true });
    }
}
