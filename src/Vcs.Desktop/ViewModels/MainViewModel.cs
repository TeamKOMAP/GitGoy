using System.Windows.Input;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;
using Vcs.Desktop.Services;

namespace Vcs.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const double SidebarDragThreshold = 28;
    private readonly IRepositoryDataService _dataService;
    private readonly Action _exit;
    private object _currentViewModel;
    private bool _isSidebarExpanded;
    private double _sidebarDragOffset;

    public MainViewModel(IRepositoryDataService dataService, Action exit)
    {
        _dataService = dataService;
        _exit = exit;
        Profile = new ProfileViewModel(dataService.CurrentUser, OpenProject);
        Project = new ProjectViewModel(dataService.CurrentProject, dataService);
        _currentViewModel = Project;

        ShowProfileCommand = new RelayCommand(() => Navigate(Profile));
        ShowProjectCommand = new RelayCommand(() => Navigate(Project));
        ExitCommand = new RelayCommand(_exit);
    }

    public ProfileViewModel Profile { get; }
    public ProjectViewModel Project { get; private set; }

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

    public ICommand ShowProfileCommand { get; }
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

    private void OpenProject(ProjectModel project)
    {
        Project = new ProjectViewModel(project, _dataService);
        OnPropertyChanged(nameof(Project));
        Navigate(Project);
    }
}
