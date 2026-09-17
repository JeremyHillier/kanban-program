using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Media;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class ColumnViewModel(KanbanColumn model, Brush background) : ObservableObject
{
    public KanbanColumn Model { get; } = model;

    private Brush _background = background;
    public Brush Background
    {
        get => _background;
        set => SetField(ref _background, value);
    }

    public int Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name == value) return;
            Model.Name = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName
    {
        get => Model.DisplayName;
        set
        {
            if (Model.DisplayName == value) return;
            Model.DisplayName = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CardViewModel> Cards { get; } = [];

    // What the board actually lists: Cards minus the ones the current filters hide. The board's
    // card list is virtualized (only cards on screen get built), and a virtualizing panel still
    // builds every collapsed item it passes while filling the screen - so a filter hiding most of
    // a large board would build nearly all of it anyway. Leaving hidden cards out of the list
    // avoids that.
    //
    // Live-filtered on IsVisible, so when one card is edited into or out of the filters just that
    // card is added or removed. Rebuilding the whole list instead would make the column jump while
    // it's scrolled part-way down. Changes to many cards at once go through BeginBulkChange.
    // Created on first use, which is the board binding to it - after Load has filled Cards.
    private ListCollectionView? _cardsView;
    public ListCollectionView CardsView => _cardsView ??= CreateCardsView();

    private ListCollectionView CreateCardsView()
    {
        var view = new ListCollectionView(Cards) { Filter = item => ((CardViewModel)item).IsVisible };
        view.LiveFilteringProperties.Add(nameof(CardViewModel.IsVisible));
        view.IsLiveFiltering = true;
        return view;
    }

    // More cards than this changing at once (a re-sort, or a filter change) is handled as one bulk
    // change rather than card by card.
    public const int BulkChangeThreshold = 50;

    // For changing many cards at once: a re-sort's moves, or a filter change's IsVisible flips. The
    // view processes every single change, which for thousands adds up to seconds, and WPF's
    // DeferRefresh can't help because the board still reads the view in the middle. So the current
    // view is cut loose from Cards for the duration, and when the returned scope ends the board is
    // handed a fresh view, built once from the final state. The column scrolls back to the top,
    // which suits a change that reshuffles or replaces what it shows.
    // Null when nothing has bound to the view yet, since then nothing is listening anyway.
    public IDisposable? BeginBulkChange()
    {
        if (_cardsView is null) return null;

        _cardsView.IsLiveFiltering = false;
        _cardsView.DetachFromSourceCollection();
        _cardsView = null;
        return new BulkChangeScope(this);
    }

    private sealed class BulkChangeScope(ColumnViewModel column) : IDisposable
    {
        public void Dispose() => column.OnPropertyChanged(nameof(CardsView));
    }

    // Manual-sort drag-reorder insertion indicator (see MainWindow.xaml.cs's CardsArea_*
    // handlers) - bound directly rather than named-element lookup, since this DataTemplate repeats
    // once per column.
    private bool _isDropIndicatorVisible;
    public bool IsDropIndicatorVisible
    {
        get => _isDropIndicatorVisible;
        set => SetField(ref _isDropIndicatorVisible, value);
    }

    private double _dropIndicatorY;
    public double DropIndicatorY
    {
        get => _dropIndicatorY;
        set => SetField(ref _dropIndicatorY, value);
    }
}
