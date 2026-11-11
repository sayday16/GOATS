using System.Windows;
using GoatStore.Core.DTOs;
using LogWindowUI.Services;
using LogWindowUI.ViewModels;

namespace LogWindowUI
{
    public partial class MainWindow : Window
    {
        public MainWindow(AuthResponseDto? authResponse) : this(authResponse, App.CreateProductApiClient())
        {
        }

        public MainWindow(AuthResponseDto? authResponse, IProductApiClient productApiClient)
        {
            InitializeComponent();

            DataContext = new MainViewModel(authResponse, productApiClient, new WpfDialogService());
        }
    }
}