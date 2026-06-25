using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using Vcs.Desktop.Models;

namespace Vcs.Desktop.Services;

public sealed class ApiRepositoryDataService : IRepositoryDataService, IDisposable
{
    private readonly HttpClient _http;

    private ApiRepositoryDataService(HttpClient http, UserModel currentUser, ProjectModel? currentProject)
    {
        _http = http;
        CurrentUser = currentUser;
        CurrentProject = currentProject;
    }

    public UserModel CurrentUser { get; }
    public ProjectModel? CurrentProject { get; private set; }

    public static async Task<ApiRepositoryDataService> CreateAsync(ApiClientOptions options)
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(8)
        };

        http.DefaultRequestHeaders.Add("X-User-Name", options.Username);

        var user = CreateCurrentUser(options.Username);
        var projects = await LoadProjectsAsync(http);
        foreach (var project in projects)
        {
            user.Projects.Add(project);
        }

        var currentProject = user.Projects.FirstOrDefault(project => Directory.Exists(project.LocalPath));

        return new ApiRepositoryDataService(http, user, currentProject);
    }

    public async Task<CommitModel> CreateCommitAsync(
        ProjectModel project,
        BranchModel branch,
        string message,
        string description,
        IReadOnlyCollection<string> changedFiles)
    {
        var changes = changedFiles
            .Select(path => new FileChangeRequest(path, ReadLocalFileContent(project, path), "modify"))
            .ToList();

        var response = await _http.PostAsJsonAsync(
            $"api/projects/{project.Id}/commits",
            new CreateCommitRequest(branch.Name, BuildCommitMessage(message, description), changes));
        response.EnsureSuccessStatusCode();

        await RefreshProjectAsync(project, branch.Name);

        var commit = project.Commits.FirstOrDefault();
        if (commit is not null)
        {
            return commit;
        }

        return new CommitModel
        {
            Message = message.Trim(),
            AuthorName = CurrentUser.UserName,
            CreatedAt = DateTime.Now,
            ChangedFiles = changedFiles.Count
        };
    }

    public async Task PushAsync(ProjectModel project, BranchModel branch)
    {
        await RefreshProjectAsync(project, branch.Name);
    }

    public async Task<ProjectModel> CreateRepositoryAsync(string folderPath, string name, string description)
    {
        var response = await _http.PostAsJsonAsync(
            "api/projects",
            new CreateProjectRequest(name.Trim(), description.Trim(), "private"));
        response.EnsureSuccessStatusCode();

        var createdProject = await response.Content.ReadFromJsonAsync<ProjectResponse>()
            ?? throw new InvalidOperationException("Backend returned an empty project response.");

        var project = await MapProjectAsync(_http, createdProject);
        project.LocalPath = folderPath;
        LoadLocalFiles(project, folderPath);

        CurrentUser.Projects.Insert(0, project);
        CurrentProject = project;
        return project;
    }

    public async Task DeleteRepositoryAsync(ProjectModel project)
    {
        var response = await _http.DeleteAsync($"api/projects/{project.Id}");
        response.EnsureSuccessStatusCode();

        CurrentUser.Projects.Remove(project);
        if (CurrentProject == project)
        {
            CurrentProject = CurrentUser.Projects.FirstOrDefault(item => Directory.Exists(item.LocalPath));
        }
    }

    public async Task DeleteBranchAsync(ProjectModel project, BranchModel branch)
    {
        var response = await _http.DeleteAsync($"api/projects/{project.Id}/branches/{Uri.EscapeDataString(branch.Name)}");
        response.EnsureSuccessStatusCode();

        project.Branches.Remove(branch);
    }

    public async Task SetProjectVisibilityAsync(ProjectModel project, ProjectVisibility visibility)
    {
        var response = await _http.PatchAsJsonAsync(
            $"api/projects/{project.Id}",
            new UpdateProjectRequest(null, null, ToApiVisibility(visibility)));
        response.EnsureSuccessStatusCode();

        var updatedProject = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        project.Visibility = MapVisibility(updatedProject?.Visibility ?? ToApiVisibility(visibility));
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private async Task RefreshProjectAsync(ProjectModel project, string branchName)
    {
        project.Branches.Clear();
        foreach (var branch in await LoadBranchesAsync(_http, project.Id))
        {
            project.Branches.Add(branch);
        }

        project.Files.Clear();
        foreach (var file in await LoadFilesAsync(_http, project.Id, branchName))
        {
            project.Files.Add(file);
        }

        if (Directory.Exists(project.LocalPath))
        {
            LoadLocalFiles(project, project.LocalPath);
        }

        project.Commits.Clear();
        foreach (var commit in await LoadCommitsAsync(_http, project.Id, branchName))
        {
            project.Commits.Add(commit);
        }
    }

    private static UserModel CreateCurrentUser(string username)
    {
        return new UserModel
        {
            UserName = username,
            DisplayName = username,
            Bio = $"{username}@example.local"
        };
    }

    private static async Task<IReadOnlyList<ProjectModel>> LoadProjectsAsync(HttpClient http)
    {
        var projects = await http.GetFromJsonAsync<List<ProjectResponse>>("api/projects/my") ?? [];

        var result = new List<ProjectModel>();
        foreach (var project in projects)
        {
            result.Add(await MapProjectAsync(http, project));
        }

        return result;
    }

    private static async Task<ProjectModel> MapProjectAsync(HttpClient http, ProjectResponse response)
    {
        var project = new ProjectModel
        {
            Id = response.Id,
            Name = response.Name,
            Description = response.Description ?? string.Empty,
            OwnerName = response.OwnerName,
            Visibility = MapVisibility(response.Visibility),
            IsOwnedByCurrentUser = true
        };

        var branches = await LoadBranchesAsync(http, project.Id);
        foreach (var branch in branches)
        {
            project.Branches.Add(branch);
        }

        var defaultBranch = branches.FirstOrDefault(branch => branch.IsDefault)?.Name
            ?? response.DefaultBranch
            ?? "main";

        var files = await LoadFilesAsync(http, project.Id, defaultBranch);
        foreach (var file in files)
        {
            project.Files.Add(file);
        }

        var commits = await LoadCommitsAsync(http, project.Id, defaultBranch);
        foreach (var commit in commits)
        {
            project.Commits.Add(commit);
        }

        if (project.Branches.Count == 0)
        {
            project.Branches.Add(new BranchModel { Name = defaultBranch, IsDefault = true });
        }

        return project;
    }

    private static async Task<List<BranchModel>> LoadBranchesAsync(HttpClient http, Guid projectId)
    {
        var branches = await http.GetFromJsonAsync<List<BranchResponse>>($"api/projects/{projectId}/branches") ?? [];
        return branches
            .Select(branch => new BranchModel { Name = branch.Name, IsDefault = branch.IsDefault })
            .DefaultIfEmpty(new BranchModel { Name = "main", IsDefault = true })
            .ToList();
    }

    private static async Task<List<string>> LoadFilesAsync(HttpClient http, Guid projectId, string branch)
    {
        var files = await http.GetFromJsonAsync<List<FileEntryResponse>>(
            $"api/projects/{projectId}/files?branch={Uri.EscapeDataString(branch)}") ?? [];

        return files
            .Where(file => !file.IsDirectory)
            .Select(file => file.Path)
            .DefaultIfEmpty("README.md")
            .ToList();
    }

    private static async Task<List<CommitModel>> LoadCommitsAsync(HttpClient http, Guid projectId, string branch)
    {
        var commits = await http.GetFromJsonAsync<List<CommitResponse>>(
            $"api/projects/{projectId}/commits?branch={Uri.EscapeDataString(branch)}&take=20") ?? [];

        return commits
            .Select(commit => new CommitModel
            {
                Sha = commit.Sha.Length > 7 ? commit.Sha[..7] : commit.Sha,
                Message = commit.Message,
                AuthorName = commit.Author,
                CreatedAt = commit.When.ToLocalTime(),
                ChangedFiles = 1
            })
            .ToList();
    }

    private static ProjectVisibility MapVisibility(string visibility)
    {
        return string.Equals(visibility, "private", StringComparison.OrdinalIgnoreCase)
            ? ProjectVisibility.Private
            : ProjectVisibility.Public;
    }

    private static string ToApiVisibility(ProjectVisibility visibility)
    {
        return visibility == ProjectVisibility.Private ? "private" : "public";
    }

    private static string BuildCommitMessage(string message, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return message.Trim();
        }

        return $"{message.Trim()}{Environment.NewLine}{Environment.NewLine}{description.Trim()}";
    }

    private static string? ReadLocalFileContent(ProjectModel project, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(project.LocalPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(project.LocalPath, relativePath));
        var rootPath = Path.GetFullPath(project.LocalPath);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private static void LoadLocalFiles(ProjectModel project, string folderPath)
    {
        project.Files.Clear();
        foreach (var file in EnumerateProjectFiles(folderPath))
        {
            project.Files.Add(file);
        }

        if (project.Files.Count == 0)
        {
            project.Files.Add("README.md");
        }
    }

    private static IEnumerable<string> EnumerateProjectFiles(string folderPath)
    {
        var root = Path.GetFullPath(folderPath);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(root, path))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path)
            .Take(200);
    }

    private static bool IsIgnoredPath(string root, string filePath)
    {
        var relativePath = Path.GetRelativePath(root, filePath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part is ".git" or ".vs" or "bin" or "obj" or "node_modules");
    }

    private sealed record CreateProjectRequest(string Name, string? Description, string Visibility);
    private sealed record UpdateProjectRequest(string? Name, string? Description, string? Visibility);
    private sealed record ProjectResponse(Guid Id, string Name, string? Description, string Visibility, string DefaultBranch, string OwnerName, DateTime CreatedAtUtc);
    private sealed record BranchResponse(string Name, bool IsDefault);
    private sealed record CommitResponse(string Sha, string Message, string Author, DateTime When);
    private sealed record FileEntryResponse(string Name, string Path, bool IsDirectory);
    private sealed record FileChangeRequest(string Path, string? Content, string Operation);
    private sealed record CreateCommitRequest(string Branch, string Message, List<FileChangeRequest> Changes);
}
