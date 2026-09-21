namespace KanbanApp.ViewModels;

// Thin delegation to each entity's ManagedList (constructed in MainViewModel.cs's constructor)
// for the Manage Projects/Who/Goals/Flags dialogs, plus the active-only views used by filter
// dropdowns and pickers.
public partial class MainViewModel
{
    public IEnumerable<ProjectViewModel> ActiveProjects => Projects.Where(p => p.IsActive);
    public IEnumerable<GoalViewModel> ActiveGoals => Goals.Where(g => g.IsActive);
    public IEnumerable<FlagViewModel> ActiveFlags => Flags.Where(f => f.IsActive);
    public IEnumerable<PersonViewModel> ActivePeople => People.Where(p => p.IsActive);

    public ManagedAddResult<ProjectViewModel> AddProject(string name) => _projectManager.Add(name);
    public bool RenameProject(ProjectViewModel project, string newName) => _projectManager.Rename(project, newName);
    public void DeleteProject(ProjectViewModel project) => _projectManager.Delete(project);
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

    public ManagedAddResult<GoalViewModel> AddGoal(string name) => _goalManager.Add(name);
    public bool RenameGoal(GoalViewModel goal, string newName) => _goalManager.Rename(goal, newName);
    public void DeleteGoal(GoalViewModel goal) => _goalManager.Delete(goal);
    public void SetGoalActive(GoalViewModel goal, bool isActive) => _goalManager.SetActive(goal, isActive);
    public int CountTasksUsingGoal(GoalViewModel goal) => _goalManager.CountUsage(goal);

    public ManagedAddResult<FlagViewModel> AddFlag(string name) => _flagManager.Add(name);
    public bool RenameFlag(FlagViewModel flag, string newName) => _flagManager.Rename(flag, newName);
    public void DeleteFlag(FlagViewModel flag) => _flagManager.Delete(flag);
    public void SetFlagActive(FlagViewModel flag, bool isActive) => _flagManager.SetActive(flag, isActive);
    public int CountTasksUsingFlag(FlagViewModel flag) => _flagManager.CountUsage(flag);
}
