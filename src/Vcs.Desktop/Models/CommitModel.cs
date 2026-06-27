namespace Vcs.Desktop.Models;

public sealed class CommitModel
{
    public string Sha { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int ChangedFiles { get; init; }
    public int Additions { get; init; }
    public int Deletions { get; init; }
}
