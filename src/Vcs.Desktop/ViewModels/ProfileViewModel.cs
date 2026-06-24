using System.Collections.ObjectModel;
using System.Windows.Input;
using Vcs.Desktop.Core;
using Vcs.Desktop.Models;

namespace Vcs.Desktop.ViewModels;

public sealed class ProfileViewModel
{
    private readonly Action<ProjectModel> _openProject;

    public ProfileViewModel(UserModel user, Action<ProjectModel> openProject)
    {
        User = user;
        _openProject = openProject;
        OpenProjectCommand = new RelayCommand(p =>
        {
            if (p is ProjectModel project)
            {
                _openProject(project);
            }
        });
    }

    public UserModel User { get; }
    public ObservableCollection<ProjectModel> Projects => User.Projects;
    public ICommand OpenProjectCommand { get; }
}
