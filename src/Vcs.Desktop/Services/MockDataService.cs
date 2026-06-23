using Vcs.Desktop.Models;
using System.IO;

namespace Vcs.Desktop.Services;

public sealed class MockDataService : IMockDataService
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
        project.UpdatedAt = DateTime.Now;
        return commit;
    }

    public void Push(ProjectModel project, BranchModel branch)
    {
        project.UpdatedAt = DateTime.Now;
    }

    public string Clone(ProjectModel project, string targetPath)
    {
        return Path.Combine(targetPath, project.Name);
    }

    private static UserModel CreateCurrentUser()
    {
        var user = new UserModel
        {
            UserName = "offlayt",
            DisplayName = "0_offlayt",
            Bio = "Version control client prototype",
            AvatarInitials = "OF",
            FollowersCount = 28,
            FollowingCount = 12
        };

        foreach (var day in Enumerable.Range(0, 91).Select(i => DateTime.Today.AddDays(-90 + i)))
        {
            user.Contributions.Add(new ContributionDayModel
            {
                Date = day,
                CommitCount = Math.Abs((day.Day * 7 + day.Month * 3) % 9 - 3)
            });
        }

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
            RepositoryPath = $@"C:\Repos\{name}",
            IsOwnedByCurrentUser = owned,
            UpdatedAt = DateTime.Now.AddHours(-name.Length)
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
