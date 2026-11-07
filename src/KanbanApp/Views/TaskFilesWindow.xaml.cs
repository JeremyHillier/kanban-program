using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The catalogue of task files on this PC (AppConfig.Files) and what can be done with them: switch,
// make, copy, add, name, move, reconnect, forget, delete. Switching is not done here: the window
// records the file in SwitchToPath and closes, and the board (which owns this window and must
// itself be replaced) does the switch once the window has gone.
public partial class TaskFilesWindow : Window
{
    private readonly MainViewModel _viewModel;
    private AppConfig _config = AppConfig.Load();

    // Set when the user chose a file to open; the caller switches to it after ShowDialog returns.
    public string? SwitchToPath { get; private set; }

    // True when a name or description changed, so the board's Task file button can show it.
    public bool CatalogueChanged { get; private set; }

    private sealed record FileRow(TaskFileEntry Entry, bool IsOpen, bool IsStartup)
    {
        public string Name => Entry.DisplayName;
        public string Path => Entry.Path;
        public string? Description => Entry.Description;
        public bool HasDescription => !string.IsNullOrWhiteSpace(Entry.Description);
        public bool IsMissing => !File.Exists(Entry.Path);
        public string LastOpenedText => Entry.LastOpened is { } when ? $"Last opened {when:MMM d, yyyy h:mm tt}" : "Never opened here";
    }

    public TaskFilesWindow(MainViewModel viewModel, bool showTemplates = false)
    {
        InitializeComponent();
        AccessKeys.Bind(this);
        _viewModel = viewModel;
        WindowPlacementMemory.Attach(this, viewModel, "TaskFiles");
        RefreshFiles();
        RefreshTemplates();
        if (showTemplates) Tabs.SelectedItem = TemplatesTab;
    }

    private string OpenPath => _viewModel.CurrentDbPath;

    private void RefreshFiles(string? select = null)
    {
        _config = AppConfig.Load();
        var rows = _config.FilesByRecency
            .Select(f => new FileRow(f, AppConfig.ArePathsEqual(f.Path, OpenPath), _config.StartupDbPath is { } s && AppConfig.ArePathsEqual(s, f.Path)))
            .ToList();
        FileList.ItemsSource = rows;
        FileList.SelectedItem = rows.FirstOrDefault(r => select is not null ? AppConfig.ArePathsEqual(r.Path, select) : r.IsOpen);
        FileList_SelectionChanged(this, null!);
    }

    private FileRow? Selected => FileList.SelectedItem as FileRow;

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = Selected;
        var any = row is not null;
        var exists = row is { IsMissing: false };
        SwitchButton.IsEnabled = exists && !row!.IsOpen;
        DuplicateButton.IsEnabled = exists;
        EditButton.IsEnabled = any;
        StartupButton.IsEnabled = exists;
        StartupButton.Content = row is { IsStartup: true } ? "Don't _Use at Startup" : "_Use at Startup";
        MoveButton.IsEnabled = exists && !row!.IsOpen;
        ReconnectButton.IsEnabled = any && !row!.IsOpen;
        SaveTemplateButton.IsEnabled = exists;
        RemoveButton.IsEnabled = any && !row!.IsOpen;
        DeleteButton.IsEnabled = any && !row!.IsOpen;
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SwitchButton.IsEnabled) Switch_Click(sender, e);
    }

    private void FileList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && RemoveButton.IsEnabled) Remove_Click(sender, e);
        else if (e.Key == Key.F2 && EditButton.IsEnabled) Edit_Click(sender, e);
        else return;
        e.Handled = true;
    }

    private void Switch_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row || row.IsOpen) return;
        if (!CheckCanOpen(row.Path)) return;
        SwitchToPath = row.Path;
        DialogResult = true;
    }

    // A file is opened only when it is a task file this copy of the app can read.
    private bool CheckCanOpen(string path)
    {
        var info = TaskFileInspector.Inspect(path);
        if (!info.IsTaskFile)
        {
            Dialogs.Tell(this, "Cannot Open", $"This file cannot be opened.\n\n{info.Problem}", DialogTone.Warning, path);
            return false;
        }
        if (info.IsFromNewerApp)
        {
            return Dialogs.Confirm(this, DialogMessage.AskDanger("Newer Task File",
                "This task file was last used by a newer version of the app. If you carry on, tasks you change here can lose details that only the newer version knows about.\n\nOpen it anyway?",
                "Open It Anyway") with { Detail = path });
        }
        return true;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var path = AskForNewFilePath("Create New Task File", "New Tasks");
        if (path is null) return;
        var details = new TaskFileDetailsWindow("New Task File", "A name for the list here, and a note about what the file is for.", Path.GetFileNameWithoutExtension(path), null, "Create") { Owner = this };
        if (details.ShowDialog() != true) return;

        try
        {
            _ = new DatabaseService(path); // makes the empty file with its standard columns
            Register(path, details.Name, details.Description);
            OfferToOpen(path, "The new task file is ready.");
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "New File", "The task file could not be made.", DialogTone.Error, ex.Message);
        }
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        var path = AskForNewFilePath("Duplicate Task File", row.Name + " copy", Path.GetDirectoryName(row.Path));
        if (path is null) return;
        var details = new TaskFileDetailsWindow("Duplicate Task File", "The copy is a separate board from now on: changes in one do not reach the other.", row.Name + " copy", row.Description, "Duplicate") { Owner = this };
        if (details.ShowDialog() != true) return;

        try
        {
            TaskFileService.DuplicateAsNewFile(row.Path, path);
            Register(path, details.Name, details.Description);
            OfferToOpen(path, "The copy is ready.");
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Duplicate", "The task file was not copied.", DialogTone.Error, ex.Message);
        }
    }

    // A task file from elsewhere: this PC's own, forgotten, or one copied from another computer.
    // Registered where it is, or copied first to a folder of the user's choosing.
    private void AddExisting_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Add Existing Task File", Filter = TaskFileService.FileFilter, InitialDirectory = Path.GetDirectoryName(OpenPath) };
        if (dialog.ShowDialog(this) != true) return;
        var source = dialog.FileName;

        var info = TaskFileInspector.Inspect(source);
        if (!info.IsTaskFile)
        {
            Dialogs.Tell(this, "Not a Task File", $"This file was not added.\n\n{info.Problem}", DialogTone.Warning, source);
            return;
        }
        if (_config.Entry(source) is { } already)
        {
            Dialogs.Tell(this, "Already on the List", $"That file is already on the list as \"{already.DisplayName}\".");
            RefreshFiles(source);
            return;
        }

        // The same file (by its identity) is already registered at another path: moved or copied by hand.
        var twin = info.FileId is null ? null : _config.Files.FirstOrDefault(f => !AppConfig.ArePathsEqual(f.Path, source) && TaskFileInspector.Inspect(f.Path).FileId == info.FileId);
        if (twin is not null)
        {
            var choice = Dialogs.Show(this, new DialogMessage("Same Task File",
                $"This looks like the same task file as \"{twin.DisplayName}\", which is on the list at another location.\n\n" +
                "If you moved it, choose Reconnect and the list points at the new location. If it is a copy you want as a separate board, choose Add as a Copy and it gets an identity of its own.")
            { Tone = DialogTone.Question, Yes = "Reconnect", No = "Add as a Copy", Detail = $"On the list: {twin.Path}\nChosen: {source}", Cancel = "Cancel" });
            if (choice == DialogChoice.Cancel) return;
            if (choice == DialogChoice.Yes)
            {
                _config.Repoint(twin.Path, source);
                _config.Save();
                RefreshFiles(source);
                return;
            }
            var copy = new DatabaseService(source);
            copy.AssignNewFileId();
        }

        var where = Dialogs.Show(this, new DialogMessage("Add Existing Task File",
            "Use the file where it is, or copy it to a folder of your choosing first?\n\nUse Where It Is suits a file on this PC. Copy and Add suits a file on a USB drive or a network folder, which you want on this PC's own disk.")
        { Tone = DialogTone.Question, Yes = "Use Where It Is", No = "Copy and Add...", Cancel = "Cancel" });
        if (where == DialogChoice.Cancel) return;

        var path = source;
        if (where == DialogChoice.No)
        {
            var dest = AskForNewFilePath("Copy Task File To", Path.GetFileNameWithoutExtension(source));
            if (dest is null) return;
            try
            {
                TaskFileService.CopyTaskFile(source, dest);
            }
            catch (Exception ex)
            {
                Dialogs.Tell(this, "Copy", "The task file was not copied.", DialogTone.Error, ex.Message);
                return;
            }
            path = dest;
        }

        var details = new TaskFileDetailsWindow("Add Existing Task File", "A name for the list here, and a note about what the file is for.", Path.GetFileNameWithoutExtension(path), null, "Add") { Owner = this };
        if (details.ShowDialog() != true) return;
        Register(path, details.Name, details.Description);
        if (TaskFileService.LooksSynced(path)) Dialogs.Tell(this, "Synced Folder", TaskFileService.SyncedFolderWarning, DialogTone.Warning, path);
        OfferToOpen(path, $"\"{details.Name}\" is on the list{(info.OpenTaskCount > 0 ? $", with {info.OpenTaskCount} open task{(info.OpenTaskCount == 1 ? "" : "s")}" : "")}.");
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        var details = new TaskFileDetailsWindow("Edit Name", "The name and description shown on the list. The file itself keeps its own name.", row.Name, row.Description) { Owner = this };
        if (details.ShowDialog() != true) return;
        row.Entry.Name = details.Name;
        row.Entry.Description = details.Description;
        _config.Save();
        CatalogueChanged = true;
        RefreshFiles(row.Path);
    }

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        _config.StartupDbPath = row.IsStartup ? null : row.Path;
        _config.Save();
        RefreshFiles(row.Path);
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row || row.IsOpen) return;
        var dest = AskForNewFilePath("Move Task File To", Path.GetFileNameWithoutExtension(row.Path), Path.GetDirectoryName(row.Path));
        if (dest is null) return;
        try
        {
            TaskFileService.MoveTaskFile(row.Path, dest);
            _config.Repoint(row.Path, dest);
            _config.Save();
            CatalogueChanged = true;
            RefreshFiles(dest);
            if (TaskFileService.LooksSynced(dest)) Dialogs.Tell(this, "Synced Folder", TaskFileService.SyncedFolderWarning, DialogTone.Warning, dest);
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Move", "The task file was not moved.", DialogTone.Error, ex.Message);
            RefreshFiles(row.Path);
        }
    }

    private void Reconnect_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = $"Find \"{row.Name}\"", Filter = TaskFileService.FileFilter, FileName = Path.GetFileName(row.Path) };
        if (dialog.ShowDialog(this) != true) return;
        var info = TaskFileInspector.Inspect(dialog.FileName);
        if (!info.IsTaskFile)
        {
            Dialogs.Tell(this, "Not a Task File", $"The list was not changed.\n\n{info.Problem}", DialogTone.Warning, dialog.FileName);
            return;
        }
        if (_config.Entry(dialog.FileName) is { } other && other != row.Entry)
        {
            Dialogs.Tell(this, "Already on the List", $"That file is already on the list as \"{other.DisplayName}\". Remove one of the two entries instead.");
            return;
        }
        _config.Repoint(row.Path, dialog.FileName);
        _config.Save();
        CatalogueChanged = true;
        RefreshFiles(dialog.FileName);
    }

    private void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        if (SaveTemplateFrom(row.Path, row.Name)) Tabs.SelectedItem = TemplatesTab;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row || row.IsOpen) return;
        if (!Dialogs.Confirm(this, DialogMessage.Ask("Remove from List", $"Take \"{row.Name}\" off the list?\n\nThe file stays where it is, untouched. Add Existing File puts it back on the list.", "Remove") with { Detail = row.Path })) return;
        _config.Remove(row.Path);
        _config.Save();
        RefreshFiles();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row || row.IsOpen) return;
        var tasks = row.IsMissing ? 0 : TaskFileInspector.Inspect(row.Path).OpenTaskCount;
        if (!Dialogs.Confirm(this, DialogMessage.AskDanger("Delete Task File",
                $"Delete \"{row.Name}\"{(tasks > 0 ? $", with its {tasks} open task{(tasks == 1 ? "" : "s")}" : "")}?\n\nThe file goes to the Recycle Bin with its Attachments and Backups folders, so it can be put back from there.",
                "Delete") with { Detail = row.Path })) return;
        try
        {
            if (!row.IsMissing) TaskFileService.SendToRecycleBin(row.Path);
            _config.Remove(row.Path);
            _config.Save();
            RefreshFiles();
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Delete", "The task file was not deleted.", DialogTone.Error, ex.Message);
        }
    }

    private void Register(string path, string name, string? description)
    {
        _config = AppConfig.Load();
        _config.Register(path, name, description);
        _config.Save();
        CatalogueChanged = true;
        RefreshFiles(path);
    }

    // A new or newly added file can be opened at once.
    private void OfferToOpen(string path, string lead)
    {
        if (!Dialogs.Confirm(this, new DialogMessage("Open It Now?", $"{lead}\n\nOpen it now, in place of the file that is open?") { Tone = DialogTone.Question, Yes = "Open It", No = "Not Now" })) return;
        SwitchToPath = path;
        DialogResult = true;
    }

    // Where a new file goes: the user picks the folder and name; the app never decides for them.
    private string? AskForNewFilePath(string title, string suggestedName, string? folder = null)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = title, FileName = SafeName(suggestedName), DefaultExt = ".db", Filter = TaskFileService.FileFilter,
            InitialDirectory = folder ?? Path.GetDirectoryName(OpenPath), OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != true) return null;
        if (File.Exists(dialog.FileName))
        {
            Dialogs.Tell(this, title, "There is already a file with that name, so nothing was done.\n\nChoose a different name, or Add Existing File to use that one.", DialogTone.Warning, dialog.FileName);
            return null;
        }
        if (AppConfig.ArePathsEqual(Path.GetDirectoryName(dialog.FileName)!, Path.GetDirectoryName(OpenPath)!) && Directory.Exists(DatabaseService.GetAttachmentsDir(dialog.FileName)))
        {
            // Two task files in one folder would share one Attachments folder.
            Dialogs.Tell(this, title, "Put each task file in a folder of its own: its Attachments and Backups folders sit beside it, and two files in one folder would share them.", DialogTone.Warning);
            return null;
        }
        return dialog.FileName;
    }

    private static string SafeName(string name) => TaskFileTemplates.SafeFileName(name);
}
