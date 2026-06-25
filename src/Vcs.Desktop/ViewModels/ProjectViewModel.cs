using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    private bool _isBusy;

    public ProjectViewModel(ProjectModel project, IRepositoryDataService dataService)
    {
        _dataService = dataService;
        _selectedProject = project;
        _selectedBranch = project.Branches.First();
        _selectedCommit = project.Commits.FirstOrDefault();
        ChangedFiles = CreateFileChanges(project);

        CommitCommand = new RelayCommand(CreateCommit, CanCreateCommit);
        PushCommand = new RelayCommand(Push, () => Project.IsOwnedByCurrentUser && !IsBusy);
        OpenProjectFolderCommand = new RelayCommand(OpenProjectFolder, CanOpenProjectFolder);
        ToggleVisibilityCommand = new RelayCommand(ToggleVisibility, () => Project.IsOwnedByCurrentUser && !IsBusy);
        DeleteBranchCommand = new RelayCommand(DeleteBranch, CanDeleteBranch);
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
        set
        {
            if (SetProperty(ref _selectedBranch, value))
            {
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
    public ICommand OpenProjectFolderCommand { get; }
    public ICommand ToggleVisibilityCommand { get; }
    public ICommand DeleteBranchCommand { get; }

    private bool CanCreateCommit()
    {
        return Project.IsOwnedByCurrentUser
            && !IsBusy
            && !string.IsNullOrWhiteSpace(CommitMessage)
            && ChangedFiles.Any(file => file.IsIncluded);
    }

    private async void CreateCommit()
    {
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

            RefreshProjectBindings();
            SelectedBranch = Project.Branches.FirstOrDefault(branch => branch.Name == SelectedBranch.Name)
                ?? Project.Branches.First();
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
        IsBusy = true;
        try
        {
            await _dataService.PushAsync(Project, SelectedBranch);
            RefreshProjectBindings();
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

    private async void DeleteBranch()
    {
        IsBusy = true;
        try
        {
            await _dataService.DeleteBranchAsync(Project, SelectedBranch);

            if (Branches.Count == 0)
            {
                Project.Branches.Add(new BranchModel { Name = "main", IsDefault = true });
            }

            SelectedBranch = Project.Branches.FirstOrDefault(branch => branch.IsDefault)
                ?? Project.Branches.First();
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

        if (OpenProjectFolderCommand is RelayCommand folderCommand)
        {
            folderCommand.RaiseCanExecuteChanged();
        }

        if (DeleteBranchCommand is RelayCommand deleteBranchCommand)
        {
            deleteBranchCommand.RaiseCanExecuteChanged();
        }
    }

    private void RefreshProjectBindings()
    {
        ChangedFiles = CreateFileChanges(Project);
        OnPropertyChanged(nameof(Branches));
        OnPropertyChanged(nameof(Commits));
        OnPropertyChanged(nameof(Files));
        OnPropertyChanged(nameof(ChangedFiles));
        OnPropertyChanged(nameof(ChangedFilesCount));
        OnPropertyChanged(nameof(AreAllChangedFilesIncluded));
    }
}
