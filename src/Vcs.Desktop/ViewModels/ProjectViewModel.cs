using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;
using Vcs.Desktop.Services;

namespace Vcs.Desktop.ViewModels;

public sealed class ProjectViewModel : ObservableObject
{
    private readonly IRepositoryDataService _dataService;
    private readonly BranchModel _createBranchOption = new() { Name = "Create branch", IsCreateOption = true };
    private FileSystemWatcher? _fileSystemWatcher;
    private bool _isUpdatingAllChangedFiles;
    private ProjectModel _selectedProject;
    private BranchModel _selectedBranch;
    private CommitModel? _selectedCommit;
    private PushModel? _selectedPush;
    private string _commitMessage = string.Empty;
    private string _commitDescription = string.Empty;
    private string _commitSearchText = string.Empty;
    private string _editableRepositoryName = string.Empty;
    private string _editableBranchName = string.Empty;
    private bool _isBusy;

    public ProjectViewModel(ProjectModel project, IRepositoryDataService dataService)
    {
        _dataService = dataService;
        _selectedProject = project;
        _selectedBranch = project.Branches.First();
        _selectedCommit = project.Commits.FirstOrDefault();
        _selectedPush = project.Pushes.FirstOrDefault();
        ChangedFiles = CreateFileChanges(project);
        BranchMenuItems = [];
        RefreshBranchMenuItems();
        Commits = [];
        Pushes = [];
        Project.Commits.CollectionChanged += Commits_CollectionChanged;
        Project.Pushes.CollectionChanged += Pushes_CollectionChanged;
        UpdateCommitsFilter();
        UpdatePushesFilter();
        InitializeEditFields();
        StartLocalWatcher();
        _ = RefreshChangedFilesAsync();

        CommitCommand = new RelayCommand(CreateCommit, CanCreateCommit);
        PushCommand = new RelayCommand(Push, CanPush);
        OpenProjectFolderCommand = new RelayCommand(OpenProjectFolder, CanOpenProjectFolder);
        ToggleVisibilityCommand = new RelayCommand(ToggleVisibility, () => Project.IsOwnedByCurrentUser && !IsBusy);
        CreateBranchCommand = new RelayCommand(CreateDefaultBranch, CanCreateDefaultBranch);
        SaveRepositoryNameCommand = new RelayCommand(SaveRepositoryName, CanSaveRepositoryName);
        SaveBranchNameCommand = new RelayCommand(SaveBranchName, CanSaveBranchName);
        DeleteBranchCommand = new RelayCommand(DeleteBranch, CanDeleteBranch);
    }

    public ProjectModel Project => SelectedProject;
    public ObservableCollection<ProjectModel> Repositories => _dataService.CurrentUser.Projects;
    public ObservableCollection<BranchModel> Branches => Project.Branches;
    public ObservableCollection<BranchModel> BranchMenuItems { get; }
    public ObservableCollection<CommitModel> Commits { get; }
    public ObservableCollection<PushModel> Pushes { get; }
    public ObservableCollection<string> Files => Project.Files;
    public ObservableCollection<FileChangeViewModel> ChangedFiles { get; private set; }

    public ProjectModel SelectedProject
    {
        get => _selectedProject;
        set
        {
            var oldProject = _selectedProject;
            if (SetProperty(ref _selectedProject, value))
            {
                oldProject.Commits.CollectionChanged -= Commits_CollectionChanged;
                oldProject.Pushes.CollectionChanged -= Pushes_CollectionChanged;
                SelectedBranch = Project.Branches.First();
                SelectedCommit = Project.Commits.FirstOrDefault();
                SelectedPush = Project.Pushes.FirstOrDefault();
                ChangedFiles = CreateFileChanges(Project);
                RefreshBranchMenuItems();
                Project.Commits.CollectionChanged += Commits_CollectionChanged;
                Project.Pushes.CollectionChanged += Pushes_CollectionChanged;
                UpdateCommitsFilter();
                UpdatePushesFilter();
                InitializeEditFields();
                StartLocalWatcher();
                _ = RefreshChangedFilesAsync();

                OnPropertyChanged(nameof(Project));
                OnPropertyChanged(nameof(Branches));
                OnPropertyChanged(nameof(Commits));
                OnPropertyChanged(nameof(Pushes));
                OnPropertyChanged(nameof(Files));
                OnPropertyChanged(nameof(ChangedFiles));
                OnPropertyChanged(nameof(CanWrite));
                OnPropertyChanged(nameof(VisibilityToggleText));
                OnPropertyChanged(nameof(ChangedFilesCount));
                RaiseCommandStates();
            }
        }
    }

    public BranchModel SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            if (value is null)
            {
                OnPropertyChanged();
                return;
            }

            if (value.IsCreateOption)
            {
                _ = CreateDefaultBranchAsync();
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _selectedBranch, value))
            {
                EditableBranchName = value.Name;
                _ = LoadSelectedBranchAsync(value.Name);
                RaiseCommandStates();
            }
        }
    }

    public CommitModel? SelectedCommit
    {
        get => _selectedCommit;
        set => SetProperty(ref _selectedCommit, value);
    }

    public string CommitMessage
    {
        get => _commitMessage;
        set
        {
            if (SetProperty(ref _commitMessage, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string CommitDescription
    {
        get => _commitDescription;
        set => SetProperty(ref _commitDescription, value);
    }

    public PushModel? SelectedPush
    {
        get => _selectedPush;
        set => SetProperty(ref _selectedPush, value);
    }

    public string CommitSearchText
    {
        get => _commitSearchText;
        set
        {
            if (SetProperty(ref _commitSearchText, value))
            {
                UpdateCommitsFilter();
                UpdatePushesFilter();
            }
        }
    }

    public string EditableRepositoryName
    {
        get => _editableRepositoryName;
        set
        {
            if (SetProperty(ref _editableRepositoryName, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string EditableBranchName
    {
        get => _editableBranchName;
        set
        {
            if (SetProperty(ref _editableBranchName, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool CanWrite => Project.IsOwnedByCurrentUser;
    public bool AreAllChangedFilesIncluded
    {
        get => ChangedFiles.Count > 0 && ChangedFiles.All(file => file.IsIncluded);
        set
        {
            if (_isUpdatingAllChangedFiles)
            {
                return;
            }

            _isUpdatingAllChangedFiles = true;
            foreach (var file in ChangedFiles)
            {
                file.IsIncluded = value;
            }
            _isUpdatingAllChangedFiles = false;

            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public string VisibilityToggleText => Project.Visibility == ProjectVisibility.Public
        ? "Сейчас это публичный репозиторий"
        : "Сейчас это приватный репозиторий";
    public int ChangedFilesCount => ChangedFiles.Count;

    public ICommand CommitCommand { get; }
    public ICommand PushCommand { get; }
    public ICommand OpenProjectFolderCommand { get; }
    public ICommand ToggleVisibilityCommand { get; }
    public ICommand CreateBranchCommand { get; }
    public ICommand SaveRepositoryNameCommand { get; }
    public ICommand SaveBranchNameCommand { get; }
    public ICommand DeleteBranchCommand { get; }

    private bool CanCreateCommit()
    {
        return Project.IsOwnedByCurrentUser
            && !IsBusy
            && !string.IsNullOrWhiteSpace(CommitMessage)
            && ChangedFiles.Any(file => file.IsIncluded);
    }

    private bool CanPush()
    {
        return Project.IsOwnedByCurrentUser
            && !IsBusy
            && Project.Commits.Count > 0;
    }

    private async void CreateCommit()
    {
        var branchName = SelectedBranch.Name;
        IsBusy = true;
        try
        {
            var includedFiles = ChangedFiles
                .Where(file => file.IsIncluded)
                .Select(file => file.Path)
                .ToArray();

            var commit = await _dataService.CreateCommitAsync(
                Project,
                SelectedBranch,
                CommitMessage,
                CommitDescription,
                includedFiles);

            foreach (var file in includedFiles)
            {
                Project.ChangedFiles.Remove(file);
            }

            RefreshProjectBindings(branchName);
            SelectedCommit = commit;
            CommitMessage = string.Empty;
            CommitDescription = string.Empty;
        }
        catch
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void Push()
    {
        var branchName = SelectedBranch.Name;
        IsBusy = true;
        try
        {
            await _dataService.PushAsync(Project, SelectedBranch);
            RefreshProjectBindings(branchName);
        }
        catch
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanOpenProjectFolder()
    {
        return Directory.Exists(Project.LocalPath);
    }

    private void OpenProjectFolder()
    {
        if (!Directory.Exists(Project.LocalPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Project.LocalPath,
            UseShellExecute = true
        });
    }

    private async void ToggleVisibility()
    {
        var oldVisibility = Project.Visibility;
        var newVisibility = Project.Visibility == ProjectVisibility.Public
            ? ProjectVisibility.Private
            : ProjectVisibility.Public;

        Project.Visibility = newVisibility;
        OnPropertyChanged(nameof(VisibilityToggleText));

        IsBusy = true;
        try
        {
            await _dataService.SetProjectVisibilityAsync(Project, newVisibility);
        }
        catch
        {
            Project.Visibility = oldVisibility;
            OnPropertyChanged(nameof(VisibilityToggleText));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDeleteBranch()
    {
        return Project.IsOwnedByCurrentUser
            && !IsBusy
            && !SelectedBranch.IsDefault
            && Branches.Count > 1;
    }

    private bool CanCreateDefaultBranch()
    {
        return Project.IsOwnedByCurrentUser
            && !IsBusy
            && Branches.Count > 0;
    }

    private async void CreateDefaultBranch()
    {
        await CreateDefaultBranchAsync();
    }

    private async Task CreateDefaultBranchAsync()
    {
        if (!CanCreateDefaultBranch())
        {
            return;
        }

        IsBusy = true;
        try
        {
            var branch = await _dataService.CreateBranchAsync(Project, SelectedBranch, CreateDefaultBranchName());
            RefreshProjectBindings(branch.Name);
        }
        catch
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveRepositoryName()
    {
        return Project.IsOwnedByCurrentUser
            && !IsBusy
            && !string.IsNullOrWhiteSpace(EditableRepositoryName)
            && !string.Equals(Project.Name, EditableRepositoryName.Trim(), StringComparison.Ordinal);
    }

    private async void SaveRepositoryName()
    {
        IsBusy = true;
        try
        {
            await _dataService.RenameRepositoryAsync(Project, EditableRepositoryName);
            EditableRepositoryName = Project.Name;
            OnPropertyChanged(nameof(Project));
            OnPropertyChanged(nameof(Repositories));
        }
        catch
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveBranchName()
    {
        var branchName = EditableBranchName.Trim();
        return Project.IsOwnedByCurrentUser
            && !IsBusy
            && !string.IsNullOrWhiteSpace(branchName)
            && !SelectedBranch.IsCreateOption
            && !string.Equals(SelectedBranch.Name, branchName, StringComparison.Ordinal)
            && !Branches.Any(branch => branch != SelectedBranch
                && string.Equals(branch.Name, branchName, StringComparison.OrdinalIgnoreCase));
    }

    private async void SaveBranchName()
    {
        IsBusy = true;
        try
        {
            var branch = await _dataService.RenameBranchAsync(Project, SelectedBranch, EditableBranchName);
            RefreshProjectBindings(branch.Name);
        }
        catch
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void DeleteBranch()
    {
        var branchIndex = Branches.IndexOf(SelectedBranch);
        IsBusy = true;
        try
        {
            await _dataService.DeleteBranchAsync(Project, SelectedBranch);

            if (Branches.Count == 0)
            {
                Project.Branches.Add(new BranchModel { Name = "main", IsDefault = true });
            }

            var replacementIndex = Math.Clamp(branchIndex, 0, Math.Max(Branches.Count - 1, 0));
            var replacementBranch = Branches.ElementAtOrDefault(replacementIndex)
                ?? Project.Branches.FirstOrDefault(branch => branch.IsDefault)
                ?? Project.Branches.First();
            await _dataService.LoadBranchAsync(Project, replacementBranch);
            RefreshProjectBindings(replacementBranch.Name);
        }
        catch
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ObservableCollection<FileChangeViewModel> CreateFileChanges(ProjectModel project)
    {
        var changes = new ObservableCollection<FileChangeViewModel>(
            project.ChangedFiles.Select(file => new FileChangeViewModel(file)));

        foreach (var change in changes)
        {
            change.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FileChangeViewModel.IsIncluded))
                {
                    OnPropertyChanged(nameof(AreAllChangedFilesIncluded));
                    RaiseCommandStates();
                }
            };
        }

        return changes;
    }

    private void RaiseCommandStates()
    {
        if (CommitCommand is RelayCommand commitCommand)
        {
            commitCommand.RaiseCanExecuteChanged();
        }

        if (PushCommand is RelayCommand pushCommand)
        {
            pushCommand.RaiseCanExecuteChanged();
        }

        if (ToggleVisibilityCommand is RelayCommand visibilityCommand)
        {
            visibilityCommand.RaiseCanExecuteChanged();
        }

        if (CreateBranchCommand is RelayCommand createBranchCommand)
        {
            createBranchCommand.RaiseCanExecuteChanged();
        }

        if (SaveRepositoryNameCommand is RelayCommand saveRepositoryNameCommand)
        {
            saveRepositoryNameCommand.RaiseCanExecuteChanged();
        }

        if (SaveBranchNameCommand is RelayCommand saveBranchNameCommand)
        {
            saveBranchNameCommand.RaiseCanExecuteChanged();
        }

        if (OpenProjectFolderCommand is RelayCommand folderCommand)
        {
            folderCommand.RaiseCanExecuteChanged();
        }

        if (DeleteBranchCommand is RelayCommand deleteBranchCommand)
        {
            deleteBranchCommand.RaiseCanExecuteChanged();
        }
    }

    private void RefreshProjectBindings(string? selectedBranchName = null)
    {
        selectedBranchName ??= SelectedBranch.Name;
        ChangedFiles = CreateFileChanges(Project);
        RefreshBranchMenuItems();
        RestoreSelectedBranch(selectedBranchName);
        UpdateCommitsFilter();
        UpdatePushesFilter();
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(Branches));
        OnPropertyChanged(nameof(BranchMenuItems));
        OnPropertyChanged(nameof(Commits));
        OnPropertyChanged(nameof(Pushes));
        OnPropertyChanged(nameof(Files));
        OnPropertyChanged(nameof(ChangedFiles));
        OnPropertyChanged(nameof(ChangedFilesCount));
        OnPropertyChanged(nameof(AreAllChangedFilesIncluded));
    }

    private async Task RefreshChangedFilesAsync()
    {
        await _dataService.RefreshChangedFilesAsync(Project, SelectedBranch);
        RefreshProjectBindings();
        RaiseCommandStates();
    }

    private async Task LoadSelectedBranchAsync(string branchName)
    {
        IsBusy = true;
        try
        {
            var branch = Branches.FirstOrDefault(item => string.Equals(item.Name, branchName, StringComparison.OrdinalIgnoreCase));
            if (branch is null)
            {
                return;
            }

            await _dataService.LoadBranchAsync(Project, branch);
            RefreshProjectBindings(branchName);
        }
        catch
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshBranchMenuItems()
    {
        BranchMenuItems.Clear();
        foreach (var branch in Branches)
        {
            BranchMenuItems.Add(branch);
        }

        BranchMenuItems.Add(_createBranchOption);
    }

    private void InitializeEditFields()
    {
        EditableRepositoryName = Project.Name;
        EditableBranchName = SelectedBranch.Name;
    }

    private void RestoreSelectedBranch(string branchName)
    {
        var branch = Branches.FirstOrDefault(item => string.Equals(item.Name, branchName, StringComparison.OrdinalIgnoreCase))
            ?? Branches.FirstOrDefault();
        if (branch is null)
        {
            return;
        }

        _selectedBranch = branch;
        EditableBranchName = branch.Name;
        OnPropertyChanged(nameof(SelectedBranch));
    }

    private string CreateDefaultBranchName()
    {
        const string baseName = "new-branch";
        if (!Branches.Any(branch => string.Equals(branch.Name, baseName, StringComparison.OrdinalIgnoreCase)))
        {
            return baseName;
        }

        var index = 2;
        while (Branches.Any(branch => string.Equals(branch.Name, $"{baseName}-{index}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"{baseName}-{index}";
    }

    private void StartLocalWatcher()
    {
        _fileSystemWatcher?.Dispose();
        _fileSystemWatcher = null;

        if (!Directory.Exists(Project.LocalPath))
        {
            return;
        }

        _fileSystemWatcher = new FileSystemWatcher(Project.LocalPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime
        };

        _fileSystemWatcher.Changed += LocalFilesChanged;
        _fileSystemWatcher.Created += LocalFilesChanged;
        _fileSystemWatcher.Renamed += LocalFilesChanged;
        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    private void LocalFilesChanged(object sender, FileSystemEventArgs e)
    {
        if (IsIgnoredWatcherPath(e.FullPath))
        {
            return;
        }

        _ = Application.Current.Dispatcher.InvokeAsync(async () => await RefreshChangedFilesAsync());
    }

    private static bool IsIgnoredWatcherPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part is ".git" or ".vs" or "bin" or "obj" or "node_modules");
    }

    private void Commits_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCommitsFilter();
    }

    private void Pushes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdatePushesFilter();
    }

    private void UpdateCommitsFilter()
    {
        var query = CommitSearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? Project.Commits
            : Project.Commits.Where(commit => commit.Message.Contains(query, StringComparison.OrdinalIgnoreCase));

        Commits.Clear();
        foreach (var commit in filtered)
        {
            Commits.Add(commit);
        }
    }

    private void UpdatePushesFilter()
    {
        var query = CommitSearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? Project.Pushes
            : Project.Pushes.Where(push =>
                push.BranchName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || push.CommitMessage.Contains(query, StringComparison.OrdinalIgnoreCase)
                || push.Files.Any(file => file.Contains(query, StringComparison.OrdinalIgnoreCase)));

        Pushes.Clear();
        foreach (var push in filtered)
        {
            Pushes.Add(push);
        }

        SelectedPush = Pushes.FirstOrDefault();
    }
}
