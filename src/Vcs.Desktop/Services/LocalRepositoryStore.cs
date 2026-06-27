using System.IO;
using System.Text.Json;
using Vcs.Desktop.Models;

namespace Vcs.Desktop.Services;

internal sealed class LocalRepositoryStore
{
    private readonly string _storePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public LocalRepositoryStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appData, "Vcs.Desktop");
        Directory.CreateDirectory(directory);
        _storePath = Path.Combine(directory, "repositories.json");
    }

    public IReadOnlyDictionary<Guid, string> Load()
    {
        return LoadStates().ToDictionary(item => item.Key, item => item.Value.LocalPath);
    }

    public IReadOnlyDictionary<string, string> LoadSnapshot(Guid projectId, string branchName)
    {
        var states = LoadStates();
        if (!states.TryGetValue(projectId, out var state))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (state.BranchFileHashes.TryGetValue(branchName, out var branchSnapshot))
        {
            return new Dictionary<string, string>(branchSnapshot, StringComparer.OrdinalIgnoreCase);
        }

        return string.Equals(branchName, "main", StringComparison.OrdinalIgnoreCase)
            ? new Dictionary<string, string>(state.FileHashes, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Save(ProjectModel project)
    {
        if (string.IsNullOrWhiteSpace(project.LocalPath))
        {
            return;
        }

        var states = LoadStates();
        states[project.Id] = states.TryGetValue(project.Id, out var existing)
            ? existing with { LocalPath = project.LocalPath }
            : new LocalRepositoryState(project.LocalPath, new Dictionary<string, string>(), new Dictionary<string, Dictionary<string, string>>());

        SaveStates(states);
    }

    public void SaveSnapshot(ProjectModel project, string branchName, IReadOnlyDictionary<string, string> fileHashes)
    {
        if (string.IsNullOrWhiteSpace(project.LocalPath))
        {
            return;
        }

        var states = LoadStates();
        var existing = states.TryGetValue(project.Id, out var state)
            ? state
            : new LocalRepositoryState(project.LocalPath, [], []);

        var branchFileHashes = existing.BranchFileHashes
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        branchFileHashes[branchName] = fileHashes.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);

        states[project.Id] = new LocalRepositoryState(
            project.LocalPath,
            existing.FileHashes,
            branchFileHashes);

        SaveStates(states);
    }

    public void Remove(ProjectModel project)
    {
        var states = LoadStates();
        if (states.Remove(project.Id))
        {
            SaveStates(states);
        }
    }

    private Dictionary<Guid, LocalRepositoryState> LoadStates()
    {
        if (!File.Exists(_storePath))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_storePath));
            var repositories = new Dictionary<Guid, LocalRepositoryState>();

            foreach (var item in document.RootElement.EnumerateObject())
            {
                if (!Guid.TryParse(item.Name, out var id))
                {
                    continue;
                }

                if (item.Value.ValueKind == JsonValueKind.String)
                {
                    var path = item.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        repositories[id] = new LocalRepositoryState(path, [], []);
                    }

                    continue;
                }

                if (item.Value.ValueKind != JsonValueKind.Object
                    || !item.Value.TryGetProperty(nameof(LocalRepositoryState.LocalPath), out var pathElement))
                {
                    continue;
                }

                var localPath = pathElement.GetString();
                if (string.IsNullOrWhiteSpace(localPath))
                {
                    continue;
                }

                var fileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (item.Value.TryGetProperty(nameof(LocalRepositoryState.FileHashes), out var hashesElement)
                    && hashesElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var hash in hashesElement.EnumerateObject())
                    {
                        var value = hash.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            fileHashes[hash.Name] = value;
                        }
                    }
                }

                var branchFileHashes = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                if (item.Value.TryGetProperty(nameof(LocalRepositoryState.BranchFileHashes), out var branchesElement)
                    && branchesElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var branch in branchesElement.EnumerateObject())
                    {
                        var branchHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (branch.Value.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var hash in branch.Value.EnumerateObject())
                            {
                                var value = hash.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    branchHashes[hash.Name] = value;
                                }
                            }
                        }

                        branchFileHashes[branch.Name] = branchHashes;
                    }
                }

                repositories[id] = new LocalRepositoryState(localPath, fileHashes, branchFileHashes);
            }

            return repositories;
        }
        catch
        {
            return [];
        }
    }

    private void SaveStates(IReadOnlyDictionary<Guid, LocalRepositoryState> repositories)
    {
        var stored = repositories.ToDictionary(item => item.Key.ToString(), item => item.Value);
        File.WriteAllText(_storePath, JsonSerializer.Serialize(stored, _jsonOptions));
    }

    private sealed record LocalRepositoryState(
        string LocalPath,
        Dictionary<string, string> FileHashes,
        Dictionary<string, Dictionary<string, string>> BranchFileHashes);
}
