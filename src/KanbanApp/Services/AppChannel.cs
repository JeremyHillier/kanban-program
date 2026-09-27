namespace KanbanApp.Services;

public static class AppChannel
{
#if TEST_CHANNEL
    public const bool IsTest = true;
    public const string Name = "Test";
    public const string DataFolderName = "KanbanApp.Test";
    public const string DisplayName = "Kanban Task Board (Test)";
#else
    public const bool IsTest = false;
    public const string Name = "Production";
    public const string DataFolderName = "KanbanApp";
    public const string DisplayName = "Kanban Task Board";
#endif
}
