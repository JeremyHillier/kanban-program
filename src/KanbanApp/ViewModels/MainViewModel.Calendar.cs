using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// How the Calendar view was last left: Month, Week or Day (Month at first), weekends and finished tasks, the side
// list of tasks with no due date and how it is ordered. The filters are shared with the Gantt view
// (TimelineFilter), so a choice of people, priorities, flags, projects and goals looks the same in both. The day a
// week starts on is a Setting (General tab), so it is in the Settings snapshot.
public partial class MainViewModel
{
    public CalendarScale CalendarView => CalendarLayout.Parse(_db.GetSetting("CalendarView"));

    public void SaveCalendarView(CalendarScale mode) => _db.SetSetting("CalendarView", mode.ToString());

    public bool CalendarShowWeekends => _db.GetFlag("CalendarShowWeekends", true);

    public void SaveCalendarShowWeekends(bool show) => _db.SetFlag("CalendarShowWeekends", show);

    public bool CalendarIncludeDone => _db.GetFlag("CalendarIncludeDone", false);

    public void SaveCalendarIncludeDone(bool include) => _db.SetFlag("CalendarIncludeDone", include);

    public bool CalendarShowNoDueDate => _db.GetFlag("CalendarShowNoDueDate", true);

    public void SaveCalendarShowNoDueDate(bool show) => _db.SetFlag("CalendarShowNoDueDate", show);

    public NoDueOrder CalendarNoDueOrder => Enum.TryParse<NoDueOrder>(_db.GetSetting("CalendarNoDueOrder"), out var order) ? order : NoDueOrder.TitleAscending;

    public void SaveCalendarNoDueOrder(NoDueOrder order) => _db.SetSetting("CalendarNoDueOrder", order.ToString());

    // Monday unless the user chose Sunday.
    public bool WeekStartsOnSunday => _db.GetFlag("WeekStartsOnSunday", false);

    public DayOfWeek FirstDayOfWeek => WeekStartsOnSunday ? DayOfWeek.Sunday : DayOfWeek.Monday;

    public void SetWeekStartsOnSunday(bool sunday) => _db.SetFlag("WeekStartsOnSunday", sunday);
}
