using Vcs.Desktop.Models;

namespace Vcs.Desktop.Services;

public interface IRepositoryDataService
{
    UserModel CurrentUser { get; }
    ProjectModel CurrentProject { get; }
    CommitModel CreateCommit(ProjectModel project, BranchModel branch, string message, int changedFiles);
    void Push(ProjectModel project, BranchModel branch);
}
