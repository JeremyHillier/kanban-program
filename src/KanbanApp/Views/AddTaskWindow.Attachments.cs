using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The task's attached files: the rows, adding by file picker, drag and drop or a pasted
// screenshot, and on close removing any file pasted in this session that didn't get saved.
public partial class AddTaskWindow
{
    private void AddAttachmentRow(AttachmentViewModel attachment)
    {
        // Transparent rather than no background, so a right-click anywhere on the row opens its menu.
        var row = new Grid { Margin = new Thickness(0, 0, 0, 6), Tag = attachment, Background = Brushes.Transparent };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        FrameworkElement preview;
        if (attachment.IsImage && File.Exists(attachment.FilePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 40;
                bitmap.UriSource = new Uri(attachment.FilePath);
                bitmap.EndInit();
                preview = new Image { Source = bitmap, Width = 40, Height = 40, Stretch = Stretch.UniformToFill, Margin = new Thickness(0, 0, 8, 0) };
            }
            catch
            {
                preview = new TextBlock { Text = "🖼", FontSize = 20, Width = 40, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            }
        }
        else
        {
            preview = new TextBlock { Text = "📄", FontSize = 20, Width = 40, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        }
        Grid.SetColumn(preview, 0);
        preview.Cursor = Cursors.Hand;
        preview.MouseLeftButtonUp += (_, _) => OpenAttachment(attachment.FilePath);

        var nameText = new TextBlock
        {
            Text = attachment.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Cursor = Cursors.Hand,
            ToolTip = $"{attachment.FilePath}\nClick to open. Right-click to copy, save a copy or show it in its folder."
        };
        nameText.MouseLeftButtonUp += (_, _) => OpenAttachment(attachment.FilePath);
        Grid.SetColumn(nameText, 1);

        var deleteButton = new Button
        {
            Content = "×",
            Width = 26,
            Height = 26,
            Margin = new Thickness(6, 0, 0, 0),
            Background = (Brush)FindResource("ButtonBackgroundBrush"),
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            ToolTip = "Remove attachment"
        };
        Grid.SetColumn(deleteButton, 2);
        deleteButton.Click += (_, _) => AttachmentsPanel.Children.Remove(row);

        row.Children.Add(preview);
        row.Children.Add(nameText);
        row.Children.Add(deleteButton);
        AttachAttachmentMenu(row, attachment);
        AttachmentsPanel.Children.Add(row);
    }

    private static void OpenAttachment(string path)
    {
        if (!File.Exists(path))
        {
            Dialogs.Tell(ActiveWindow, "File Not Found",
                "This attachment can no longer be found.\n\nIt may have been moved, renamed or deleted, or be on a drive that is not connected.", DialogTone.Warning, path);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Tell(ActiveWindow, "Could Not Open", "Windows could not open this attachment. There may be no program set up for this kind of file.",
                DialogTone.Error, $"{path}\n\n{ex.Message}");
        }
    }

    // The window a message about an attachment belongs over: whichever of the app's windows is in front.
    private static Window? ActiveWindow => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a File to Attach",
            Filter = "All Files (*.*)|*.*",
            InitialDirectory = Directory.Exists(_viewModel.LinkedFilesDefaultPath) ? _viewModel.LinkedFilesDefaultPath : null
        };

        if (dialog.ShowDialog() != true) return;

        string destPath;
        try
        {
            destPath = OutlookDragDropHelper.CopyFileIntoAttachmentsDir(dialog.FileName, _viewModel.AttachmentsDir);
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Could Not Attach", "The file could not be copied into the attachments folder, so it was not attached.", DialogTone.Error, ex.Message);
            return;
        }

        _sessionPastedFilePaths.Add(destPath);

        var attachment = new AttachmentViewModel(new CardAttachment
        {
            FilePath = destPath,
            DisplayName = Path.GetFileName(destPath),
            AddedDate = DateTime.Now
        });
        AddAttachmentRow(attachment);
    }

    private void AttachmentsDropZone_DragOver(object sender, DragEventArgs e)
    {
        var canDrop = OutlookDragDropHelper.HasDroppableFiles(e.Data);
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void AttachmentsDropZone_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!OutlookDragDropHelper.HasDroppableFiles(e.Data)) return;

        List<(string FilePath, string DisplayName, bool WasSaved)> files;
        try
        {
            files = OutlookDragDropHelper.ExtractDroppedFiles(e.Data, _viewModel.AttachmentsDir);
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Could Not Attach", "The dropped item could not be read, so nothing was attached.", DialogTone.Error, ex.Message);
            return;
        }

        foreach (var file in files)
        {
            if (file.WasSaved)
            {
                _sessionPastedFilePaths.Add(file.FilePath);
            }

            AddAttachmentRow(new AttachmentViewModel(new CardAttachment
            {
                FilePath = file.FilePath,
                DisplayName = file.DisplayName,
                AddedDate = DateTime.Now
            }));
        }
    }

    private void PasteScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsImage())
        {
            Dialogs.Tell(this, "No Image Found",
                "There is no picture on the clipboard.\n\nCopy a screenshot first, for example with the Snipping Tool (Windows+Shift+S) or Print Screen, then try again.");
            return;
        }

        BitmapSource? image;
        try
        {
            image = Clipboard.GetImage();
        }
        catch (Exception ex)
        {
            Dialogs.Tell(this, "Could Not Paste", "The picture on the clipboard could not be read, so nothing was attached.", DialogTone.Error, ex.Message);
            return;
        }

        if (image is null) return;

        var attachmentsDir = _viewModel.AttachmentsDir;
        Directory.CreateDirectory(attachmentsDir);

        var fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        var filePath = Path.Combine(attachmentsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }

        _sessionPastedFilePaths.Add(filePath);

        var attachment = new AttachmentViewModel(new CardAttachment
        {
            FilePath = filePath,
            DisplayName = fileName,
            AddedDate = DateTime.Now
        });
        AddAttachmentRow(attachment);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        var survivingPaths = SelectedAttachments.Select(a => a.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in _sessionPastedFilePaths)
        {
            if (survivingPaths.Contains(path)) continue;
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }
    }
}
