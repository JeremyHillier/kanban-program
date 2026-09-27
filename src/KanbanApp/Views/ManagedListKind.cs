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

    public abstract IEnumerable Items { get; }
    public abstract void Add(Window owner, string name);
    public abstract bool Rename(IManagedItem item, string newName);
    public abstract void SetActive(IManagedItem item, bool isActive);
    public abstract int CountUsage(IManagedItem item);
    public abstract void Delete(IManagedItem item);
    public virtual void SetEmail(IManagedItem item, string? email) { }

    public string DeleteQuestion(string name) => NounInDeleteQuestion ? $"Delete {Noun} \"{name}\"?" : $"Delete \"{name}\"?";

    public static ManagedListKind Projects(MainViewModel vm) =>
        new Of<ProjectViewModel>(vm.Projects, vm.AddProject, vm.RenameProject, vm.SetProjectActive, vm.CountTasksUsingProject, vm.DeleteProject)
        { Title = "Manage Projects", Noun = "project", IfDeleted = "they'll show as having no project." };

    public static ManagedListKind Goals(MainViewModel vm) =>
        new Of<GoalViewModel>(vm.Goals, vm.AddGoal, vm.RenameGoal, vm.SetGoalActive, vm.CountTasksUsingGoal, vm.DeleteGoal)
        { Title = "Manage Goals", Noun = "goal", IfDeleted = "they'll show as having no goal." };

    public static ManagedListKind Flags(MainViewModel vm) =>
        new Of<FlagViewModel>(vm.Flags, vm.AddFlag, vm.RenameFlag, vm.SetFlagActive, vm.CountTasksUsingFlag, vm.DeleteFlag)
        { Title = "Manage Flags", Noun = "flag", IfDeleted = "they'll lose this flag." };

    // People also carry an email address, used to email a task to them.
    public static ManagedListKind People(MainViewModel vm) =>
        new Of<PersonViewModel>(vm.People, vm.AddPerson, vm.RenamePerson, vm.SetPersonActive, vm.CountTasksUsingPerson, vm.DeletePerson, vm.SetPersonEmail)
        { Title = "Manage Who", Noun = "person", IfDeleted = "they'll show as unassigned.", NounInDeleteQuestion = false };

    // One list's actions, typed; the window only ever sees IManagedItem.
    private sealed class Of<T>(
        ObservableCollection<T> items,
        Func<string, ManagedAddResult<T>> add,
        Func<T, string, bool> rename,
        Action<T, bool> setActive,
        Func<T, int> countUsage,
        Action<T> delete,
        Action<T, string?>? setEmail = null) : ManagedListKind where T : IManagedItem
    {
        public override bool HasEmail => setEmail is not null;
        public override IEnumerable Items => items;
        public override void Add(Window owner, string name) => ManagedListPrompts.ShowAddNotice(owner, add(name), Noun, name);
        public override bool Rename(IManagedItem item, string newName) => rename((T)item, newName);
        public override void SetActive(IManagedItem item, bool isActive) => setActive((T)item, isActive);
        public override int CountUsage(IManagedItem item) => countUsage((T)item);
        public override void Delete(IManagedItem item) => delete((T)item);
        public override void SetEmail(IManagedItem item, string? email) => setEmail?.Invoke((T)item, email);
    }
}
