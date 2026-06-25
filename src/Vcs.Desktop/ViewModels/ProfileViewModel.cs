using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;

namespace Vcs.Desktop.ViewModels;

public sealed class ProfileViewModel
{
    private readonly Action<ProjectModel> _openProject;
    private readonly Func<ProjectModel, Task> _deleteProject;

    public ProfileViewModel(UserModel user, Action<ProjectModel> openProject, Func<ProjectModel, Task> deleteProject)
    {
        User = user;
        _openProject = openProject;
        _deleteProject = deleteProject;
        OpenProjectCommand = new RelayCommand(p =>
        {
            if (p is ProjectModel project)
            {
                _openProject(project);
            }
        }, p => p is ProjectModel project && Directory.Exists(project.LocalPath));
        DeleteProjectCommand = new RelayCommand(DeleteProject);
    }

    public UserModel User { get; }
    public ObservableCollection<ProjectModel> Projects => User.Projects;
    public ICommand OpenProjectCommand { get; }
    public ICommand DeleteProjectCommand { get; }

    private async void DeleteProject(object? parameter)
    {
        if (parameter is ProjectModel project)
        {
            await _deleteProject(project);
        }
    }
}
