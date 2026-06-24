using Vcs.Desktop.Models;

namespace Vcs.Desktop.Services;

public sealed class MockDataService : IRepositoryDataService
{
    public MockDataService()
    {
        CurrentUser = CreateCurrentUser();
        CurrentProject = CurrentUser.Projects[0];
    }

    public UserModel CurrentUser { get; }
    public ProjectModel CurrentProject { get; }

    public CommitModel CreateCommit(ProjectModel project, BranchModel branch, string message, int changedFiles)
    {
        var commit = new CommitModel
        {
            Sha = Guid.NewGuid().ToString("N")[..7],
            Message = message.Trim(),
            AuthorName = CurrentUser.UserName,
            CreatedAt = DateTime.Now,
            ChangedFiles = changedFiles,
            Additions = changedFiles * 24 + 2,
            Deletions = Math.Max(1, changedFiles * 4)
        };

        project.Commits.Insert(0, commit);
        return commit;
    }

    public void Push(ProjectModel project, BranchModel branch)
    {
    }

    private static UserModel CreateCurrentUser()
    {
        var user = new UserModel
        {
            UserName = "offlayt",
            DisplayName = "0_offlayt",
            Bio = "Version control client prototype"
        };

        user.Projects.Add(CreateProject("vcs-desktop", "Desktop client for a lightweight Git hosting system.", true, ProjectVisibility.Private));
        user.Projects.Add(CreateProject("git-api-contracts", "DTO sketches and API contracts for repository operations.", true, ProjectVisibility.Public));
        user.Projects.Add(CreateProject("commit-heatmap", "Contribution graph prototype for profile statistics.", true, ProjectVisibility.Public));

        return user;
    }

    private static ProjectModel CreateProject(
        string name,
        string description,
        bool owned,
        ProjectVisibility visibility,
        string owner = "offlayt")
    {
        var project = new ProjectModel
        {
            Name = name,
            Description = description,
            OwnerName = owner,
            Visibility = visibility,
            IsOwnedByCurrentUser = owned
        };

        project.Branches.Add(new BranchModel { Name = "main", IsDefault = true });
        project.Branches.Add(new BranchModel { Name = "feature/wpf-shell" });
        project.Branches.Add(new BranchModel { Name = "fix/commit-panel" });

        project.Files.Add("README.md");
        project.Files.Add("src/App.xaml");
        project.Files.Add("src/MainWindow.xaml");
        project.Files.Add("src/ViewModels/ProjectViewModel.cs");
        project.Files.Add("docs/TZ_VCS_System.pdf");

        project.Commits.Add(new CommitModel
        {
            Sha = "a13f91c",
            Message = "Build repository workspace shell",
            AuthorName = owner,
            CreatedAt = DateTime.Now.AddHours(-3),
            ChangedFiles = 8,
            Additions = 214,
            Deletions = 31
        });
        project.Commits.Add(new CommitModel
        {
            Sha = "91de0af",
            Message = "Add branch selector and commit history",
            AuthorName = owner,
            CreatedAt = DateTime.Now.AddDays(-1),
            ChangedFiles = 5,
            Additions = 128,
            Deletions = 18
        });
        project.Commits.Add(new CommitModel
        {
            Sha = "04bc772",
            Message = "Initialize project metadata",
            AuthorName = owner,
            CreatedAt = DateTime.Now.AddDays(-4),
            ChangedFiles = 11,
            Additions = 402,
            Deletions = 0
        });

        return project;
    }
}
