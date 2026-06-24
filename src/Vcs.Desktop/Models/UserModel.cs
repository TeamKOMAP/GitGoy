using System.Collections.ObjectModel;

namespace Vcs.Desktop.Models;

public sealed class UserModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Bio { get; init; } = string.Empty;
    public ObservableCollection<ProjectModel> Projects { get; } = [];
}
