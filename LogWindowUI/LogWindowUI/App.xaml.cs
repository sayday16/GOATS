using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using GoatStore.Core.Interfaces;
using LogWindowUI.ViewModels;
using LogWindowUI.Views;
using LogWindowUI.Services;

namespace LogWindowUI
{
    public partial class App : Application
    {
        private const string DefaultApiUrl = "http://localhost:5585";

        private static readonly HttpClient ApiHttpClient = new(new BearerTokenHandler())
        {
            BaseAddress = new Uri(
                Environment.GetEnvironmentVariable("GOATSTORE_API_URL") ?? DefaultApiUrl)
        };

        public static IAuthService CreateAuthService() => new AuthApiClient(ApiHttpClient);

        public static IProductApiClient CreateProductApiClient() => new ProductApiClient(ApiHttpClient);

        public static ISaleApiClient CreateSaleApiClient() => new SaleApiClient(ApiHttpClient);

        public static IReportApiClient CreateReportApiClient() => new ReportApiClient(ApiHttpClient);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppLog.Start(ApiHttpClient.BaseAddress!.ToString());
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception, application is terminating");
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };

            IAuthService authService = CreateAuthService();
            var loginViewModel = new LoginViewModel(authService);
            var loginWindow = new LoginView
            {
                DataContext = loginViewModel
            };

            loginWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLog.Stop();
            base.OnExit(e);
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled UI exception");
            MessageBox.Show(AppLog.UnexpectedErrorMessage, "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}