using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows.Input;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;

namespace Vcs.Desktop.ViewModels;

public sealed class ProfileViewModel : ObservableObject
{
    private readonly Action<ProjectModel> _openProject;
    private readonly Func<ProjectModel, Task> _deleteProject;
    private string _searchText = string.Empty;

    public ProfileViewModel(UserModel user, Action<ProjectModel> openProject, Func<ProjectModel, Task> deleteProject)
    {
        User = user;
        _openProject = openProject;
        _deleteProject = deleteProject;
        Projects = [];
        User.Projects.CollectionChanged += Projects_CollectionChanged;
        UpdateProjectsFilter();

        OpenProjectCommand = new RelayCommand(p =>
        {
            if (p is ProjectModel project)
            {
                _openProject(project);
            }
        }, p => p is ProjectModel);
        DeleteProjectCommand = new RelayCommand(DeleteProject);
    }

    public UserModel User { get; }
    public ObservableCollection<ProjectModel> Projects { get; }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                UpdateProjectsFilter();
            }
        }
    }

    public ICommand OpenProjectCommand { get; }
    public ICommand DeleteProjectCommand { get; }

    private async void DeleteProject(object? parameter)
    {
        if (parameter is ProjectModel project)
        {
            await _deleteProject(project);
        }
    }

    private void Projects_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateProjectsFilter();
    }

    private void UpdateProjectsFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? User.Projects
            : User.Projects.Where(project => project.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        Projects.Clear();
        foreach (var project in filtered)
        {
            Projects.Add(project);
        }
    }
}
