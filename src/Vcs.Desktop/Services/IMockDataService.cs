using Vcs.Desktop.Models;

namespace Vcs.Desktop.Services;

public interface IMockDataService
{
    UserModel CurrentUser { get; }
    ProjectModel CurrentProject { get; }
    CommitModel CreateCommit(ProjectModel project, BranchModel branch, string message, int changedFiles);
    void Push(ProjectModel project, BranchModel branch);
    string Clone(ProjectModel project, string targetPath);
}
