using LibGit2Sharp;

namespace Vcs.Infrastructure.Services;

public class GitService
{
    private readonly string _storageRoot;

    public GitService()
    {
        _storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "storage", "repositories");
        Directory.CreateDirectory(_storageRoot);
    }

    public string GetRepositoryPath(Guid projectId) => Path.Combine(_storageRoot, $"{projectId}.git");

    public Task InitRepositoryAsync(Guid projectId)
    {
        Repository.Init(GetRepositoryPath(projectId), isBare: true);
        return Task.CompletedTask;
    }

    public void DeleteRepository(Guid projectId)
    {
        var path = GetRepositoryPath(projectId);
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    public Task<List<BranchRes>> GetBranchesAsync(Guid projectId)
    {
        using var repo = Open(projectId);
        return Task.FromResult(repo.Branches.Where(b => !b.IsRemote)
            .Select(b => new BranchRes(b.FriendlyName, b.IsCurrentRepositoryHead)).ToList());
    }

    public Task CreateBranchAsync(Guid projectId, string name, string? sourceBranch)
    {
        using var repo = Open(projectId);
        var src = sourceBranch != null ? repo.Branches[sourceBranch] : repo.Head;
        if (src == null) throw new InvalidOperationException($"Source branch not found");
        if (repo.Branches[name] != null) throw new InvalidOperationException($"Branch '{name}' exists");
        repo.Branches.Add(name, src.Tip);
        return Task.CompletedTask;
    }

    public Task DeleteBranchAsync(Guid projectId, string name)
    {
        using var repo = Open(projectId);
        var b = repo.Branches[name];
        if (b == null) throw new InvalidOperationException($"Branch '{name}' not found");
        if (b.IsCurrentRepositoryHead) throw new InvalidOperationException("Cannot delete default branch");
        repo.Branches.Remove(b);
        return Task.CompletedTask;
    }

    public Task<List<CommitRes>> GetCommitsAsync(Guid projectId, string branch, int skip = 0, int take = 20)
    {
        using var repo = Open(projectId);
        var b = repo.Branches[branch] ?? throw new InvalidOperationException($"Branch '{branch}' not found");
        return Task.FromResult(b.Commits.Skip(skip).Take(take)
            .Select(c => new CommitRes(c.Sha, c.MessageShort, c.Author.Name, c.Author.When.UtcDateTime)).ToList());
    }

    public Task<CommitDetailRes?> GetCommitAsync(Guid projectId, string sha)
    {
        using var repo = Open(projectId);
        var c = repo.Lookup<Commit>(sha);
        if (c == null) return Task.FromResult<CommitDetailRes?>(null);

        var changes = new List<FileChange>();
        if (c.Parents.Any())
        {
            var diff = repo.Diff.Compare<TreeChanges>(c.Parents.First().Tree, c.Tree);
            foreach (var ch in diff)
                changes.Add(new FileChange(ch.Path, null, ch.Status.ToString().ToLower()));
        }
        else
        {
            foreach (var e in c.Tree)
                changes.Add(new FileChange(e.Path, null, "added"));
        }

        return Task.FromResult<CommitDetailRes?>(new CommitDetailRes(c.Sha, c.Message, c.Author.Name, c.Author.When.UtcDateTime, changes));
    }

    public async Task<string> CreateCommitAsync(Guid projectId, string branch, string message, string author, List<FileChange> changes)
    {
        var repoPath = GetRepositoryPath(projectId);
        var tmp = Path.Combine(Path.GetTempPath(), "vcs", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        try
        {
            Repository.Clone(repoPath, tmp, new CloneOptions { IsBare = false, BranchName = branch });
            using var repo = new Repository(tmp);
            foreach (var ch in changes)
            {
                var fp = Path.Combine(tmp, ch.Path);
                var dir = Path.GetDirectoryName(fp);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                switch (ch.Operation.ToLower())
                {
                    case "add": case "modify": await File.WriteAllTextAsync(fp, ch.Content ?? ""); break;
                    case "delete": if (File.Exists(fp)) File.Delete(fp); break;
                }
            }
            Commands.Stage(repo, "*");
            var sig = new Signature(author, $"{author}@vcs", DateTimeOffset.UtcNow);
            var commit = repo.Commit(message, sig, sig);
            repo.Network.Push(repo.Network.Remotes["origin"], $"refs/heads/{branch}");
            return commit.Sha;
        }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
    }

    public Task<List<FileEntryRes>> GetFilesAsync(Guid projectId, string branch, string? path)
    {
        using var repo = Open(projectId);
        var b = repo.Branches[branch] ?? throw new InvalidOperationException($"Branch '{branch}' not found");
        if (b.Tip == null) return Task.FromResult(new List<FileEntryRes>());

        Tree tree;
        if (string.IsNullOrEmpty(path)) { tree = b.Tip.Tree; }
        else
        {
            var e = b.Tip.Tree[path] ?? throw new InvalidOperationException($"Path '{path}' not found");
            if (e.TargetType != TreeEntryTargetType.Tree) throw new InvalidOperationException("Not a directory");
            tree = (Tree)e.Target;
        }
        return Task.FromResult(tree.Select(e => new FileEntryRes(e.Name, e.Path, e.TargetType == TreeEntryTargetType.Tree)).ToList());
    }

    public Task<List<DiffRes>> GetDiffAsync(Guid projectId, string? from, string to)
    {
        using var repo = Open(projectId);
        var toC = repo.Lookup<Commit>(to) ?? throw new InvalidOperationException($"Commit '{to}' not found");
        var fromC = !string.IsNullOrEmpty(from) ? repo.Lookup<Commit>(from) : toC.Parents.FirstOrDefault();

        if (fromC == null)
            return Task.FromResult(toC.Tree.Select(e => new DiffRes(e.Path, "", "added")).ToList());

        var patch = repo.Diff.Compare<Patch>(fromC.Tree, toC.Tree);
        return Task.FromResult(patch.Select(p => new DiffRes(p.Path, p.Patch, p.Status.ToString().ToLower())).ToList());
    }

    private Repository Open(Guid projectId)
    {
        var path = GetRepositoryPath(projectId);
        if (!Repository.IsValid(path)) throw new InvalidOperationException("Repository not found");
        return new Repository(path);
    }
}
