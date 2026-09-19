using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using KanbanApp.Models;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Composes an email for a card. Classic desktop Outlook is driven through late-bound COM automation
// (no Office interop reference, so building the app doesn't need Outlook) and gets the full treatment:
// the user's own signature and the task's files attached. The new Outlook for Windows, other mail apps,
// and PCs without Outlook have no automation to drive, so they get a mailto: link opened in the default
// email app instead, with the files that can't be attached that way put in a folder to drag in.
// Either way a compose window opens for the user to review - nothing is ever sent automatically.
public static class OutlookEmailHelper
{
    // Mail apps and Windows itself cut off, or refuse to open, very long mailto: links.
    internal const int MaxMailtoLength = 2000;

    private static readonly string AttachmentFoldersRoot = Path.Combine(Path.GetTempPath(), "Kanban Task Board Email");

    private static bool _defaultMailAppNoticeShown;

    // recipientEmail is passed explicitly rather than always reading card.WhoEmail: the Add/Edit
    // Task dialog needs to email the currently-selected Who in its combo box, which can be a live,
    // not-yet-saved change that hasn't made it onto the CardViewModel yet.
    public static void ComposeCardEmail(Window owner, CardViewModel card, string recipientEmail, MainViewModel viewModel)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail)) return;

        if (TryComposeInClassicOutlook(owner, card, recipientEmail, viewModel)) return;
        ComposeInDefaultMailApp(owner, card, recipientEmail.Trim(), viewModel);
    }

    // False only when Outlook couldn't open a compose window at all, so the caller can fall back. A
    // failure after the window is already showing is reported here instead - falling back at that
    // point would open a second, duplicate email.
    private static bool TryComposeInClassicOutlook(Window owner, CardViewModel card, string recipientEmail, MainViewModel viewModel)
    {
        dynamic mailItem;
        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType is null) return false;

            // Outlook enforces single-instance at the OS level, so CreateInstance attaches to an
            // already-running instance rather than launching a duplicate.
            dynamic app = Activator.CreateInstance(outlookType)!;
            mailItem = app.CreateItem(0); // olMailItem
            mailItem.To = recipientEmail;
            mailItem.Subject = EmailSubject(card);

            // Display before setting HTMLBody so Outlook inserts the user's own default "new message"
            // signature the way it would for a message composed by hand; reading HTMLBody back then
            // captures it, and our content goes in ahead of it rather than overwriting it.
            mailItem.Display(false);
        }
        catch
        {
            return false;
        }

        try
        {
            string outlookHtml = mailItem.HTMLBody ?? string.Empty;

            var content = BuildHtmlBody(card);
            if (!HasVisibleContent(outlookHtml))
            {
                var fallbackSignature = BuildFallbackSignature(viewModel);
                if (fallbackSignature is not null) content += fallbackSignature;
            }

            mailItem.HTMLBody = InsertAfterBodyTag(outlookHtml, content);

            foreach (var attachment in card.Attachments)
            {
                if (File.Exists(attachment.FilePath))
                {
                    mailItem.Attachments.Add(attachment.FilePath);
                }
            }

            AttachImportFile(mailItem, card, viewModel);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"The email opened, but couldn't be fully filled in: {ex.Message}", "Email", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return true;
    }

    private static void ComposeInDefaultMailApp(Window owner, CardViewModel card, string recipientEmail, MainViewModel viewModel)
    {
        var attachmentPaths = card.Attachments.Select(a => a.FilePath).Where(File.Exists).ToList();

        // Once per run: after the first time the user knows why a folder opens alongside the email.
        if (!_defaultMailAppNoticeShown)
        {
            _defaultMailAppNoticeShown = true;
            var files = attachmentPaths.Count == 0
                ? "this task's Excel file"
                : $"this task's Excel file and its {attachmentPaths.Count} attachment{(attachmentPaths.Count == 1 ? "" : "s")}";
            MessageBox.Show(owner,
                "Classic Outlook isn't available on this PC, so this email will open in your default email app instead.\n\n" +
                $"Files can't be attached for you that way, so a folder with {files} will open too. Drag them into the email.",
                "Email This Task", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Opened before the email so the compose window ends up in front of it.
        try
        {
            var folder = PrepareAttachmentFolder(AttachmentFoldersRoot, card.Title, BuildImportRow(card, viewModel), attachmentPaths);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch
        {
            // The folder is a convenience; the email itself can still go ahead without it.
        }

        var subject = EmailSubject(card);
        var body = BuildPlainTextBody(card, BuildPlainTextSignature(viewModel));
        try
        {
            Process.Start(new ProcessStartInfo(BuildMailtoUri(recipientEmail, subject, body)) { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Clipboard.SetText($"To: {recipientEmail}\r\nSubject: {subject}\r\n\r\n{body}");
            }
            catch
            {
                // Clipboard busy - the message below still tells them what happened.
            }

            MessageBox.Show(owner,
                "No email app is set up to open email links on this PC.\n\n" +
                $"The email has been copied to the clipboard instead. Paste it into a new message to {recipientEmail}.",
                "Email This Task", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // A plain email with files attached, for anything that isn't a task (the problem report). Same
    // rules as a task email: classic Outlook attaches the files; otherwise the default mail app opens
    // with the text and a folder holding the files opens alongside it to drag in.
    internal static void ComposeEmailWithFiles(Window? owner, string recipient, string subject, string body,
        IReadOnlyList<string> filePaths, string folderName, string dialogTitle)
    {
        if (TryComposePlainInClassicOutlook(owner, recipient, subject, body, filePaths, dialogTitle)) return;

        var existing = filePaths.Where(File.Exists).ToList();
        if (existing.Count > 0)
        {
            ShowMessage(owner,
                "Classic Outlook isn't available on this PC, so this email will open in your default email app instead.\n\n" +
                "Files can't be attached for you that way, so a folder with them will open too. Drag them into the email.",
                dialogTitle, MessageBoxImage.Information);
            try
            {
                var folder = Path.Combine(AttachmentFoldersRoot, SanitizeFileName(folderName));
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                Directory.CreateDirectory(folder);
                foreach (var path in existing) File.Copy(path, Path.Combine(folder, Path.GetFileName(path)));
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch
            {
                // The folder is a convenience; the email itself can still go ahead without it.
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo(BuildMailtoUri(recipient, subject, body)) { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Clipboard.SetText($"To: {recipient}\r\nSubject: {subject}\r\n\r\n{body}");
            }
            catch
            {
                // Clipboard busy - the message below still tells them what happened.
            }

            ShowMessage(owner,
                "No email app is set up to open email links on this PC.\n\n" +
                $"The email has been copied to the clipboard instead. Paste it into a new message to {recipient}.",
                dialogTitle, MessageBoxImage.Warning);
        }
    }

    private static bool TryComposePlainInClassicOutlook(Window? owner, string recipient, string subject, string body,
        IReadOnlyList<string> filePaths, string dialogTitle)
    {
        dynamic mailItem;
        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType is null) return false;

            dynamic app = Activator.CreateInstance(outlookType)!;
            mailItem = app.CreateItem(0); // olMailItem
            mailItem.To = recipient;
            mailItem.Subject = subject;
            mailItem.Display(false);
        }
        catch
        {
            return false;
        }

        try
        {
            var html = "<div style=\"font-family: Segoe UI, sans-serif; font-size: 11pt;\">" +
                WebUtility.HtmlEncode(body).Replace("\r\n", "\n").Replace("\n", "<br/>") + "</div>";
            mailItem.HTMLBody = InsertAfterBodyTag((string)(mailItem.HTMLBody ?? string.Empty), html);

            foreach (var path in filePaths.Where(File.Exists))
            {
                mailItem.Attachments.Add(path);
            }
        }
        catch (Exception ex)
        {
            ShowMessage(owner, $"The email opened, but couldn't be fully filled in: {ex.Message}", dialogTitle, MessageBoxImage.Error);
        }

        return true;
    }

    private static void ShowMessage(Window? owner, string message, string title, MessageBoxImage icon)
    {
        if (owner is not null)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, icon);
        }
        else
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }
    }

    internal static string EmailSubject(CardViewModel card) => $"Task: {card.Title}";

    // A long body is shortened to fit, ending in an ellipsis, rather than the link failing to open.
    internal static string BuildMailtoUri(string recipient, string subject, string body)
    {
        var to = Uri.EscapeDataString(recipient).Replace("%40", "@");
        string Build(string text) => $"mailto:{to}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(text)}";

        var uri = Build(body);
        var keep = body.Length;
        while (uri.Length > MaxMailtoLength && keep > 0)
        {
            keep = (int)(keep * 0.9);
            if (keep > 0 && char.IsHighSurrogate(body[keep - 1])) keep--;
            uri = Build(body[..keep].TrimEnd() + "\r\n…");
        }

        return uri;
    }

    // Recreated each time, so files from an earlier email of the same task never linger alongside.
    internal static string PrepareAttachmentFolder(string rootDir, string taskTitle, ImportedTaskRow importRow, IEnumerable<string> attachmentPaths)
    {
        var folder = Path.Combine(rootDir, SanitizeFileName(taskTitle));
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);

        ImportService.SaveSingleTaskFile(Path.Combine(folder, ImportFileName(taskTitle)), importRow);

        foreach (var path in attachmentPaths.Where(File.Exists))
        {
            var destination = Path.Combine(folder, Path.GetFileName(path));
            for (var n = 2; File.Exists(destination); n++)
            {
                destination = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(path)} ({n}){Path.GetExtension(path)}");
            }
            File.Copy(path, destination);
        }

        return folder;
    }

    internal static string BuildPlainTextBody(CardViewModel card, string? signature)
    {
        var sb = new StringBuilder();
        sb.Append(card.Title).Append("\r\n\r\n");
        sb.Append("Project: ").Append(card.ProjectName).Append("\r\n");
        sb.Append("Priority: ").Append(card.Priority).Append("\r\n");
        if (card.IsWaiting) sb.Append("Waiting on: ").Append(card.WaitingOn).Append("\r\n");
        if (card.StartDate.HasValue) sb.Append("Start: ").Append(FormatStart(card)).Append("\r\n");
        if (card.DueDate.HasValue) sb.Append("Due: ").Append(FormatDue(card)).Append("\r\n");
        if (HasGoal(card)) sb.Append("Goal: ").Append(card.GoalName).Append("\r\n");
        if (card.Flags.Count > 0) sb.Append("Flags: ").Append(string.Join(", ", card.Flags.Select(f => f.Name))).Append("\r\n");

        if (!string.IsNullOrWhiteSpace(card.Notes))
        {
            sb.Append("\r\nNotes:\r\n").Append(card.Notes.Replace("\r\n", "\n").Replace("\n", "\r\n")).Append("\r\n");
        }

        if (card.SubTasks.Count > 0)
        {
            sb.Append("\r\nSub-tasks:\r\n");
            foreach (var subTask in card.SubTasks)
            {
                sb.Append(subTask.IsDone ? "[x] " : "[ ] ").Append(subTask.Title).Append("\r\n");
            }
        }

        sb.Append("\r\nIf an Excel file is attached, open Kanban Task Board and click Import Tasks to add this task to your own board.\r\n");

        if (signature is not null) sb.Append("\r\n").Append(signature);
        return sb.ToString().TrimEnd();
    }

    private static string? BuildPlainTextSignature(MainViewModel viewModel)
    {
        var lines = new[] { viewModel.UserName, viewModel.UserTitle, viewModel.UserEmail, viewModel.UserPhone }
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        return lines.Count == 0 ? null : string.Join("\r\n", lines);
    }

    // Outlook still returns a full (if empty) HTML document even with no signature configured, so
    // "is there a signature" means "is there any rendered text inside <body>", not "is HTMLBody non-empty".
    private static bool HasVisibleContent(string html)
    {
        var bodyMatch = Regex.Match(html, "<body[^>]*>(.*)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var bodyInner = bodyMatch.Success ? bodyMatch.Groups[1].Value : html;
        return Regex.Replace(bodyInner, "<[^>]+>", "").Trim().Length > 0;
    }

    private static string InsertAfterBodyTag(string html, string contentHtml)
    {
        if (string.IsNullOrWhiteSpace(html)) return $"<html><body>{contentHtml}</body></html>";

        var bodyTagMatch = Regex.Match(html, "<body[^>]*>", RegexOptions.IgnoreCase);
        if (!bodyTagMatch.Success) return contentHtml + html;

        var insertAt = bodyTagMatch.Index + bodyTagMatch.Length;
        return html.Insert(insertAt, contentHtml);
    }

    // Attaches a one-row Excel file (same headers ImportService.ReadTasks looks for) so the
    // recipient can pull this task straight into their own board via Import Tasks, instead of
    // retyping it from the email body. Written to a temp file and deleted right after attaching -
    // Outlook copies attachment content into the message on Add, so the source no longer needs to
    // exist once that call returns.
    private static void AttachImportFile(dynamic mailItem, CardViewModel card, MainViewModel viewModel)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), ImportFileName(card.Title));
        try
        {
            ImportService.SaveSingleTaskFile(tempPath, BuildImportRow(card, viewModel));
            mailItem.Attachments.Add(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static ImportedTaskRow BuildImportRow(CardViewModel card, MainViewModel viewModel) => new()
    {
        Title = card.Title,
        Category = viewModel.Columns.FirstOrDefault(c => c.Id == card.ColumnId)?.DisplayName,
        Priority = card.Priority,
        Project = card.ProjectName,
        Goal = HasGoal(card) ? card.GoalName : null,
        DueDate = card.DueDate,
        StartDate = card.StartDate,
        WaitingOn = card.WaitingOn,
        Who = card.WhoName == "Unassigned" ? null : card.WhoName
    };

    private static string ImportFileName(string taskTitle) => $"KanbanTask_{SanitizeFileName(taskTitle)}.xlsx";

    // Also used as a folder name, so it must never come back empty - Path.Combine(root, "") is root
    // itself, which PrepareAttachmentFolder would then delete.
    internal static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (cleaned.Length > 60) cleaned = cleaned[..60];
        cleaned = cleaned.TrimEnd('.', ' '); // Windows silently drops trailing dots and spaces from names
        return cleaned.Length == 0 ? "Task" : cleaned;
    }

    internal static bool HasGoal(CardViewModel card) => !string.IsNullOrWhiteSpace(card.GoalName) && card.GoalName != "No Goal";

    internal static string FormatStart(CardViewModel card) => card.StartDate!.Value.ToString("dd-MMM-yyyy");

    internal static string FormatDue(CardViewModel card) =>
        card.DueDateTime is { } dueAt ? dueAt.ToString("dd-MMM-yyyy h:mm tt") : card.DueDate!.Value.ToString("dd-MMM-yyyy");

    private static string? BuildFallbackSignature(MainViewModel viewModel)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(viewModel.UserName)) lines.Add($"<b>{WebUtility.HtmlEncode(viewModel.UserName)}</b>");
        if (!string.IsNullOrWhiteSpace(viewModel.UserTitle)) lines.Add(WebUtility.HtmlEncode(viewModel.UserTitle));
        if (!string.IsNullOrWhiteSpace(viewModel.UserEmail)) lines.Add(WebUtility.HtmlEncode(viewModel.UserEmail));
        if (!string.IsNullOrWhiteSpace(viewModel.UserPhone)) lines.Add(WebUtility.HtmlEncode(viewModel.UserPhone));

        if (lines.Count == 0) return null;

        return "<p style=\"margin-top:20px;font-family:'Segoe UI',sans-serif;font-size:11pt;color:#333;\">" +
            string.Join("<br/>", lines) + "</p>";
    }

    private static string BuildHtmlBody(CardViewModel card)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family: Segoe UI, sans-serif; font-size: 11pt;\">");
        sb.Append($"<p style=\"font-size: 13pt;\"><b>{WebUtility.HtmlEncode(card.Title)}</b></p>");

        sb.Append("<table style=\"border-collapse: collapse;\">");
        AppendRow(sb, "Project", card.ProjectName);
        AppendRow(sb, "Priority", card.Priority);
        if (card.IsWaiting) AppendRow(sb, "Waiting on", card.WaitingOn!);
        if (card.StartDate.HasValue) AppendRow(sb, "Start", FormatStart(card));
        if (card.DueDate.HasValue) AppendRow(sb, "Due", FormatDue(card));
        if (HasGoal(card)) AppendRow(sb, "Goal", card.GoalName);
        if (card.Flags.Count > 0) AppendRow(sb, "Flags", string.Join(", ", card.Flags.Select(f => f.Name)));
        sb.Append("</table>");

        if (!string.IsNullOrWhiteSpace(card.Notes))
        {
            sb.Append("<p><b>Notes</b><br/>" + WebUtility.HtmlEncode(card.Notes).Replace("\n", "<br/>") + "</p>");
        }

        if (card.SubTasks.Count > 0)
        {
            sb.Append("<p><b>Sub-tasks</b></p><ul style=\"margin-top: 0;\">");
            foreach (var subTask in card.SubTasks)
            {
                var mark = subTask.IsDone ? "&#9745;" : "&#9744;";
                sb.Append($"<li>{mark} {WebUtility.HtmlEncode(subTask.Title)}</li>");
            }
            sb.Append("</ul>");
        }

        if (card.Attachments.Count > 0)
        {
            sb.Append($"<p style=\"color: #777;\">{card.Attachments.Count} attachment(s) included.</p>");
        }

        sb.Append("<p style=\"color: #777;\">This task is also attached as an Excel file - if you use Kanban Task Board, "
            + "click Import Tasks and select it to add this task to your own board directly.</p>");

        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string value)
    {
        sb.Append($"<tr><td style=\"padding: 2px 12px 2px 0; color: #555;\"><b>{WebUtility.HtmlEncode(label)}</b></td>" +
                  $"<td style=\"padding: 2px 0;\">{WebUtility.HtmlEncode(value)}</td></tr>");
    }
}
