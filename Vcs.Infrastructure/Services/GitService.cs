using System.Text.Json;

namespace Vcs.Infrastructure.Services;

public class GitService
{
    private const string StoreFileName = "repository.json";
    private readonly string _storageRoot;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public GitService()
    {
        _storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "storage", "repositories");
        Directory.CreateDirectory(_storageRoot);
    }

    public string GetRepositoryPath(Guid projectId) => Path.Combine(_storageRoot, projectId.ToString());

    public async Task InitRepositoryAsync(Guid projectId)
    {
        var repoPath = GetRepositoryPath(projectId);
        var storePath = GetStorePath(projectId);
        Directory.CreateDirectory(repoPath);

        if (File.Exists(storePath))
        {
            return;
        }

        var initialCommit = new CommitData(
            Guid.NewGuid().ToString("N"),
            "Initial commit",
            "system",
            DateTime.UtcNow,
            [new FileChange("README.md", "# Repository" + Environment.NewLine, "added")]);

        var data = new RepositoryData
        {
            Branches =
            [
                new BranchData
                {
                    Name = "main",
                    IsDefault = true,
                    Files = [new FileData("README.md", "# Repository" + Environment.NewLine)],
                    Commits = [initialCommit]
                }
            ]
        };

        await SaveAsync(projectId, data);
    }

    public void DeleteRepository(Guid projectId)
    {
        var path = GetRepositoryPath(projectId);
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    public async Task<List<BranchRes>> GetBranchesAsync(Guid projectId)
    {
        var data = await LoadAsync(projectId);
        return data.Branches
            .Select(branch => new BranchRes(branch.Name, branch.IsDefault))
            .ToList();
    }

    public async Task CreateBranchAsync(Guid projectId, string name, string? sourceBranch)
    {
        var data = await LoadAsync(projectId);
        if (data.Branches.Any(branch => branch.Name == name))
        {
            throw new InvalidOperationException($"Branch '{name}' exists");
        }

        var source = FindBranch(data, sourceBranch ?? data.DefaultBranchName);
        data.Branches.Add(new BranchData
        {
            Name = name,
            Files = source.Files.Select(file => new FileData(file.Path, file.Content)).ToList(),
            Commits = source.Commits.ToList()
        });

        await SaveAsync(projectId, data);
    }

    public async Task DeleteBranchAsync(Guid projectId, string name)
    {
        var data = await LoadAsync(projectId);
        var branch = FindBranch(data, name);
        if (branch.IsDefault)
        {
            throw new InvalidOperationException("Cannot delete default branch");
        }

        data.Branches.Remove(branch);
        await SaveAsync(projectId, data);
    }

    public async Task<List<CommitRes>> GetCommitsAsync(Guid projectId, string branch, int skip = 0, int take = 20)
    {
        var data = await LoadAsync(projectId);
        return FindBranch(data, branch).Commits
            .OrderByDescending(commit => commit.When)
            .Skip(skip)
            .Take(take)
            .Select(commit => new CommitRes(commit.Sha, commit.Message, commit.Author, commit.When))
            .ToList();
    }

    public async Task<CommitDetailRes?> GetCommitAsync(Guid projectId, string sha)
    {
        var data = await LoadAsync(projectId);
        var commit = data.Branches
            .SelectMany(branch => branch.Commits)
            .FirstOrDefault(item => item.Sha.StartsWith(sha, StringComparison.OrdinalIgnoreCase));

        return commit is null
            ? null
            : new CommitDetailRes(commit.Sha, commit.Message, commit.Author, commit.When, commit.Changes);
    }

    public async Task<string> CreateCommitAsync(Guid projectId, string branch, string message, string author, List<FileChange> changes)
    {
        var data = await LoadAsync(projectId);
        var targetBranch = FindBranch(data, branch);

        foreach (var change in changes)
        {
            var existingFile = targetBranch.Files.FirstOrDefault(file => file.Path == change.Path);
            switch (change.Operation.ToLowerInvariant())
            {
                case "add":
                case "modify":
                    if (existingFile is null)
                    {
                        targetBranch.Files.Add(new FileData(change.Path, change.Content ?? string.Empty));
                    }
                    else
                    {
                        existingFile.Content = change.Content ?? string.Empty;
                    }
                    break;
                case "delete":
                    if (existingFile is not null)
                    {
                        targetBranch.Files.Remove(existingFile);
                    }
                    break;
            }
        }

        var commit = new CommitData(Guid.NewGuid().ToString("N"), message, author, DateTime.UtcNow, changes);
        targetBranch.Commits.Insert(0, commit);
        await SaveAsync(projectId, data);
        return commit.Sha;
    }

    public async Task<List<FileEntryRes>> GetFilesAsync(Guid projectId, string branch, string? path)
    {
        var data = await LoadAsync(projectId);
        var files = FindBranch(data, branch).Files;
        var prefix = string.IsNullOrWhiteSpace(path) ? string.Empty : path.TrimEnd('/') + "/";

        return files
            .Where(file => file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(file => new FileEntryRes(Path.GetFileName(file.Path), file.Path, false))
            .ToList();
    }

    public async Task<List<DiffRes>> GetDiffAsync(Guid projectId, string? from, string to)
    {
        var data = await LoadAsync(projectId);
        var commit = data.Branches
            .SelectMany(branch => branch.Commits)
            .FirstOrDefault(item => item.Sha.StartsWith(to, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Commit '{to}' not found");

        return commit.Changes
            .Select(change => new DiffRes(change.Path, change.Content ?? string.Empty, change.Operation))
            .ToList();
    }

    private string GetStorePath(Guid projectId) => Path.Combine(GetRepositoryPath(projectId), StoreFileName);

    private async Task<RepositoryData> LoadAsync(Guid projectId)
    {
        await InitRepositoryAsync(projectId);
        await using var stream = File.OpenRead(GetStorePath(projectId));
        return await JsonSerializer.DeserializeAsync<RepositoryData>(stream, _jsonOptions)
            ?? throw new InvalidOperationException("Repository store is empty");
    }

    private async Task SaveAsync(Guid projectId, RepositoryData data)
    {
        Directory.CreateDirectory(GetRepositoryPath(projectId));
        await using var stream = File.Create(GetStorePath(projectId));
        await JsonSerializer.SerializeAsync(stream, data, _jsonOptions);
    }

    private static BranchData FindBranch(RepositoryData data, string branchName)
    {
        return data.Branches.FirstOrDefault(branch => branch.Name == branchName)
            ?? throw new InvalidOperationException($"Branch '{branchName}' not found");
    }

    private sealed class RepositoryData
    {
        public List<BranchData> Branches { get; set; } = [];
        public string DefaultBranchName => Branches.FirstOrDefault(branch => branch.IsDefault)?.Name ?? "main";
    }

    private sealed class BranchData
    {
        public string Name { get; set; } = "main";
        public bool IsDefault { get; set; }
        public List<FileData> Files { get; set; } = [];
        public List<CommitData> Commits { get; set; } = [];
    }

    private sealed class FileData
    {
        public FileData()
        {
        }

        public FileData(string path, string content)
        {
            Path = path;
            Content = content;
        }

        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed record CommitData(
        string Sha,
        string Message,
        string Author,
        DateTime When,
        List<FileChange> Changes);
}
