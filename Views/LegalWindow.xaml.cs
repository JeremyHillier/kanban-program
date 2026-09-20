using System.Windows;

namespace KanbanApp.Views;

// Shows the licence agreement or the privacy note (Services/LegalDocuments) from the About screen.
public partial class LegalWindow : Window
{
    public LegalWindow(string title, string documentText)
    {
        InitializeComponent();
        Title = title;
        DocumentText.Text = documentText;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(DocumentText.Text);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another program has the clipboard open; the text can still be selected and copied by hand.
        }
    }
}
