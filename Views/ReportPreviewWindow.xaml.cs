using System.Windows;
using System.Windows.Documents;

namespace KanbanApp.Views;

public partial class ReportPreviewWindow : Window
{
    public ReportPreviewWindow(FlowDocument document)
    {
        InitializeComponent();
        DocumentViewer.Document = document;
    }
}
