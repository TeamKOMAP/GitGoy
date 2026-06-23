using System.Collections.ObjectModel;

namespace Vcs.Desktop.Models;

public sealed class UserModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Bio { get; init; } = string.Empty;
    public string AvatarInitials { get; init; } = string.Empty;
    public int FollowersCount { get; init; }
    public int FollowingCount { get; init; }
    public ObservableCollection<ProjectModel> Projects { get; } = [];
    public ObservableCollection<ContributionDayModel> Contributions { get; } = [];
}
