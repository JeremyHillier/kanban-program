namespace KanbanApp.ViewModels;

// What a card shows besides its text: the buttons on it, which the user can switch off (the
// Hide Buttons button in the button column, Alt+U), and the lesser details, which a narrow column
// leaves off by itself so what is left still reads. A view choice like Compact Cards, so it is not
// in the Settings snapshot; everything it hides is still on the card's right-click menu.
public partial class MainViewModel
{
    // Narrower than this, a column's cards drop their side buttons and the goal, flags, attachments
    // and Updated lines; it is also about where the card's buttons start to need two rows.
    public const double NarrowColumnWidth = 210;

    public bool IsNarrowColumns => EffectiveColumnWidth < NarrowColumnWidth;

    // The goal, flags, attachments and Updated lines.
    public bool ShowCardExtras => !IsNarrowColumns;

    private bool _showCardButtons = true;

    // The move, Done and Delete buttons along the foot of each card.
    public bool ShowCardButtons
    {
        get => _showCardButtons;
        private set
        {
            if (SetField(ref _showCardButtons, value))
            {
                OnPropertyChanged(nameof(ShowCardSideButtons));
                OnPropertyChanged(nameof(CardButtonsButtonLabel));
            }
        }
    }

    // The flag, email and calendar buttons down a card's right side.
    public bool ShowCardSideButtons => ShowCardButtons && !IsNarrowColumns;

    public string CardButtonsButtonLabel => ShowCardButtons ? "Hide Buttons" : "Show Buttons";

    public void ToggleCardButtons()
    {
        ShowCardButtons = !ShowCardButtons;
        _db.SetFlag("ShowCardButtons", ShowCardButtons);
    }

    private void LoadCardDisplay()
    {
        _showCardButtons = _db.GetFlag("ShowCardButtons", true);
        CardParts = CardPartsShown.Parse(_db.GetSetting(HiddenCardPartsKey));
    }

    // Which details and buttons large cards show (Settings > Large cards show). Unlike Hide Buttons this is a
    // preference set in Settings, so it is in the Settings snapshot and Cancel puts it back.
    private const string HiddenCardPartsKey = "HiddenCardParts";
    private CardPartsShown _cardParts = CardPartsShown.All;

    public CardPartsShown CardParts
    {
        get => _cardParts;
        private set => SetField(ref _cardParts, value);
    }

    // The parts switched off, as saved ("Goal,Updated"); empty when everything shows.
    public string HiddenCardParts => CardParts.Serialize();

    public void SetCardPartShown(CardPart part, bool shown) => SetHiddenCardParts(CardParts.With(part, shown).Serialize());

    public void SetHiddenCardParts(string hidden)
    {
        var parts = CardPartsShown.Parse(hidden);
        if (parts.Serialize() == CardParts.Serialize()) return;
        CardParts = parts;
        _db.SetSetting(HiddenCardPartsKey, parts.Serialize());
    }

    private void NotifyColumnWidthChanged()
    {
        OnPropertyChanged(nameof(EffectiveColumnWidth));
        OnPropertyChanged(nameof(IsNarrowColumns));
        OnPropertyChanged(nameof(ShowCardExtras));
        OnPropertyChanged(nameof(ShowCardSideButtons));
    }
}
