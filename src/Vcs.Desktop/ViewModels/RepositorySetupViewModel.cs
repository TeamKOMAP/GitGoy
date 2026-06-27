using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;
using Vcs.Desktop.Services;

namespace Vcs.Desktop.ViewModels;

public sealed class RepositorySetupViewModel : ObservableObject
{
    private readonly IRepositoryDataService _dataService;
    private readonly Action<ProjectModel> _openProject;
    private string _selectedFolder = string.Empty;
    private string _repositoryName = string.Empty;
    private string _description = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public RepositorySetupViewModel(IRepositoryDataService dataService, Action<ProjectModel> openProject)
    {
        _dataService = dataService;
        _openProject = openProject;
        BrowseFolderCommand = new RelayCommand(BrowseFolder, () => !IsBusy);
        CreateRepositoryCommand = new RelayCommand(CreateRepository, CanCreateRepository);
    }

    public string SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                if (string.IsNullOrWhiteSpace(RepositoryName) && Directory.Exists(value))
                {
                    RepositoryName = new DirectoryInfo(value).Name;
                }

                StatusMessage = string.Empty;
                RaiseCommandStates();
            }
        }
    }

    public string RepositoryName
    {
        get => _repositoryName;
        set
        {
            if (SetProperty(ref _repositoryName, value))
            {
                StatusMessage = string.Empty;
                RaiseCommandStates();
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(StatusVisibility));
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
                OnPropertyChanged(nameof(CreateButtonText));
                RaiseCommandStates();
            }
        }
    }

    public string CreateButtonText => IsBusy ? "Создаем..." : "Создать репозиторий";
    public string StatusVisibility => string.IsNullOrWhiteSpace(StatusMessage) ? "Collapsed" : "Visible";
    public ICommand BrowseFolderCommand { get; }
    public ICommand CreateRepositoryCommand { get; }

    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку проекта",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFolder = dialog.FolderName;
        }
    }

    private bool CanCreateRepository()
    {
        return !IsBusy
            && Directory.Exists(SelectedFolder)
            && !string.IsNullOrWhiteSpace(RepositoryName);
    }

    private async void CreateRepository()
    {
        IsBusy = true;
        try
        {
            var project = await _dataService.CreateRepositoryAsync(SelectedFolder, RepositoryName, Description);
            _openProject(project);
        }
        catch
        {
            StatusMessage = "Не удалось создать репозиторий. Проверьте папку и запуск backend.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStates()
    {
        if (BrowseFolderCommand is RelayCommand browseCommand)
        {
            browseCommand.RaiseCanExecuteChanged();
        }

        if (CreateRepositoryCommand is RelayCommand createCommand)
        {
            createCommand.RaiseCanExecuteChanged();
        }
    }
}
