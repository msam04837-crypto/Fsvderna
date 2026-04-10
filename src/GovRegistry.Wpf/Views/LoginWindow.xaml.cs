using GovRegistry.Core;
using GovRegistry.Wpf.ViewModels;
using System.Windows;

namespace GovRegistry.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow()
    {
        InitializeComponent();
        var services = new AppServices();
        _vm = new LoginViewModel(services);
        _vm.LoginSucceeded += user => OpenMain(user, services);
        DataContext = _vm;
    }

    private void PasswordInput_OnPasswordChanged(object sender, RoutedEventArgs e) => _vm.Password = PasswordInput.Password;

    private void OpenMain(AppUser user, AppServices services)
    {
        var main = new MainWindow { DataContext = new MainViewModel(services, user) };
        main.Show();
        Close();
    }
}
