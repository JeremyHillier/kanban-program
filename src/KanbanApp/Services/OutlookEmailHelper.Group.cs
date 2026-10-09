using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Emailing several tasks at once - a selection, or every open task of a project. One email: a
// table of the tasks in the body and one Excel file holding all of them, which the recipient
// imports in one go (each task keeps its Task ID, so sending them back updates the same tasks).
// The tasks' own attachments are left out; a project's worth could be any size. Same routes as a
// single task: classic Outlook, else the default email app with the Excel file in a folder to drag in.
public static partial class OutlookEmailHelper
{
    public static void ComposeTasksEmail(Window owner, IReadOnlyList<CardViewModel> cards, MainViewModel viewModel)
    {
        if (cards.Count == 0) return;

        var recipients = GroupRecipients(cards);
        var subject = GroupSubject(cards);
        var rows = cards.Select(card => BuildImportRow(card, viewModel)).ToList();
        var fileName = $"KanbanTasks_{SanitizeFileName(SingleProject(cards) ?? "Tasks")}.xlsx";

        if (OpenClassicOutlookEmail(recipients, subject) is { } opened)
        {
            dynamic mailItem = opened;
            try
            {
                string outlookHtml = mailItem.HTMLBody ?? string.Empty;
                var content = BuildGroupHtmlBody(cards, viewModel);
                if (!HasVisibleContent(outlookHtml) && BuildFallbackSignature(viewModel) is { } signature) content += signature;
                mailItem.HTMLBody = InsertAfterBodyTag(outlookHtml, content);

                var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                try
                {
                    ImportService.SaveTasksFile(tempPath, rows);
                    mailItem.Attachments.Add(tempPath);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                Dialogs.Tell(owner, "Email Tasks",
                    "The email opened, but it could not be filled in completely.\n\nCheck it before sending, and add anything that is missing.",
                    DialogTone.Warning, ex.Message);
            }

            RecordGroupEmailed(cards, recipients, "in Outlook", viewModel);
            return;
        }

        if (!_defaultMailAppNoticeShown)
        {
            _defaultMailAppNoticeShown = true;
            Dialogs.Tell(owner, "Email Tasks",
                "The email will open in your default email app.\n\n" +
                "Classic Outlook is not available on this PC, so files cannot be attached for you. A folder with the tasks' Excel file opens too: drag it into the email.");
        }

        try
        {
            var folder = Path.Combine(AttachmentFoldersRoot, SanitizeFileName(subject));
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            Directory.CreateDirectory(folder);
            ImportService.SaveTasksFile(Path.Combine(folder, fileName), rows);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch
        {
            // The folder is a convenience; the email itself can still go ahead without it.
        }

        var body = BuildGroupPlainTextBody(cards, viewModel, BuildPlainTextSignature(viewModel));
        try
        {
            Process.Start(new ProcessStartInfo(BuildMailtoUri(recipients, subject, body)) { UseShellExecute = true });
            RecordGroupEmailed(cards, recipients, "in your email app", viewModel);
        }
        catch
        {
            try
            {
                Clipboard.SetText($"To: {recipients}\r\nSubject: {subject}\r\n\r\n{body}");
                RecordGroupEmailed(cards, recipients, "copied to the clipboard", viewModel);
            }
            catch
            {
                // Clipboard busy - the message below still tells them what happened.
            }

            Dialogs.Tell(owner, "Email Tasks",
                "The email has been copied to the clipboard.\n\nNo email app is set up to open email links on this PC. Paste it into a new message.",
                DialogTone.Warning);
        }
    }

    // Everyone on any of the tasks who has an address, each once, in the order the tasks name them.
    internal static string GroupRecipients(IEnumerable<CardViewModel> cards) =>
        JoinRecipients(cards.SelectMany(c => c.PeopleEmails).Distinct(StringComparer.OrdinalIgnoreCase));

    internal static string? SingleProject(IReadOnlyList<CardViewModel> cards)
    {
        var projects = cards.Select(c => c.ProjectName).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        return projects.Count == 1 && cards.All(c => c.ProjectName == projects[0]) ? projects[0] : null;
    }

    internal static string GroupSubject(IReadOnlyList<CardViewModel> cards) =>
        SingleProject(cards) is { } project ? $"Tasks: {project} ({cards.Count})" : $"Tasks ({cards.Count})";

    private static void RecordGroupEmailed(IReadOnlyList<CardViewModel> cards, string recipients, string how, MainViewModel viewModel)
    {
        foreach (var card in cards) viewModel.RecordCardEmailed(card, recipients, how, cards.Count);
    }

    private static string ColumnName(CardViewModel card, MainViewModel viewModel) =>
        viewModel.Columns.FirstOrDefault(c => c.Id == card.ColumnId)?.DisplayName ?? string.Empty;

    internal static string BuildGroupHtmlBody(IReadOnlyList<CardViewModel> cards, MainViewModel viewModel)
    {
        const string cell = "padding: 4px 10px 4px 0; border-bottom: 1px solid #ddd; vertical-align: top;";
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family: Segoe UI, sans-serif; font-size: 11pt;\">");
        sb.Append($"<p style=\"font-size: 13pt;\"><b>{WebUtility.HtmlEncode(GroupSubject(cards))}</b></p>");

        sb.Append("<table style=\"border-collapse: collapse;\"><tr style=\"color: #555;\">");
        foreach (var heading in new[] { "Task", "Status", "Due", "Priority", "Assigned to", "Waiting on" })
        {
            sb.Append($"<th style=\"{cell} text-align: left; border-bottom: 2px solid #999;\">{heading}</th>");
        }
        sb.Append("</tr>");

        foreach (var card in cards)
        {
            string Td(string? text, bool bold = false) =>
                $"<td style=\"{cell}\">{(bold ? "<b>" : "")}{WebUtility.HtmlEncode(text ?? string.Empty)}{(bold ? "</b>" : "")}</td>";

            sb.Append("<tr>");
            sb.Append(Td(card.Title, bold: true));
            sb.Append(Td(ColumnName(card, viewModel)));
            sb.Append(Td(card.DueDate.HasValue ? FormatDue(card) : null));
            sb.Append(Td(card.Priority));
            sb.Append(Td(card.People.Count > 0 ? card.WhoName : null));
            sb.Append(Td(card.WaitingOn));
            sb.Append("</tr>");
        }
        sb.Append("</table>");

        var withFiles = cards.Count(c => c.Attachments.Count > 0);
        if (withFiles > 0)
        {
            sb.Append($"<p style=\"color: #777;\">{withFiles} of these tasks {(withFiles == 1 ? "has" : "have")} files attached in Kanban Task Board; " +
                "they are not included here. Email a task on its own to send its files.</p>");
        }

        sb.Append("<p style=\"color: #777;\">Every detail of these tasks - notes, sub-tasks and more - is in the attached Excel file. "
            + "If you use Kanban Task Board, click Import Tasks and select it to add them all to your own board.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    internal static string BuildGroupPlainTextBody(IReadOnlyList<CardViewModel> cards, MainViewModel viewModel, string? signature)
    {
        var sb = new StringBuilder();
        sb.Append(GroupSubject(cards)).Append("\r\n\r\n");
        foreach (var card in cards)
        {
            var parts = new List<string> { ColumnName(card, viewModel) };
            if (card.DueDate.HasValue) parts.Add("due " + FormatDue(card));
            parts.Add(card.Priority);
            if (card.People.Count > 0) parts.Add(card.WhoName);
            if (card.IsWaiting) parts.Add("waiting on " + card.WaitingOn);
            sb.Append("• ").Append(card.Title).Append(" - ").Append(string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)))).Append("\r\n");
        }

        sb.Append("\r\nIf an Excel file is attached, open Kanban Task Board and click Import Tasks to add these tasks to your own board.\r\n");
        if (signature is not null) sb.Append("\r\n").Append(signature);
        return sb.ToString().TrimEnd();
    }
}
