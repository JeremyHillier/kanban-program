namespace KanbanApp.Models;

// A task's priority is stored as its name. These are the four, highest first, in the order every menu,
// list and sort uses.
public static class Priorities
{
    public static readonly string[] All = ["High", "Medium", "Normal", "Low"];

    // For sorting, highest first. Anything unrecognised sorts with Normal.
    public static int Rank(string priority) => priority switch
    {
        "High" => 0,
        "Medium" => 1,
        "Normal" => 2,
        "Low" => 3,
        _ => 2
    };
}
