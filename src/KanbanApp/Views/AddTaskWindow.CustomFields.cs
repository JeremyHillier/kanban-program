using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.Services;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// The task file's custom fields on the Secondary tab. Each task chooses which of the offered fields apply to it: a new task starts with none
// (except those its project lists), the + beside the heading lists the offered fields it does not have yet, and a - takes one off again.
// A field that applies to the task but is no longer offered is not on screen; its value is kept, and saved back unchanged.
public partial class AddTaskWindow
{
    private sealed record CustomControl(CustomFieldDef Field, FrameworkElement Control, FrameworkElement Row);

    private readonly List<CustomControl> _customControls = [];
    private Dictionary<int, string> _hiddenCustomValues = [];

    // Every field that applies to the task after Save, by slot, with its value ("" for a field with nothing in it): what the controls say,
    // plus the fields that apply but are not on screen.
    public Dictionary<int, string> SelectedCustomValues { get; private set; } = [];

    private void InitializeCustomFields()
    {
        ApplyProjectDefaultFields();

        // A new task picking another project also gets that project's fields. An existing task keeps the fields it has.
        ProjectComboBox.SelectionChanged += (_, _) =>
        {
            if (_cardToEdit is not null) return;
            ApplyProjectDefaultFields();
        };
    }

    // Adds the fields the chosen project lists, to those already on the task.
    private void ApplyProjectDefaultFields()
    {
        var projectId = (ProjectComboBox.SelectedItem as ProjectViewModel)?.Id;
        foreach (var field in _viewModel.DefaultFieldsOf(projectId))
        {
            if (_customControls.All(c => c.Field.Slot != field.Slot)) AddCustomFieldRow(field);
        }
        RebuildCustomPanel();
    }

    // The section shows while any field is offered, or the task has one on screen; with none of either it is not there at all.
    private void RefreshCustomSection()
    {
        var offered = _viewModel.ActiveCustomFields;
        CustomFieldsSection.Visibility = offered.Count > 0 || _customControls.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CustomFieldsHint.Visibility = _customControls.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AddCustomFieldButton.IsEnabled = offered.Any(f => _customControls.All(c => c.Field.Slot != f.Slot));
    }

    private void RebuildCustomPanel()
    {
        CustomFieldsPanel.Children.Clear();
        foreach (var item in _customControls) CustomFieldsPanel.Children.Add(item.Row);
        RefreshCustomSection();
    }

    // A field with its label, a - to take it off the task, and the control for its type. Kept in slot order.
    private CustomControl AddCustomFieldRow(CustomFieldDef field)
    {
        var label = new TextBlock { Text = field.Name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("PrimaryTextBrush") };
        var remove = new Button { Content = "−", Style = (Style)FindResource("DeleteLinkButtonStyle"), ToolTip = "Take this field off the task" };
        var head = new StackPanel { Style = (Style)FindResource("FieldLabelRowStyle") };
        head.Children.Add(label);
        head.Children.Add(remove);

        var control = NewCustomControl(field);
        var row = new StackPanel();
        row.Children.Add(head);
        row.Children.Add(control);

        var item = new CustomControl(field, control, row);
        remove.Click += (_, _) =>
        {
            _customControls.Remove(item);
            RebuildCustomPanel();
        };
        _customControls.Add(item);
        _customControls.Sort((a, b) => a.Field.Slot.CompareTo(b.Field.Slot));
        return item;
    }

    private void AddCustomField_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var field in _viewModel.ActiveCustomFields.Where(f => _customControls.All(c => c.Field.Slot != f.Slot)))
        {
            var chosen = field;
            var item = new MenuItem { Header = field.Name.Replace("_", "__") };
            item.Click += (_, _) =>
            {
                var added = AddCustomFieldRow(chosen);
                RebuildCustomPanel();
                added.Control.Focus();
            };
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0) menu.IsOpen = true;
    }

    // Applies a field to the task, as the + menu does (the tests, and anything that opens the window ready-made, use it).
    internal void AddCustomField(CustomFieldDef field)
    {
        if (_customControls.Any(c => c.Field.Slot == field.Slot)) return;
        AddCustomFieldRow(field);
        RebuildCustomPanel();
    }

    internal FrameworkElement? CustomControlOf(int slot) => _customControls.FirstOrDefault(c => c.Field.Slot == slot)?.Control;

    private FrameworkElement NewCustomControl(CustomFieldDef field)
    {
        var input = (Brush)FindResource("InputBackgroundBrush");
        var text = (Brush)FindResource("PrimaryTextBrush");
        var border = (Brush)FindResource("CardBorderBrush");

        switch (field.Type)
        {
            case CustomFieldType.YesNo:
                return new CheckBox { Content = "Yes", Foreground = text, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 0) };
            case CustomFieldType.Date:
                var picker = new DatePicker
                {
                    Padding = new Thickness(4), Width = 170, HorizontalAlignment = HorizontalAlignment.Left,
                    Background = input, Foreground = text, BorderBrush = border,
                    ToolTip = "A date. Delete the text to clear it."
                };
                CalendarWheelSupport.Attach(picker);
                return picker;
            default:
                var box = new TextBox
                {
                    Padding = new Thickness(6), Style = (Style)FindResource("HoverTextBoxStyle"), Background = input, Foreground = text,
                    MaxLength = field.Type == CustomFieldType.Number ? 30 : CustomFieldRules.MaxTextLength
                };
                if (field.Type == CustomFieldType.Number)
                {
                    box.Width = 170;
                    box.HorizontalAlignment = HorizontalAlignment.Left;
                    box.ToolTip = "A number, like 12 or 3.5";
                }
                return box;
        }
    }

    private static void SetControlValue(CustomControl item, string? stored)
    {
        switch (item.Control)
        {
            case CheckBox box:
                box.IsChecked = !string.IsNullOrEmpty(stored);
                break;
            case DatePicker picker:
                picker.SelectedDate = DateTime.TryParseExact(stored, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;
                break;
            case TextBox box:
                box.Text = item.Field.Type == CustomFieldType.Number ? CustomFieldRules.Display(CustomFieldType.Number, stored) : stored ?? string.Empty;
                break;
        }
    }

    // Puts the task's fields on screen, replacing what was there: a row for each that is offered, with its value. The ones that apply but are
    // not offered are held to be saved back as they were.
    private void LoadCustomValues(IReadOnlyDictionary<int, string> values)
    {
        var offered = _viewModel.ActiveCustomFields;
        _customControls.Clear();
        _hiddenCustomValues = [];

        foreach (var (slot, stored) in values.OrderBy(p => p.Key))
        {
            var field = offered.FirstOrDefault(f => f.Slot == slot);
            if (field is null)
            {
                _hiddenCustomValues[slot] = stored;
                continue;
            }
            SetControlValue(AddCustomFieldRow(field), stored);
        }
        RebuildCustomPanel();
    }

    // What the controls hold, ready to save. False, with the control to look at, when a number is not one.
    private bool TryReadCustomValues(out Dictionary<int, string> values, out CustomControl? failed)
    {
        values = new Dictionary<int, string>(_hiddenCustomValues);
        failed = null;

        foreach (var item in _customControls)
        {
            string? stored;
            switch (item.Control)
            {
                case CheckBox box:
                    stored = box.IsChecked == true ? "Yes" : null;
                    break;
                case DatePicker picker:
                    stored = picker.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    break;
                case TextBox box:
                    if (!CustomFieldRules.TryNormalise(item.Field.Type, box.Text, out stored))
                    {
                        failed = item;
                        return false;
                    }
                    break;
                default:
                    stored = null;
                    break;
            }

            values[item.Field.Slot] = stored ?? string.Empty; // the field applies to the task even with nothing in it
        }
        return true;
    }

    // For a template: the fields on screen as they stand, without the checking a save does (a half-typed number is simply left out).
    private Dictionary<int, string> CurrentCustomValuesForTemplate()
    {
        var values = new Dictionary<int, string>();
        foreach (var item in _customControls)
        {
            var stored = item.Control switch
            {
                CheckBox box => box.IsChecked == true ? "Yes" : null,
                DatePicker picker => picker.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TextBox box => CustomFieldRules.TryNormalise(item.Field.Type, box.Text, out var ok) ? ok : null,
                _ => null
            };
            values[item.Field.Slot] = stored ?? string.Empty;
        }
        return values;
    }

    // For the unsaved-changes check: which fields apply, and what they hold.
    private string CustomSignature() => string.Join("|", _customControls.Select(c => c.Field.Slot + ":" + c.Control switch
    {
        CheckBox box => box.IsChecked == true ? "1" : "0",
        DatePicker picker => picker.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-",
        TextBox box => box.Text.Trim(),
        _ => string.Empty
    }));
}
