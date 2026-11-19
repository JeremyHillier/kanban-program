using System.Windows;
using System.Windows.Controls;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The task file's custom fields, from Settings, Board tab: up to ten, each with a name, a type, an Active tick (offered on tasks) and a
// Keep on repeat tick (a repeating task's next occurrence keeps the value). Every change is made straight away (like the other Manage
// screens) and the task screen follows the next time it opens.
public partial class ManageCustomFieldsWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly List<Row> _rows = [];
    private bool _loading;

    private sealed record Row(CustomFieldDef Field, CheckBox Active, TextBox Name, ComboBox Type, CheckBox Keep, TextBlock Uses);

    public ManageCustomFieldsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        Build();
    }

    private static readonly CustomFieldType[] Types = [CustomFieldType.Text, CustomFieldType.Number, CustomFieldType.Date, CustomFieldType.YesNo];

    private void Build()
    {
        _loading = true;
        string[] headings = ["", "Active", "Name", "Type", "Keep on repeat", "Used by"];
        FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

        FieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var c = 0; c < headings.Length; c++)
        {
            var heading = new TextBlock { Text = headings[c], FontWeight = FontWeights.SemiBold, Margin = new Thickness(c == 1 ? 8 : 0, 0, 8, 4), Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush") };
            Grid.SetColumn(heading, c);
            FieldsGrid.Children.Add(heading);
        }

        foreach (var field in _viewModel.CustomFieldDefs)
        {
            FieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var line = FieldsGrid.RowDefinitions.Count - 1;

            var number = new TextBlock { Text = field.Slot.ToString(), VerticalAlignment = VerticalAlignment.Center, Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush") };
            var active = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 14, 0), ToolTip = "Offer this field on tasks" };
            var name = new TextBox
            {
                MaxLength = CustomFieldRules.MaxNameLength, Padding = new Thickness(6), Margin = new Thickness(0, 3, 8, 3),
                Background = (System.Windows.Media.Brush)FindResource("InputBackgroundBrush"), Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("CardBorderBrush"), ToolTip = "The field's name, as the task screen and reports show it. Leave it empty to leave the field unused."
            };
            var type = new ComboBox
            {
                Padding = new Thickness(6), Margin = new Thickness(0, 3, 8, 3),
                Background = (System.Windows.Media.Brush)FindResource("InputBackgroundBrush"), Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("CardBorderBrush")
            };
            foreach (var t in Types) type.Items.Add(new ComboBoxItem { Content = CustomFieldRules.TypeLabel(t), Tag = t });
            var keep = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = "A repeating task's next occurrence keeps this field's value. Untick it to start the field empty each time."
            };
            var uses = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextStrongBrush") };

            Grid.SetRow(number, line); Grid.SetColumn(number, 0);
            Grid.SetRow(active, line); Grid.SetColumn(active, 1);
            Grid.SetRow(name, line); Grid.SetColumn(name, 2);
            Grid.SetRow(type, line); Grid.SetColumn(type, 3);
            Grid.SetRow(keep, line); Grid.SetColumn(keep, 4);
            Grid.SetRow(uses, line); Grid.SetColumn(uses, 5);
            foreach (UIElement element in new UIElement[] { number, active, name, type, keep, uses }) FieldsGrid.Children.Add(element);

            var row = new Row(field, active, name, type, keep, uses);
            _rows.Add(row);
            name.LostFocus += (_, _) => Save(row);
            active.Click += (_, _) => Save(row);
            keep.Click += (_, _) => Save(row);
            type.SelectionChanged += (_, _) => { if (!_loading) Save(row); };
            Show(row);
        }
        _loading = false;
    }

    // Puts the row's controls back to what the field holds now (after a change, or one that was refused).
    private void Show(Row row)
    {
        var wasLoading = _loading;
        _loading = true;
        row.Name.Text = row.Field.Name;
        row.Active.IsChecked = row.Field.IsActive;
        row.Keep.IsChecked = row.Field.KeepOnRepeat;
        row.Type.SelectedIndex = Array.IndexOf(Types, row.Field.Type);
        var used = row.Field.IsDefined ? _viewModel.CustomFieldUseCount(row.Field.Slot) : 0;
        row.Uses.Text = used == 0 ? string.Empty : used == 1 ? "1 task" : $"{used} tasks";
        row.Type.IsEnabled = used == 0;
        row.Type.ToolTip = used == 0 ? null : "Tasks already have a value in this field, so its type can't change.";
        _loading = wasLoading;
    }

    private void Save(Row row)
    {
        if (_loading) return;

        var name = row.Name.Text.Trim();
        var type = row.Type.SelectedItem is ComboBoxItem { Tag: CustomFieldType chosen } ? chosen : row.Field.Type;
        var active = row.Active.IsChecked == true && name.Length > 0; // an unnamed field can't be offered
        var problem = _viewModel.SetCustomField(row.Field.Slot, name, type, active, row.Keep.IsChecked == true);
        if (problem is not null) Dialogs.Tell(this, "Custom Fields", problem);
        Show(row);
    }
}
