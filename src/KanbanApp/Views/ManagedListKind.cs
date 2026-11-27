using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using KanbanApp.ViewModels;

namespace KanbanApp.Views;

// Everything that differs between Manage Projects, Goals, Flags and Who - the list, its wording and
// its actions. ManageListWindow is the one screen for all four; add a managed list here, not as
// another copy of the window.
internal abstract class ManagedListKind
{
    public required string Title { get; init; }
    public required string Noun { get; init; }          // "project", in messages
    public required string IfDeleted { get; init; }     // what happens to tasks that use one
    public bool NounInDeleteQuestion { get; init; } = true; // "Delete project "X"?" rather than "Delete "X"?"
    public virtual bool HasEmail => false;
    public virtual bool HasTarget => false;
    public virtual bool HasComplete => false;
    public bool HasFields { get; init; }                // projects: the custom fields a new task in one starts with

    // People and projects: the first choice when one that tasks use is deleted, which leaves them without. The
    // other choices move them all to another entry (see DeleteListEntryWindow). Null where the list does not offer it.
    public string? MoveNone { get; init; }
    public string? MoveHint { get; init; }              // under the choice; {0} is the name being deleted
    public bool CanMoveTasks => MoveNone is not null;
    public string DeleteTitle => $"Delete {char.ToUpperInvariant(Noun[0])}{Noun[1..]}";

    public abstract IEnumerable Items { get; }
    public abstract void Add(Window owner, string name);
    public abstract bool Rename(IManagedItem item, string newName);
    public abstract void SetActive(IManagedItem item, bool isActive);
    public abstract int CountUsage(IManagedItem item);
    public abstract void Delete(IManagedItem item, IManagedItem? moveTo = null);
    public abstract IEnumerable<IManagedItem> MoveChoices(IManagedItem item); // where its tasks can go: every other active entry
    public virtual void SetEmail(IManagedItem item, string? email) { }
    public virtual void SetTarget(IManagedItem item, DateTime? target) { }
    public virtual void SetStart(IManagedItem item, DateTime? start) { }
    public virtual void SetComplete(IManagedItem item, bool complete) { }

    public string DeleteQuestion(string name) => NounInDeleteQuestion ? $"Delete {Noun} \"{name}\"?" : $"Delete \"{name}\"?";

    public static ManagedListKind Projects(MainViewModel vm) =>
        new Of<ProjectViewModel>(vm.Projects, vm.AddProject, vm.RenameProject, vm.SetProjectActive, vm.CountTasksUsingProject, vm.DeleteProject, setTarget: vm.SetProjectTarget, setStart: vm.SetProjectStart)
        {
            Title = "Manage Projects", Noun = "project", IfDeleted = "they'll show as having no project.", HasFields = true,
            MoveNone = "No project: leave them without one", MoveHint = "Archived and deleted tasks move too. This cannot be undone."
        };

    public static ManagedListKind Goals(MainViewModel vm) =>
        new Of<GoalViewModel>(vm.Goals, vm.AddGoal, vm.RenameGoal, vm.SetGoalActive, vm.CountTasksUsingGoal, (g, _) => vm.DeleteGoal(g), setTarget: vm.SetGoalTarget, setStart: vm.SetGoalStart, setComplete: vm.SetGoalComplete)
        { Title = "Manage Goals", Noun = "goal", IfDeleted = "they'll show as having no goal." };

    public static ManagedListKind Flags(MainViewModel vm) =>
        new Of<FlagViewModel>(vm.Flags, vm.AddFlag, vm.RenameFlag, vm.SetFlagActive, vm.CountTasksUsingFlag, (f, _) => vm.DeleteFlag(f))
        { Title = "Manage Flags", Noun = "flag", IfDeleted = "they'll lose this flag." };

    // People also carry an email address, used to email a task to them.
    public static ManagedListKind People(MainViewModel vm) =>
        new Of<PersonViewModel>(vm.People, vm.AddPerson, vm.RenamePerson, vm.SetPersonActive, vm.CountTasksUsingPerson, vm.DeletePerson, vm.SetPersonEmail)
        {
            Title = "Manage Who", Noun = "person", IfDeleted = "they'll show as unassigned.", NounInDeleteQuestion = false,
            MoveNone = "Nobody: leave them unassigned",
            MoveHint = "The person you pick takes {0}'s place on each task, as lead where {0} led. Archived and deleted tasks move too. This cannot be undone."
        };

    // One list's actions, typed; the window only ever sees IManagedItem.
    private sealed class Of<T>(
        ObservableCollection<T> items,
        Func<string, ManagedAddResult<T>> add,
        Func<T, string, bool> rename,
        Action<T, bool> setActive,
        Func<T, int> countUsage,
        Action<T, T?> delete,
        Action<T, string?>? setEmail = null,
        Action<T, DateTime?>? setTarget = null,
        Action<T, DateTime?>? setStart = null,
        Action<T, bool>? setComplete = null) : ManagedListKind where T : IManagedItem
    {
        public override bool HasEmail => setEmail is not null;
        public override bool HasTarget => setTarget is not null;
        public override bool HasComplete => setComplete is not null;
        public override IEnumerable Items => items;
        public override void Add(Window owner, string name) => ManagedListPrompts.ShowAddNotice(owner, add(name), Noun, name);
        public override bool Rename(IManagedItem item, string newName) => rename((T)item, newName);
        public override void SetActive(IManagedItem item, bool isActive) => setActive((T)item, isActive);
        public override int CountUsage(IManagedItem item) => countUsage((T)item);
        public override void Delete(IManagedItem item, IManagedItem? moveTo = null) => delete((T)item, moveTo is null ? default : (T)moveTo);
        public override IEnumerable<IManagedItem> MoveChoices(IManagedItem item) => items.Where(i => i.IsActive && !ReferenceEquals(i, item)).Cast<IManagedItem>();
        public override void SetEmail(IManagedItem item, string? email) => setEmail?.Invoke((T)item, email);
        public override void SetTarget(IManagedItem item, DateTime? target) => setTarget?.Invoke((T)item, target);
        public override void SetStart(IManagedItem item, DateTime? start) => setStart?.Invoke((T)item, start);
        public override void SetComplete(IManagedItem item, bool complete) => setComplete?.Invoke((T)item, complete);
    }
}
