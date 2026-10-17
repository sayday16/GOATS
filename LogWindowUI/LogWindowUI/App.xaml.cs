using System.Net.Http;
using System.Windows;
using GoatStore.Core.Interfaces;
using LogWindowUI.ViewModels;
using LogWindowUI.Views;
using LogWindowUI.Services;

namespace LogWindowUI
{
    public partial class App : Application
    {
        private const string DefaultApiUrl = "http://localhost:5585";

        private static readonly HttpClient ApiHttpClient = new()
        {
            BaseAddress = new Uri(
                Environment.GetEnvironmentVariable("GOATSTORE_API_URL") ?? DefaultApiUrl)
        };

        public static IAuthService CreateAuthService() => new AuthApiClient(ApiHttpClient);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            IAuthService authService = CreateAuthService();
            var loginViewModel = new LoginViewModel(authService);
            var loginWindow = new LoginView
            {
                DataContext = loginViewModel
            };

            loginWindow.Show();
        }
    }
}