using System.Windows.Input;
using Vcs.Desktop.Core;

namespace Vcs.Desktop.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private readonly Func<string, string, Task> _login;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _isPasswordFocused;

    public LoginViewModel(Func<string, string, Task> login)
    {
        _login = login;
        LoginCommand = new RelayCommand(Submit, CanSubmit);
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ErrorMessage = string.Empty;
                RaiseCommandState();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                ErrorMessage = string.Empty;
                OnPropertyChanged(nameof(PasswordPlaceholderVisibility));
                RaiseCommandState();
            }
        }
    }

    public bool IsPasswordFocused
    {
        get => _isPasswordFocused;
        set
        {
            if (SetProperty(ref _isPasswordFocused, value))
            {
                OnPropertyChanged(nameof(PasswordPlaceholderVisibility));
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(ErrorVisibility));
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
                OnPropertyChanged(nameof(LoginButtonText));
                RaiseCommandState();
            }
        }
    }

    public string TitleText => "Вход";
    public string SubtitleText => "Введите любое имя и пароль";
    public string LoginButtonText => IsBusy ? "Подключаемся..." : "Войти";
    public string ErrorVisibility => string.IsNullOrWhiteSpace(ErrorMessage) ? "Collapsed" : "Visible";
    public string PasswordPlaceholderVisibility => string.IsNullOrEmpty(Password) && !IsPasswordFocused ? "Visible" : "Collapsed";
    public ICommand LoginCommand { get; }

    private bool CanSubmit()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Password);
    }

    private async void Submit()
    {
        IsBusy = true;
        try
        {
            await _login(Username, Password);
        }
        catch
        {
            ErrorMessage = "Не удалось войти. Проверьте запуск backend.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandState()
    {
        if (LoginCommand is RelayCommand command)
        {
            command.RaiseCanExecuteChanged();
        }
    }
}
