using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Services;

// Composes an Outlook email for a card via late-bound COM automation (no Microsoft.Office.Interop.Outlook
// reference, so this doesn't require Outlook to be installed on the machine that builds the app - only
// on the machine that runs it). Classic desktop Outlook only, same caveat as OutlookDragDropHelper.
// Always opens a compose window for the user to review (Display), never sends automatically.
public static class OutlookEmailHelper
{
    // recipientEmail is passed explicitly rather than always reading card.WhoEmail: the Add/Edit
    // Task dialog needs to email the currently-selected Who in its combo box, which can be a live,
    // not-yet-saved change that hasn't made it onto the CardViewModel yet.
    public static void ComposeCardEmail(Window owner, CardViewModel card, string recipientEmail)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail)) return;

        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType is null)
            {
                MessageBox.Show(owner, "Outlook doesn't appear to be installed on this machine.", "Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Outlook enforces single-instance at the OS level, so CreateInstance attaches to an
            // already-running instance rather than launching a duplicate - no need to separately
            // check the Running Object Table first (Marshal.GetActiveObject isn't available on
            // modern .NET anyway; it was never ported from .NET Framework).
            dynamic app = Activator.CreateInstance(outlookType)!;
            dynamic mailItem = app.CreateItem(0); // olMailItem
            mailItem.To = recipientEmail;
            mailItem.Subject = $"Task: {card.Title}";
            mailItem.HTMLBody = BuildHtmlBody(card);

            foreach (var attachment in card.Attachments)
            {
                if (File.Exists(attachment.FilePath))
                {
                    mailItem.Attachments.Add(attachment.FilePath);
                }
            }

            mailItem.Display(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"Couldn't create the email: {ex.Message}", "Email", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string BuildHtmlBody(CardViewModel card)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family: Segoe UI, sans-serif; font-size: 11pt;\">");
        sb.Append($"<p style=\"font-size: 13pt;\"><b>{WebUtility.HtmlEncode(card.Title)}</b></p>");

        sb.Append("<table style=\"border-collapse: collapse;\">");
        AppendRow(sb, "Project", card.ProjectName);
        AppendRow(sb, "Priority", card.Priority);
        if (card.DueDate.HasValue) AppendRow(sb, "Due", card.DueDate.Value.ToString("dd-MMM-yyyy"));
        if (!string.IsNullOrWhiteSpace(card.GoalName) && card.GoalName != "No Goal") AppendRow(sb, "Goal", card.GoalName);
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

        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string value)
    {
        sb.Append($"<tr><td style=\"padding: 2px 12px 2px 0; color: #555;\"><b>{WebUtility.HtmlEncode(label)}</b></td>" +
                  $"<td style=\"padding: 2px 0;\">{WebUtility.HtmlEncode(value)}</td></tr>");
    }
}
