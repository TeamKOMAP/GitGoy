using System.Collections.ObjectModel;

namespace Vcs.Desktop.Models;

public sealed class PushModel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string BranchName { get; init; } = string.Empty;
    public string CommitMessage { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public ObservableCollection<string> Files { get; } = [];
    public int FileCount => Files.Count;
    public string Title => string.IsNullOrWhiteSpace(CommitMessage) ? BranchName : CommitMessage;
}
