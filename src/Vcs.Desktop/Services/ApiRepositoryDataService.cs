using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Vcs.Desktop.Models;

namespace Vcs.Desktop.Services;

public sealed class ApiRepositoryDataService : IRepositoryDataService, IDisposable
{
    private readonly HttpClient _http;
    private readonly LocalRepositoryStore _localRepositoryStore = new();

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
        var localRepositoryStore = new LocalRepositoryStore();
        var localRepositories = localRepositoryStore.Load();
        foreach (var project in projects)
        {
            if (localRepositories.TryGetValue(project.Id, out var localPath))
            {
                project.LocalPath = localPath;
                EnsureLocalSnapshot(project, localRepositoryStore);
                RefreshChangedFiles(project, localRepositoryStore);
            }

            user.Projects.Add(project);
        }

        var currentProject = user.Projects.FirstOrDefault(project => Directory.Exists(project.LocalPath))
            ?? user.Projects.FirstOrDefault();

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

        SaveCommittedSnapshot(project, changedFiles);
        await RefreshProjectAsync(project, branch.Name);
        RefreshChangedFiles(project, _localRepositoryStore);

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
        RefreshChangedFiles(project, _localRepositoryStore);
    }

    public Task RefreshChangedFilesAsync(ProjectModel project)
    {
        EnsureLocalSnapshot(project, _localRepositoryStore);
        RefreshChangedFiles(project, _localRepositoryStore);
        return Task.CompletedTask;
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
        _localRepositoryStore.Save(project);
        _localRepositoryStore.SaveSnapshot(project, CaptureLocalSnapshot(project.LocalPath));
        RefreshChangedFiles(project, _localRepositoryStore);

        CurrentUser.Projects.Insert(0, project);
        CurrentProject = project;
        return project;
    }

    public async Task DeleteRepositoryAsync(ProjectModel project)
    {
        var response = await _http.DeleteAsync($"api/projects/{project.Id}");
        response.EnsureSuccessStatusCode();

        CurrentUser.Projects.Remove(project);
        _localRepositoryStore.Remove(project);
        if (CurrentProject == project)
        {
            CurrentProject = CurrentUser.Projects.FirstOrDefault(item => Directory.Exists(item.LocalPath))
                ?? CurrentUser.Projects.FirstOrDefault();
        }
    }

    public async Task<BranchModel> CreateBranchAsync(ProjectModel project, BranchModel sourceBranch, string branchName)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/projects/{project.Id}/branches",
            new CreateBranchRequest(branchName.Trim(), sourceBranch.Name));
        response.EnsureSuccessStatusCode();

        await RefreshProjectAsync(project, branchName.Trim());
        RefreshChangedFiles(project, _localRepositoryStore);
        return project.Branches.First(branch => string.Equals(branch.Name, branchName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task RenameRepositoryAsync(ProjectModel project, string newName)
    {
        var response = await _http.PatchAsJsonAsync(
            $"api/projects/{project.Id}",
            new UpdateProjectRequest(newName.Trim(), null, null));
        response.EnsureSuccessStatusCode();

        var updatedProject = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        project.Name = updatedProject?.Name ?? newName.Trim();
    }

    public async Task<BranchModel> RenameBranchAsync(ProjectModel project, BranchModel branch, string newName)
    {
        var response = await _http.PatchAsJsonAsync(
            $"api/projects/{project.Id}/branches/{Uri.EscapeDataString(branch.Name)}",
            new RenameBranchRequest(newName.Trim()));
        response.EnsureSuccessStatusCode();

        await RefreshProjectAsync(project, newName.Trim());
        RefreshChangedFiles(project, _localRepositoryStore);
        return project.Branches.First(item => string.Equals(item.Name, newName.Trim(), StringComparison.OrdinalIgnoreCase));
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
            .ToList();
    }

    private static async Task<List<CommitModel>> LoadCommitsAsync(HttpClient http, Guid projectId, string branch)
    {
        var commits = await http.GetFromJsonAsync<List<CommitResponse>>(
            $"api/projects/{projectId}/commits?branch={Uri.EscapeDataString(branch)}&take=20") ?? [];

        return commits
            .Where(commit => !IsGeneratedInitialCommit(commit))
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

    private static bool IsGeneratedInitialCommit(CommitResponse commit)
    {
        return string.Equals(commit.Author, "system", StringComparison.OrdinalIgnoreCase)
            && string.Equals(commit.Message, "Initial commit", StringComparison.Ordinal);
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

    private static void EnsureLocalSnapshot(ProjectModel project, LocalRepositoryStore localRepositoryStore)
    {
        if (!Directory.Exists(project.LocalPath))
        {
            return;
        }

        if (localRepositoryStore.LoadSnapshot(project.Id).Count == 0)
        {
            localRepositoryStore.SaveSnapshot(project, CaptureLocalSnapshot(project.LocalPath));
        }
    }

    private static void RefreshChangedFiles(ProjectModel project, LocalRepositoryStore localRepositoryStore)
    {
        project.ChangedFiles.Clear();
        if (!Directory.Exists(project.LocalPath))
        {
            return;
        }

        var snapshot = localRepositoryStore.LoadSnapshot(project.Id);
        var current = CaptureLocalSnapshot(project.LocalPath);
        foreach (var file in current)
        {
            if (!snapshot.TryGetValue(file.Key, out var previousHash)
                || !string.Equals(previousHash, file.Value, StringComparison.OrdinalIgnoreCase))
            {
                project.ChangedFiles.Add(file.Key);
            }
        }
    }

    private void SaveCommittedSnapshot(ProjectModel project, IReadOnlyCollection<string> changedFiles)
    {
        var snapshot = _localRepositoryStore.LoadSnapshot(project.Id)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        var current = CaptureLocalSnapshot(project.LocalPath);
        foreach (var file in changedFiles)
        {
            if (current.TryGetValue(file, out var hash))
            {
                snapshot[file] = hash;
            }
        }

        _localRepositoryStore.SaveSnapshot(project, snapshot);
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

    private static Dictionary<string, string> CaptureLocalSnapshot(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var root = Path.GetFullPath(folderPath);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(root, path))
            .Select(path => new
            {
                Path = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Hash = ComputeFileHash(path)
            })
            .Where(file => !string.IsNullOrWhiteSpace(file.Hash))
            .OrderBy(file => file.Path)
            .Take(200)
            .ToDictionary(file => file.Path, file => file.Hash, StringComparer.OrdinalIgnoreCase);
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsIgnoredPath(string root, string filePath)
    {
        var relativePath = Path.GetRelativePath(root, filePath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part is ".git" or ".vs" or "bin" or "obj" or "node_modules");
    }

    private sealed record CreateProjectRequest(string Name, string? Description, string Visibility);
    private sealed record CreateBranchRequest(string Name, string? SourceBranch);
    private sealed record RenameBranchRequest(string Name);
    private sealed record UpdateProjectRequest(string? Name, string? Description, string? Visibility);
    private sealed record ProjectResponse(Guid Id, string Name, string? Description, string Visibility, string DefaultBranch, string OwnerName, DateTime CreatedAtUtc);
    private sealed record BranchResponse(string Name, bool IsDefault);
    private sealed record CommitResponse(string Sha, string Message, string Author, DateTime When);
    private sealed record FileEntryResponse(string Name, string Path, bool IsDirectory);
    private sealed record FileChangeRequest(string Path, string? Content, string Operation);
    private sealed record CreateCommitRequest(string Branch, string Message, List<FileChangeRequest> Changes);
}
