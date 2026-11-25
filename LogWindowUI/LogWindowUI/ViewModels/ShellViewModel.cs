using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using LogWindowUI.Services;

namespace LogWindowUI.ViewModels
{
    public class ShellViewModel : INotifyPropertyChanged
    {
        private readonly IProductApiClient _productApiClient;
        private readonly ISaleApiClient _saleApiClient;
        private readonly AuthResponseDto? _authResponse;

        private object? _currentView;
        private bool _isInventoryActive = true;
        private bool _isPosActive = false;
        private bool _isReturnsActive = false;

        private InventoryViewModel? _inventoryViewModel;
        private MainViewModel? _posViewModel;
        private ReturnsViewModel? _returnsViewModel;

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

        public string UserEmail => _authResponse?.Email ?? "-";
        public string UserRole => _authResponse?.Role?.ToString() ?? "-";
        public string StoreName => _authResponse?.StoreName ?? "GOAT Store";

        public ICommand ShowInventoryCommand { get; }
        public ICommand ShowPosCommand { get; }
        public ICommand ShowReturnsCommand { get; }

        public ShellViewModel(
            AuthResponseDto? authResponse,
            IProductApiClient? productApiClient = null,
            ISaleApiClient? saleApiClient = null)
        {
            _authResponse = authResponse;
            _productApiClient = productApiClient ?? App.CreateProductApiClient();
            _saleApiClient = saleApiClient ?? App.CreateSaleApiClient();

            ShowInventoryCommand = new RelayCommand(_ => NavigateToInventory());
            ShowPosCommand = new RelayCommand(_ => NavigateToPos());
            ShowReturnsCommand = new RelayCommand(_ => NavigateToReturns());

            NavigateToInventory();
        }

        private void NavigateToInventory()
        {
            IsInventoryActive = true;
            IsPosActive = false;
            IsReturnsActive = false;

            if (_inventoryViewModel == null)
                _inventoryViewModel = new InventoryViewModel(_productApiClient);
            else
                _inventoryViewModel.Refresh();

            CurrentView = _inventoryViewModel;
        }

        private void NavigateToPos()
        {
            IsInventoryActive = false;
            IsPosActive = true;
            IsReturnsActive = false;

            _posViewModel ??= new MainViewModel(_authResponse, _productApiClient, new WpfDialogService(), _saleApiClient);

            CurrentView = _posViewModel;
        }

        private void NavigateToReturns()
        {
            IsInventoryActive = false;
            IsPosActive = false;
            IsReturnsActive = true;

            _returnsViewModel ??= new ReturnsViewModel(_saleApiClient, new WpfDialogService());

            CurrentView = _returnsViewModel;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}