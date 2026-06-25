using System.IO;
using System.Linq;
using Vcs.Desktop.Models;

namespace Vcs.Desktop.Services;

public sealed class MockDataService : IRepositoryDataService
{
    public MockDataService()
    {
        CurrentUser = CreateCurrentUser();
        CurrentProject = CurrentUser.Projects.FirstOrDefault(project => Directory.Exists(project.LocalPath));
    }

    public UserModel CurrentUser { get; }
    public ProjectModel? CurrentProject { get; private set; }

    public Task<CommitModel> CreateCommitAsync(
        ProjectModel project,
        BranchModel branch,
        string message,
        string description,
        IReadOnlyCollection<string> changedFiles)
    {
        var commit = new CommitModel
        {
            Sha = Guid.NewGuid().ToString("N")[..7],
            Message = message.Trim(),
            AuthorName = CurrentUser.UserName,
            CreatedAt = DateTime.Now,
            ChangedFiles = changedFiles.Count,
            Additions = changedFiles.Count * 24 + 2,
            Deletions = Math.Max(1, changedFiles.Count * 4)
        };

        project.Commits.Insert(0, commit);
        return Task.FromResult(commit);
    }

    public Task PushAsync(ProjectModel project, BranchModel branch)
    {
        return Task.CompletedTask;
    }

    public Task<ProjectModel> CreateRepositoryAsync(string folderPath, string name, string description)
    {
        var project = CreateProject(name.Trim(), description.Trim(), true, ProjectVisibility.Private, CurrentUser.UserName);
        project.LocalPath = folderPath;
        project.Files.Clear();

        foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
                     .Select(path => Path.GetRelativePath(folderPath, path).Replace('\\', '/'))
                     .OrderBy(path => path)
                     .Take(200))
        {
            project.Files.Add(file);
        }

        CurrentUser.Projects.Insert(0, project);
        CurrentProject = project;
        return Task.FromResult(project);
    }

    public Task DeleteRepositoryAsync(ProjectModel project)
    {
        CurrentUser.Projects.Remove(project);
        if (CurrentProject == project)
        {
            CurrentProject = CurrentUser.Projects.FirstOrDefault(item => Directory.Exists(item.LocalPath));
        }

        return Task.CompletedTask;
    }

    public Task DeleteBranchAsync(ProjectModel project, BranchModel branch)
    {
        project.Branches.Remove(branch);
        return Task.CompletedTask;
    }

    public Task SetProjectVisibilityAsync(ProjectModel project, ProjectVisibility visibility)
    {
        project.Visibility = visibility;
        return Task.CompletedTask;
    }

    private static UserModel CreateCurrentUser()
    {
        var user = new UserModel
        {
            UserName = "offlayt",
            DisplayName = "0_offlayt",
            Bio = "Version control client prototype"
        };

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
