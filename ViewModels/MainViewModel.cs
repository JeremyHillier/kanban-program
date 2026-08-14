using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Media;
using KanbanApp.Models;
using KanbanApp.Services;

namespace KanbanApp.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    public string AppVersion { get; } = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";

    private bool _isDarkMode;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set
        {
            if (SetField(ref _isDarkMode, value))
            {
                OnPropertyChanged(nameof(ThemeButtonLabel));
            }
        }
    }

    public string ThemeButtonLabel => IsDarkMode ? "Light Mode" : "Dark Mode";

    private static readonly Brush[] ColumnPalette =
    [
        new SolidColorBrush(Color.FromRgb(0xE3, 0xE8, 0xEF)), // To Do - blue-gray
        new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xCD)), // In Progress - yellow
        new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)), // On Hold - orange
        new SolidColorBrush(Color.FromRgb(0xE1, 0xD5, 0xF5)), // Waiting - purple
        new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA)), // Done - green
    ];

    public ObservableCollection<ColumnViewModel> Columns { get; } = [];
    public ObservableCollection<ProjectViewModel> Projects { get; } = [];

    public RelayCommand DeleteCardCommand { get; }
    public RelayCommand MoveCardCommand { get; }

    public MainViewModel(DatabaseService db)
    {
        _db = db;

        DeleteCardCommand = new RelayCommand(param => DeleteCard(param as CardViewModel));
        MoveCardCommand = new RelayCommand(param =>
        {
            if (param is (CardViewModel card, ColumnViewModel targetColumn))
            {
                MoveCard(card, targetColumn);
            }
        });

        Load();

        _isDarkMode = _db.GetSetting("Theme") == "Dark";
        Theming.ThemeManager.Apply(_isDarkMode);
    }

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        _db.SetSetting("Theme", IsDarkMode ? "Dark" : "Light");
        Theming.ThemeManager.Apply(IsDarkMode);
    }

    private void Load()
    {
        Columns.Clear();
        Projects.Clear();

        foreach (var project in _db.GetProjects())
        {
            Projects.Add(new ProjectViewModel(project));
        }

        var columns = _db.GetColumns();
        var cards = _db.GetCards();

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var background = ColumnPalette[i % ColumnPalette.Length];
            var columnVm = new ColumnViewModel(column, background);
            foreach (var card in cards.Where(c => c.ColumnId == column.Id))
            {
                var cardVm = new CardViewModel(card)
                {
                    ProjectName = ResolveProjectName(card.ProjectId),
                    LastUpdated = card.LastUpdated
                };
                columnVm.Cards.Add(cardVm);
            }
            Columns.Add(columnVm);
        }
    }

    private string ResolveProjectName(int? projectId)
    {
        if (projectId is null) return "No Project";
        return Projects.FirstOrDefault(p => p.Id == projectId)?.Name ?? "No Project";
    }

    public void AddCard(string title, ColumnViewModel column, ProjectViewModel? project, string priority, DateTime? dueDate, string? who)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        var card = _db.AddCard(column.Id, title.Trim(), project?.Id, column.Name, priority, dueDate, who);
        var cardVm = new CardViewModel(card) { ProjectName = project?.Name ?? "No Project", LastUpdated = card.LastUpdated };
        column.Cards.Add(cardVm);
    }

    public void EditCard(CardViewModel card, string title, ColumnViewModel newColumn, ProjectViewModel? project, string priority, DateTime? dueDate, string? who)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        card.Title = title.Trim();
        card.ProjectId = project?.Id;
        card.ProjectName = project?.Name ?? "No Project";
        card.Priority = priority;
        card.DueDate = dueDate;
        card.Who = who;

        card.LastUpdated = _db.UpdateCard(card.Id, card.Title, project?.Id, priority, dueDate, who);

        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is not null && sourceColumn != newColumn)
        {
            MoveCard(card, newColumn);
        }
    }

    public void AddProject(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var project = _db.AddProject(name.Trim());
        Projects.Add(new ProjectViewModel(project));
    }

    public void RenameProject(ProjectViewModel project, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || project.Name == newName.Trim()) return;

        project.Name = newName.Trim();
        _db.RenameProject(project.Id, project.Name);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.ProjectId == project.Id))
        {
            card.ProjectName = project.Name;
        }
    }

    public void DeleteProject(ProjectViewModel project)
    {
        _db.DeleteProject(project.Id);
        Projects.Remove(project);

        foreach (var card in Columns.SelectMany(c => c.Cards).Where(c => c.ProjectId == project.Id))
        {
            card.ProjectId = null;
            card.ProjectName = "No Project";
        }
    }

    private void DeleteCard(CardViewModel? card)
    {
        if (card is null) return;

        var column = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        column?.Cards.Remove(card);
        _db.DeleteCard(card.Id, card.Title, column?.Name ?? "Unknown");
    }

    private void MoveCard(CardViewModel card, ColumnViewModel targetColumn)
    {
        var sourceColumn = Columns.FirstOrDefault(c => c.Cards.Contains(card));
        if (sourceColumn is null || sourceColumn == targetColumn) return;

        sourceColumn.Cards.Remove(card);
        card.ColumnId = targetColumn.Id;
        targetColumn.Cards.Add(card);

        card.LastUpdated = _db.MoveCard(card.Id, targetColumn.Id, card.Title, sourceColumn.Name, targetColumn.Name);
    }

    public void ArchiveDoneTasks()
    {
        var doneColumn = Columns.FirstOrDefault(c => c.Name == "Done");
        if (doneColumn is null) return;

        foreach (var card in doneColumn.Cards.ToList())
        {
            _db.ArchiveCard(card.Id, card.Title, doneColumn.Name);
            doneColumn.Cards.Remove(card);
        }
    }

    public List<ArchivedCardInfo> GetArchivedCards() => _db.GetArchivedCards();
}
