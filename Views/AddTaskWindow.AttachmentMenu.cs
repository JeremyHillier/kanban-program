using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The right-click menu on an attachment row: open it, show it in its folder, copy the file or its
// path, save a copy somewhere else, or remove it from the task.
public partial class AddTaskWindow
{
    private void AttachAttachmentMenu(Grid row, AttachmentViewModel attachment)
    {
        var menu = new ContextMenu();
        row.ContextMenu = menu;
        // Rebuilt on every open, so it knows whether the file is still there.
        row.ContextMenuOpening += (_, _) => FillAttachmentMenu(menu, row, attachment);
    }

    internal void FillAttachmentMenu(ContextMenu menu, Grid row, AttachmentViewModel attachment)
    {
        menu.Items.Clear();
        var path = attachment.FilePath;
        var exists = File.Exists(path);

        if (!exists)
        {
            menu.Items.Add(new MenuItem { Header = "This file can no longer be found", IsEnabled = false });
            menu.Items.Add(new Separator());
        }

        AddAttachmentMenuItem(menu, "_Open", exists, () => OpenAttachment(path));
        AddAttachmentMenuItem(menu, "Show in _Folder", exists, () => Try("show the file in its folder", () => AttachmentFiles.ShowInFolder(path)));
        menu.Items.Add(new Separator());
        AddAttachmentMenuItem(menu, "_Copy", exists, () => Try("copy the file", () => AttachmentFiles.CopyToClipboard(AttachmentFiles.BuildClipboardData(path))),
            "Copies the file, ready to paste into a folder, an email or a chat");
        AddAttachmentMenuItem(menu, "Copy _Path", true, () => Try("copy the path", () => AttachmentFiles.CopyToClipboard(new DataObject(DataFormats.UnicodeText, path))));
        AddAttachmentMenuItem(menu, "_Save a Copy As...", exists, () => SaveAttachmentCopy(attachment));
        menu.Items.Add(new Separator());
        AddAttachmentMenuItem(menu, "_Remove", true, () => AttachmentsPanel.Children.Remove(row), "Takes the file off this task");
    }

    // Runs after the menu has closed, as the board's own right-click menus do.
    private void AddAttachmentMenuItem(ContextMenu menu, string header, bool isEnabled, Action action, string? toolTip = null)
    {
        var item = new MenuItem { Header = header, IsEnabled = isEnabled, ToolTip = toolTip };
        item.Click += (_, _) => Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        menu.Items.Add(item);
    }

    private void SaveAttachmentCopy(AttachmentViewModel attachment)
    {
        var fileName = string.IsNullOrWhiteSpace(attachment.DisplayName) ? Path.GetFileName(attachment.FilePath) : attachment.DisplayName;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save a Copy of the Attachment",
            FileName = fileName,
            Filter = AttachmentFiles.SaveFilter(fileName)
        };
        if (dialog.ShowDialog(this) != true) return;

        Try("save the copy", () => AttachmentFiles.SaveCopy(attachment.FilePath, dialog.FileName));
    }

    private void Try(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Attachment", $"Could not {what}.", DialogTone.Warning, ex.Message);
        }
    }
}
