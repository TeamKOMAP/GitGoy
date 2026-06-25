using Vcs.Desktop.Core;

namespace Vcs.Desktop.ViewModels;

public sealed class FileChangeViewModel : ObservableObject
{
    private bool _isIncluded = true;

    public FileChangeViewModel(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public bool IsIncluded
    {
        get => _isIncluded;
        set => SetProperty(ref _isIncluded, value);
    }
}
