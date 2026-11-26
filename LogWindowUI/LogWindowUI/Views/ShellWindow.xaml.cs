using System.Windows;
using GoatStore.Core.DTOs;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Views
{
    public partial class ShellWindow : Window
    {
        public ShellWindow(AuthResponseDto? authResponse = null)
        {
            InitializeComponent();
            var viewModel = new ShellViewModel(authResponse);
            viewModel.RequestClose += ReturnToLogin;
            DataContext = viewModel;
        }

        private void ReturnToLogin()
        {
            var loginView = new LoginView
            {
                DataContext = new LoginViewModel(App.CreateAuthService())
            };
            loginView.Show();
            Close();
        }
    }
}
