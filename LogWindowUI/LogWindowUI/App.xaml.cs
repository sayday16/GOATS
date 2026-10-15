using System.Windows;
using GoatStore.Core.Interfaces;
using GoatStore.Core.Services;
using GoatStore.DataAccess;
using LogWindowUI.ViewModels;
using LogWindowUI.Views;
using Microsoft.EntityFrameworkCore;

namespace LogWindowUI
{
    public partial class App : Application
    {
        private const string ConnectionString =
            "Host=aws-1-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.thpnzowfynymufgscfst;Password=DubMykolaSrakaHola;SSL Mode=Require;Trust Server Certificate=true";

        public static IAuthService CreateAuthService()
        {
            var optionsBuilder = new DbContextOptionsBuilder<GoatStoreDbContext>();
            optionsBuilder.UseNpgsql(ConnectionString);
            var dbContext = new GoatStoreDbContext(optionsBuilder.Options);
            return new AuthService(dbContext);
        }

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