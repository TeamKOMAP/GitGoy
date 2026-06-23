namespace Vcs.Desktop.Models;

public sealed class ContributionDayModel
{
    public DateTime Date { get; init; }
    public int CommitCount { get; init; }

    public string Tooltip => $"{Date:dd.MM.yyyy}: {CommitCount} commits";
}
