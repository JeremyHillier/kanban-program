namespace KanbanApp.ViewModels;

// Thin delegation to each entity's ManagedList (constructed in MainViewModel.cs's constructor)
// for the Manage screen (Views/ManageListWindow, one window for all four lists - see
// Views/ManagedListKind), plus the active-only views used by filter
// dropdowns and pickers.
public partial class MainViewModel
{
    public IEnumerable<ProjectViewModel> ActiveProjects => Projects.Where(p => p.IsActive);
    public IEnumerable<GoalViewModel> ActiveGoals => Goals.Where(g => g.IsOffered);
    public IEnumerable<FlagViewModel> ActiveFlags => Flags.Where(f => f.IsActive);
    public IEnumerable<PersonViewModel> ActivePeople => People.Where(p => p.IsActive);

    public ManagedAddResult<ProjectViewModel> AddProject(string name) => _projectManager.Add(name);
    public bool RenameProject(ProjectViewModel project, string newName) => _projectManager.Rename(project, newName);
    public void DeleteProject(ProjectViewModel project)
    {
        _projectManager.Delete(project);
        _projectMilestones?.RemoveAll(m => m.OwnerId == project.Id);
    }
    public void SetProjectActive(ProjectViewModel project, bool isActive) => _projectManager.SetActive(project, isActive);
    public int CountTasksUsingProject(ProjectViewModel project) => _projectManager.CountUsage(project);

    public ManagedAddResult<PersonViewModel> AddPerson(string name) => _personManager.Add(name);
    public bool RenamePerson(PersonViewModel person, string newName) => _personManager.Rename(person, newName);
    public void DeletePerson(PersonViewModel person) => _personManager.Delete(person);
    public void SetPersonActive(PersonViewModel person, bool isActive) => _personManager.SetActive(person, isActive);
    public int CountTasksUsingPerson(PersonViewModel person) => _personManager.CountUsage(person);

    public void SetPersonEmail(PersonViewModel person, string? email)
    {
        var trimmed = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (person.Email == trimmed) return;

        person.Email = trimmed;
        _db.SetPersonEmail(person.Id, trimmed);
        UpdateMatchingCards(c => c.IsAssignedTo(person.Id), c => c.RefreshPeople());
    }

    public void SetProjectTarget(ProjectViewModel project, DateTime? target)
    {
        var day = target?.Date;
        if (project.TargetDate == day) return;

        project.TargetDate = day;
        _db.SetProjectTarget(project.Id, day);
    }

    public void SetProjectStart(ProjectViewModel project, DateTime? start)
    {
        var day = start?.Date;
        if (project.StartDate == day) return;

        project.StartDate = day;
        _db.SetProjectStart(project.Id, day);
    }

    public void SetGoalStart(GoalViewModel goal, DateTime? start)
    {
        var day = start?.Date;
        if (goal.StartDate == day) return;

        goal.StartDate = day;
        _db.SetGoalStart(goal.Id, day);
    }

    // Marks a goal finished today, or opens it again. A complete goal drops out of the pickers and the Goal filter
    // (its tasks keep it) and the Timeline hides it unless Include Done tasks is ticked.
    public void SetGoalComplete(GoalViewModel goal, bool complete)
    {
        var day = complete ? DateTime.Today : (DateTime?)null;
        if (goal.CompletedDate == day || complete && goal.IsComplete) return;

        goal.CompletedDate = day;
        _db.SetGoalCompleted(goal.Id, day);
        RefreshGoalFilterOptions();
        OnPropertyChanged(nameof(ActiveGoals));
    }

    // The tasks still to do under a goal - what is left when it is marked complete.
    public List<CardViewModel> OpenTasksInGoal(GoalViewModel goal) =>
        Columns.Where(c => c.Name != "Done").SelectMany(c => c.Cards).Where(c => c.GoalId == goal.Id).ToList();

    public void SetGoalTarget(GoalViewModel goal, DateTime? target)
    {
        var day = target?.Date;
        if (goal.TargetDate == day) return;

        goal.TargetDate = day;
        _db.SetGoalTarget(goal.Id, day);
    }

    public ManagedAddResult<GoalViewModel> AddGoal(string name) => _goalManager.Add(name);
    public bool RenameGoal(GoalViewModel goal, string newName) => _goalManager.Rename(goal, newName);
    public void DeleteGoal(GoalViewModel goal)
    {
        _goalManager.Delete(goal);
        _goalMilestones?.RemoveAll(m => m.OwnerId == goal.Id); // the file deleted them with the goal
    }
    public void SetGoalActive(GoalViewModel goal, bool isActive) => _goalManager.SetActive(goal, isActive);
    public int CountTasksUsingGoal(GoalViewModel goal) => _goalManager.CountUsage(goal);

    public ManagedAddResult<FlagViewModel> AddFlag(string name) => _flagManager.Add(name);
    public bool RenameFlag(FlagViewModel flag, string newName) => _flagManager.Rename(flag, newName);
    public void DeleteFlag(FlagViewModel flag) => _flagManager.Delete(flag);
    public void SetFlagActive(FlagViewModel flag, bool isActive) => _flagManager.SetActive(flag, isActive);
    public int CountTasksUsingFlag(FlagViewModel flag) => _flagManager.CountUsage(flag);
}
