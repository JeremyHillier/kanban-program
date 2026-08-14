using System.Windows;
using KanbanApp.Models;

namespace KanbanApp.Views;

public partial class ArchivedTasksWindow : Window
{
    public ArchivedTasksWindow(List<ArchivedCardInfo> archivedCards)
    {
        InitializeComponent();
        ArchivedList.ItemsSource = archivedCards;
        EmptyStateText.Visibility = archivedCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
