using GovRegistry.Core;
using GovRegistry.Wpf.Commands;
using System.Windows;

namespace GovRegistry.Wpf.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly AppServices _services;
    public event Action<AppUser>? LoginSucceeded;

    private string _username = "admin";
    public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

    private string _password = string.Empty;
    public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

    public RelayCommand LoginCommand { get; }

    public LoginViewModel(AppServices services)
    {
        _services = services;
        LoginCommand = new RelayCommand(Login);
    }

    private void Login()
    {
        var user = _services.AuthService.Authenticate(Username.Trim(), Password);
        if (user is null)
        {
            MessageBox.Show("بيانات الدخول غير صحيحة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoginSucceeded?.Invoke(user);
    }
}
