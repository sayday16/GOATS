using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;

namespace LogWindowUI.ViewModels
{
    public class ShellViewModel : INotifyPropertyChanged
    {
        private readonly IProductApiClient _productApiClient;
        private readonly ISaleApiClient _saleApiClient;
        private readonly IReportApiClient _reportApiClient;
        private readonly AuthResponseDto? _authResponse;

        private object? _currentView;
        private bool _isInventoryActive = true;
        private bool _isPosActive = false;
        private bool _isReturnsActive = false;
        private bool _isAnalyticsActive = false;

        private InventoryViewModel? _inventoryViewModel;
        private MainViewModel? _posViewModel;
        private ReturnsViewModel? _returnsViewModel;
        private AnalyticsViewModel? _analyticsViewModel;

        public event Action? RequestClose;

        public object? CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public bool IsInventoryActive
        {
            get => _isInventoryActive;
            set { _isInventoryActive = value; OnPropertyChanged(); }
        }

        public bool IsPosActive
        {
            get => _isPosActive;
            set { _isPosActive = value; OnPropertyChanged(); }
        }

        public bool IsReturnsActive
        {
            get => _isReturnsActive;
            set { _isReturnsActive = value; OnPropertyChanged(); }
        }

        public bool IsAnalyticsActive
        {
            get => _isAnalyticsActive;
            set { _isAnalyticsActive = value; OnPropertyChanged(); }
        }

        public bool IsAdmin => _authResponse == null || _authResponse.Role != GoatStore.DataAccess.Entities.UserRole.Seller;

        public string UserEmail => _authResponse?.Email ?? "-";
        public string UserRole => _authResponse?.Role?.ToString() ?? "-";
        public string StoreName => _authResponse?.StoreName ?? "GOAT Store";

        public ICommand ShowInventoryCommand { get; }
        public ICommand ShowPosCommand { get; }
        public ICommand ShowReturnsCommand { get; }
        public ICommand ShowAnalyticsCommand { get; }
        public ICommand LogoutCommand { get; }

        public ShellViewModel(
            AuthResponseDto? authResponse,
            IProductApiClient? productApiClient = null,
            ISaleApiClient? saleApiClient = null,
            IReportApiClient? reportApiClient = null)
        {
            _authResponse = authResponse;
            _productApiClient = productApiClient ?? App.CreateProductApiClient();
            _saleApiClient = saleApiClient ?? App.CreateSaleApiClient();
            _reportApiClient = reportApiClient ?? App.CreateReportApiClient();

            ShowInventoryCommand = new RelayCommand(_ => NavigateToInventory());
            ShowPosCommand = new RelayCommand(_ => NavigateToPos());
            ShowReturnsCommand = new RelayCommand(_ => NavigateToReturns());
            ShowAnalyticsCommand = new RelayCommand(_ => NavigateToAnalytics());
            LogoutCommand = new RelayCommand(_ => Logout());

            if (IsAdmin)
                NavigateToInventory();
            else
                NavigateToPos();
        }

        private void NavigateToInventory()
        {
            IsInventoryActive = true;
            IsPosActive = false;
            IsReturnsActive = false;
            IsAnalyticsActive = false;

            if (_inventoryViewModel == null)
                _inventoryViewModel = new InventoryViewModel(_productApiClient, _saleApiClient);
            else
                _inventoryViewModel.Refresh();

            CurrentView = _inventoryViewModel;
        }

        private void NavigateToPos()
        {
            IsInventoryActive = false;
            IsPosActive = true;
            IsReturnsActive = false;
            IsAnalyticsActive = false;

            _posViewModel ??= new MainViewModel(_authResponse, _productApiClient, new WpfDialogService(), _saleApiClient);

            CurrentView = _posViewModel;
        }

        private void NavigateToReturns()
        {
            IsInventoryActive = false;
            IsPosActive = false;
            IsReturnsActive = true;
            IsAnalyticsActive = false;

            _returnsViewModel ??= new ReturnsViewModel(_saleApiClient, new WpfDialogService());

            CurrentView = _returnsViewModel;
        }

        private void NavigateToAnalytics()
        {
            IsInventoryActive = false;
            IsPosActive = false;
            IsReturnsActive = false;
            IsAnalyticsActive = true;

            if (_analyticsViewModel == null)
                _analyticsViewModel = new AnalyticsViewModel(_reportApiClient);
            else
                _analyticsViewModel.Refresh();

            CurrentView = _analyticsViewModel;
        }

        private void Logout()
        {
            AuthSession.Clear();
            RequestClose?.Invoke();
            if (Application.Current != null)
            {
                var loginView = new Views.LoginView
                {
                    DataContext = new LoginViewModel(App.CreateAuthService())
                };
                loginView.Show();
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext == this)
                    {
                        window.Close();
                        break;
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
