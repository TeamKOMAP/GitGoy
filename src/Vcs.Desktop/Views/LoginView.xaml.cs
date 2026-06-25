using System.Windows.Controls;
using System.Windows.Input;
using Vcs.Desktop.ViewModels;

namespace Vcs.Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void PasswordInput_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = PasswordInput.Password;
        }
    }

    private void PasswordInput_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.IsPasswordFocused = true;
        }
    }

    private void PasswordInput_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.IsPasswordFocused = false;
        }
    }

    private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter
            || DataContext is not LoginViewModel viewModel
            || !viewModel.LoginCommand.CanExecute(null))
        {
            return;
        }

        viewModel.LoginCommand.Execute(null);
        e.Handled = true;
    }
}
