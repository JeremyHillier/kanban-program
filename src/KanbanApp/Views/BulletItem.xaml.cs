using System.Windows;
using System.Windows.Controls;

namespace KanbanApp.Views;

public partial class BulletItem : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(BulletItem), new PropertyMetadata(string.Empty));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BulletItem()
    {
        InitializeComponent();
    }
}
