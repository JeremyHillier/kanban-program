using System.IO;
using KanbanApp.Models;

namespace KanbanApp.ViewModels;

public class AttachmentViewModel(CardAttachment model) : ObservableObject
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp"];

    public CardAttachment Model { get; } = model;

    public int Id => Model.Id;
    public string FilePath => Model.FilePath;
    public string DisplayName => Model.DisplayName;
    public DateTime AddedDate => Model.AddedDate;

    public bool IsImage => ImageExtensions.Contains(Path.GetExtension(FilePath).ToLowerInvariant());
}
