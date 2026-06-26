using Vcs.Desktop.Models;

namespace Vcs.Desktop.Services;

public interface IRepositoryDataService
{
    UserModel CurrentUser { get; }
    ProjectModel? CurrentProject { get; }
    Task<CommitModel> CreateCommitAsync(
        ProjectModel project,
        BranchModel branch,
        string message,
        string description,
        IReadOnlyCollection<string> changedFiles);

    Task<ProjectModel> CreateRepositoryAsync(string folderPath, string name, string description);
    Task DeleteRepositoryAsync(ProjectModel project);
    Task RefreshChangedFilesAsync(ProjectModel project);
    Task<BranchModel> CreateBranchAsync(ProjectModel project, BranchModel sourceBranch, string branchName);
    Task RenameRepositoryAsync(ProjectModel project, string newName);
    Task<BranchModel> RenameBranchAsync(ProjectModel project, BranchModel branch, string newName);
    Task DeleteBranchAsync(ProjectModel project, BranchModel branch);
    Task PushAsync(ProjectModel project, BranchModel branch);
    Task SetProjectVisibilityAsync(ProjectModel project, ProjectVisibility visibility);
}
