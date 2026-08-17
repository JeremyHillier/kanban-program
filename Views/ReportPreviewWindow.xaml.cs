using System.Windows;
using System.Windows.Documents;

namespace KanbanApp.Views;

public partial class ReportPreviewWindow : Window
{
    public ReportPreviewWindow(FixedDocument document)
    {
        InitializeComponent();
        Viewer.Document = document;
    }
}
