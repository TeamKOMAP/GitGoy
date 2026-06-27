namespace Vcs.Desktop.Models;

public sealed class BranchModel
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsCreateOption { get; init; }
}
