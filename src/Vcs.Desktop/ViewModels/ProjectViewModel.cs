using System.Collections.ObjectModel;
using System.Windows.Input;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;
using Vcs.Desktop.Services;

namespace Vcs.Desktop.ViewModels;

public sealed class ProjectViewModel : ObservableObject
{
    private readonly IRepositoryDataService _dataService;
    private bool _isUpdatingAllChangedFiles;
    private ProjectModel _selectedProject;
    private BranchModel _selectedBranch;
    private CommitModel? _selectedCommit;
    private string _commitMessage = string.Empty;
    private string _commitDescription = string.Empty;

    public ProjectViewModel(ProjectModel project, IRepositoryDataService dataService)
    {
        _dataService = dataService;
        _selectedProject = project;
        _selectedBranch = project.Branches.First();
        _selectedCommit = project.Commits.FirstOrDefault();
        ChangedFiles = CreateFileChanges(project);

        CommitCommand = new RelayCommand(CreateCommit, CanCreateCommit);
        PushCommand = new RelayCommand(Push, () => Project.IsOwnedByCurrentUser);
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

                OnPropertyChanged(nameof(Project));
                OnPropertyChanged(nameof(Branches));
                OnPropertyChanged(nameof(Commits));
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

    public bool CanWrite => Project.IsOwnedByCurrentUser;
    public bool AreAllChangedFilesIncluded
    {
        get => ChangedFiles.All(file => file.IsIncluded);
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
    public int ChangedFilesCount => Files.Count;

    public ICommand CommitCommand { get; }
    public ICommand PushCommand { get; }
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
    }

    private void Push()
    {
        _dataService.Push(Project, SelectedBranch);
    }

    private void ToggleVisibility()
    {
        Project.Visibility = Project.Visibility == ProjectVisibility.Public
            ? ProjectVisibility.Private
            : ProjectVisibility.Public;

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
    }
}
