using System.Windows;

namespace KanbanApp.Views;

// Holds a value for bindings that cannot reach it through the element tree cheaply. In the board's resources it holds
// the board's view model ("Board"), so each task card binds to the board's settings with Source={StaticResource Board}
// instead of every binding searching up the tree for the window - dozens of searches per card, which made cards slow
// to build. A Freezable, so in a window's resources it takes the window's DataContext.
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
