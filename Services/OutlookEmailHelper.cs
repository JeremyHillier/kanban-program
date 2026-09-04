using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
    public static void ComposeCardEmail(Window owner, CardViewModel card, string recipientEmail, MainViewModel viewModel)
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

            // Display before setting HTMLBody (rather than after, as a naive version would) so
            // Outlook gets a chance to insert the user's own default "new message" signature the
            // normal way it would for a message composed by hand - reading HTMLBody back afterward
            // captures whatever it inserted (a full HTML document, empty body if no signature is
            // configured). Our own content is then spliced in right after <body...>, ahead of
            // whatever Outlook put there, instead of overwriting it outright.
            mailItem.Display(false);
            string outlookHtml = mailItem.HTMLBody ?? string.Empty;

            var content = BuildHtmlBody(card);
            if (!HasVisibleContent(outlookHtml))
            {
                // No default signature came back from Outlook - fall back to one built from the
                // user's own details in Settings, if any are filled in.
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
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"Couldn't create the email: {ex.Message}", "Email", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
