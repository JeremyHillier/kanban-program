using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.Views;

// The task file's custom fields on the Secondary tab: one control for each field that is named and switched on, by its type.
// A field that is switched off is not on screen but its values are kept, and saved back with the task unchanged.
public partial class AddTaskWindow
{
    private sealed record CustomControl(CustomFieldDef Field, FrameworkElement Control);

    private readonly List<CustomControl> _customControls = [];
    private Dictionary<int, string> _hiddenCustomValues = [];

    // Every value the task holds after Save, by slot: what the controls say, plus those of the fields not shown.
    public Dictionary<int, string> SelectedCustomValues { get; private set; } = [];

    private void BuildCustomFields()
    {
        CustomFieldsPanel.Children.Clear();
        _customControls.Clear();
        var fields = _viewModel.ActiveCustomFields;
        CustomFieldsSection.Visibility = fields.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var field in fields)
        {
            CustomFieldsPanel.Children.Add(new TextBlock { Text = field.Name, Style = (Style)FindResource("FieldLabelStyle") });
            var control = NewCustomControl(field);
            CustomFieldsPanel.Children.Add(control);
            _customControls.Add(new CustomControl(field, control));
        }
    }

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

    // Fills the controls from a task's values; those of fields not shown are held to be saved back as they were.
    private void LoadCustomValues(IReadOnlyDictionary<int, string> values)
    {
        var shown = _customControls.Select(c => c.Field.Slot).ToHashSet();
        _hiddenCustomValues = values.Where(p => !shown.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);

        foreach (var (field, control) in _customControls)
        {
            var stored = values.GetValueOrDefault(field.Slot);
            switch (control)
            {
                case CheckBox box:
                    box.IsChecked = !string.IsNullOrEmpty(stored);
                    break;
                case DatePicker picker:
                    picker.SelectedDate = DateTime.TryParseExact(stored, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;
                    break;
                case TextBox box:
                    box.Text = field.Type == CustomFieldType.Number ? CustomFieldRules.Display(CustomFieldType.Number, stored) : stored ?? string.Empty;
                    break;
            }
        }
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

            if (stored is null) values.Remove(item.Field.Slot);
            else values[item.Field.Slot] = stored;
        }
        return true;
    }

    // For the unsaved-changes check: everything the custom controls hold.
    private string CustomSignature() => string.Join("|", _customControls.Select(c => c.Control switch
    {
        CheckBox box => box.IsChecked == true ? "1" : "0",
        DatePicker picker => picker.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-",
        TextBox box => box.Text.Trim(),
        _ => string.Empty
    }));
}
