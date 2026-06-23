using System.Collections.ObjectModel;

namespace Vcs.Desktop.Models;

public enum ProjectVisibility
{
    Public,
    Private
}

public sealed class ProjectModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProjectVisibility Visibility { get; set; }
    public string RepositoryPath { get; set; } = string.Empty;
    public string Language { get; init; } = "C#";
    public bool IsOwnedByCurrentUser { get; init; }
    public DateTime UpdatedAt { get; set; }
    public ObservableCollection<BranchModel> Branches { get; } = [];
    public ObservableCollection<CommitModel> Commits { get; } = [];
    public ObservableCollection<string> Files { get; } = [];

    public string VisibilityLabel => Visibility == ProjectVisibility.Public ? "Public" : "Private";
    public string AccessLabel => IsOwnedByCurrentUser ? "Write access" : "Read only";
}
