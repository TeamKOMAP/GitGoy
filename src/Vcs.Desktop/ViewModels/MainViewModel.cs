using System.Windows.Input;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;
using Vcs.Desktop.Services;

namespace Vcs.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const double SidebarDragThreshold = 28;
    private readonly Action _exit;
    private IRepositoryDataService? _dataService;
    private object _currentViewModel;
    private ProfileViewModel? _profile;
    private ProjectViewModel? _project;
    private RepositorySetupViewModel? _repositorySetup;
    private bool _isSidebarExpanded;
    private bool _isAuthenticated;
    private double _sidebarDragOffset;

    public MainViewModel(Action exit)
    {
        _exit = exit;
        Login = new LoginViewModel(SignInAsync);
        _currentViewModel = Login;

        ShowProfileCommand = new RelayCommand(() => Navigate(Profile), () => _isAuthenticated);
        ShowRepositorySetupCommand = new RelayCommand(() => Navigate(RepositorySetup), () => _isAuthenticated);
        ShowProjectCommand = new RelayCommand(() =>
        {
            if (_project is not null)
            {
                Navigate(_project);
            }
        }, () => _isAuthenticated && _project is not null);
        ExitCommand = new RelayCommand(_exit);
    }

    public LoginViewModel Login { get; }
    public ProfileViewModel Profile => _profile ?? throw new InvalidOperationException("Profile is not ready.");
    public RepositorySetupViewModel RepositorySetup => _repositorySetup ?? throw new InvalidOperationException("Repository setup is not ready.");
    public ProjectViewModel Project
    {
        get => _project ?? throw new InvalidOperationException("Project is not ready.");
        private set => SetProperty(ref _project, value);
    }

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(SidebarWidth));
                OnPropertyChanged(nameof(SidebarLabelVisibility));
            }
        }
    }

    public double SidebarWidth => IsSidebarExpanded ? 236 : 72;
    public string SidebarLabelVisibility => IsSidebarExpanded ? "Visible" : "Collapsed";
    public string SidebarVisibility => _isAuthenticated ? "Visible" : "Collapsed";

    public ICommand ShowProfileCommand { get; }
    public ICommand ShowRepositorySetupCommand { get; }
    public ICommand ShowProjectCommand { get; }
    public ICommand ExitCommand { get; }

    public void DragSidebar(double horizontalChange)
    {
        _sidebarDragOffset += horizontalChange;

        if (!IsSidebarExpanded && _sidebarDragOffset >= SidebarDragThreshold)
        {
            IsSidebarExpanded = true;
            ResetSidebarDrag();
        }
        else if (IsSidebarExpanded && _sidebarDragOffset <= -SidebarDragThreshold)
        {
            IsSidebarExpanded = false;
            ResetSidebarDrag();
        }
    }

    public void ResetSidebarDrag()
    {
        _sidebarDragOffset = 0;
    }

    private void Navigate(object viewModel)
    {
        CurrentViewModel = viewModel;
    }

    private async Task SignInAsync(string username, string password)
    {
        var dataService = await RepositoryDataServiceFactory.SignInAsync(username, password);
        OpenSession(dataService);
    }

    private void OpenSession(IRepositoryDataService dataService)
    {
        _dataService = dataService;
        _profile = new ProfileViewModel(dataService.CurrentUser, OpenProject, DeleteProjectAsync);
        _repositorySetup = new RepositorySetupViewModel(dataService, OpenProject);
        if (dataService.CurrentProject is not null)
        {
            Project = new ProjectViewModel(dataService.CurrentProject, dataService);
        }
        _isAuthenticated = true;

        OnPropertyChanged(nameof(Profile));
        OnPropertyChanged(nameof(RepositorySetup));
        OnPropertyChanged(nameof(SidebarVisibility));
        RaiseNavigationState();
        Navigate(RepositorySetup);
    }

    private void OpenProject(ProjectModel project)
    {
        if (_dataService is null)
        {
            return;
        }

        Project = new ProjectViewModel(project, _dataService);
        Navigate(Project);
        RaiseNavigationState();
    }

    private async Task DeleteProjectAsync(ProjectModel project)
    {
        if (_dataService is null)
        {
            return;
        }

        try
        {
            await _dataService.DeleteRepositoryAsync(project);
        }
        catch
        {
            return;
        }

        if (_project?.Project == project)
        {
            if (_dataService.CurrentProject is null)
            {
                _project = null;
                OnPropertyChanged(nameof(Project));
                Navigate(RepositorySetup);
            }
            else
            {
                Project = new ProjectViewModel(_dataService.CurrentProject, _dataService);
            }
        }

        RaiseNavigationState();
    }

    private void RaiseNavigationState()
    {
        if (ShowProfileCommand is RelayCommand profileCommand)
        {
            profileCommand.RaiseCanExecuteChanged();
        }

        if (ShowProjectCommand is RelayCommand projectCommand)
        {
            projectCommand.RaiseCanExecuteChanged();
        }

        if (ShowRepositorySetupCommand is RelayCommand setupCommand)
        {
            setupCommand.RaiseCanExecuteChanged();
        }
    }
}
