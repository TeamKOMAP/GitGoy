using System.Collections.ObjectModel;
using System.Windows.Input;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;
using Vcs.Desktop.Services;

namespace Vcs.Desktop.ViewModels;

public sealed class ProjectViewModel : ObservableObject
{
    private readonly IMockDataService _dataService;
    private ProjectModel _selectedProject;
    private BranchModel _selectedBranch;
    private CommitModel? _selectedCommit;
    private string _commitMessage = string.Empty;
    private string _commitDescription = string.Empty;
    private string _statusMessage;
    private bool _showSettings;

    public ProjectViewModel(ProjectModel project, IMockDataService dataService)
    {
        _dataService = dataService;
        _selectedProject = project;
        _selectedBranch = project.Branches.First();
        _selectedCommit = project.Commits.FirstOrDefault();
        ChangedFiles = CreateFileChanges(project);
        _statusMessage = project.IsOwnedByCurrentUser
            ? "Repository ready"
            : "Read-only project: view or clone";

        CommitCommand = new RelayCommand(CreateCommit, CanCreateCommit);
        PushCommand = new RelayCommand(Push, () => Project.IsOwnedByCurrentUser);
        CloneCommand = new RelayCommand(Clone);
        ToggleSettingsCommand = new RelayCommand(() => ShowSettings = !ShowSettings);
        CreateBranchCommand = new RelayCommand(CreateBranch, () => Project.IsOwnedByCurrentUser);
        ToggleVisibilityCommand = new RelayCommand(ToggleVisibility, () => Project.IsOwnedByCurrentUser);
    }

    public ProjectModel Project => SelectedProject;
    public ObservableCollection<ProjectModel> Repositories => _dataService.CurrentUser.Projects;
    public ObservableCollection<BranchModel> Branches => Project.Branches;
    public ObservableCollection<CommitModel> Commits => Project.Commits;
    public ObservableCollection<string> Files => Project.Files;
    public ObservableCollection<FileChangeViewModel> ChangedFiles { get; private set; }

    public ProjectModel SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                SelectedBranch = Project.Branches.First();
                SelectedCommit = Project.Commits.FirstOrDefault();
                ChangedFiles = CreateFileChanges(Project);
                StatusMessage = Project.IsOwnedByCurrentUser
                    ? "Repository ready"
                    : "Read-only project: view or clone";

                OnPropertyChanged(nameof(Project));
                OnPropertyChanged(nameof(Branches));
                OnPropertyChanged(nameof(Commits));
                OnPropertyChanged(nameof(Files));
                OnPropertyChanged(nameof(ChangedFiles));
                OnPropertyChanged(nameof(CanWrite));
                OnPropertyChanged(nameof(ModeLabel));
                OnPropertyChanged(nameof(VisibilityToggleText));
                OnPropertyChanged(nameof(ChangedFilesCount));
                RaiseCommandStates();
            }
        }
    }

    public BranchModel SelectedBranch
    {
        get => _selectedBranch;
        set => SetProperty(ref _selectedBranch, value);
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

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool ShowSettings
    {
        get => _showSettings;
        set => SetProperty(ref _showSettings, value);
    }

    public bool CanWrite => Project.IsOwnedByCurrentUser;
    public string ModeLabel => CanWrite ? "Owner workspace" : "Read-only cloneable repository";
    public string VisibilityToggleText => Project.Visibility == ProjectVisibility.Public
        ? "Сейчас это публичный репозиторий"
        : "Сейчас это приватный репозиторий";
    public int ChangedFilesCount => Files.Count;

    public ICommand CommitCommand { get; }
    public ICommand PushCommand { get; }
    public ICommand CloneCommand { get; }
    public ICommand ToggleSettingsCommand { get; }
    public ICommand CreateBranchCommand { get; }
    public ICommand ToggleVisibilityCommand { get; }

    private bool CanCreateCommit()
    {
        return Project.IsOwnedByCurrentUser
            && !string.IsNullOrWhiteSpace(CommitMessage)
            && ChangedFiles.Any(file => file.IsIncluded);
    }

    private void CreateCommit()
    {
        var includedFilesCount = ChangedFiles.Count(file => file.IsIncluded);
        var commit = _dataService.CreateCommit(Project, SelectedBranch, CommitMessage, includedFilesCount);
        SelectedCommit = commit;
        CommitMessage = string.Empty;
        CommitDescription = string.Empty;
        StatusMessage = $"Committed {commit.Sha} to {SelectedBranch.Name}";
    }

    private void Push()
    {
        _dataService.Push(Project, SelectedBranch);
        StatusMessage = $"Pushed {SelectedBranch.Name} to origin";
    }

    private void Clone()
    {
        var target = _dataService.Clone(Project, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        StatusMessage = $"Clone target: {target}";
    }

    private void CreateBranch()
    {
        var name = $"feature/mock-{Branches.Count + 1}";
        var branch = new BranchModel { Name = name };
        Branches.Add(branch);
        SelectedBranch = branch;
        StatusMessage = $"Created branch {name}";
    }

    private void ToggleVisibility()
    {
        Project.Visibility = Project.Visibility == ProjectVisibility.Public
            ? ProjectVisibility.Private
            : ProjectVisibility.Public;

        StatusMessage = Project.Visibility == ProjectVisibility.Public
            ? "Repository is public"
            : "Repository is private";

        OnPropertyChanged(nameof(VisibilityToggleText));
    }

    private ObservableCollection<FileChangeViewModel> CreateFileChanges(ProjectModel project)
    {
        var changes = new ObservableCollection<FileChangeViewModel>(
            project.Files.Select(file => new FileChangeViewModel(file)));

        foreach (var change in changes)
        {
            change.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(FileChangeViewModel.IsIncluded))
                {
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

        if (CreateBranchCommand is RelayCommand createBranchCommand)
        {
            createBranchCommand.RaiseCanExecuteChanged();
        }

        if (ToggleVisibilityCommand is RelayCommand visibilityCommand)
        {
            visibilityCommand.RaiseCanExecuteChanged();
        }
    }
}
