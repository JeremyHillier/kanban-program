using KanbanApp.Services;

namespace KanbanApp.ViewModels;

// How the Calendar view was last left: Month, Week or Day (Month at first), weekends and finished tasks, and the
// side list of tasks with no due date. The filters are shared with the Gantt view (TimelineFilter), so a choice of
// people, priorities, flags, projects and goals looks the same in both.
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
}
