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
        private readonly AuthResponseDto? _authResponse;

        private object? _currentView;
        private bool _isInventoryActive = true;
        private bool _isPosActive = false;

        private InventoryViewModel? _inventoryViewModel;
        private MainViewModel? _posViewModel;

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

        public string UserEmail => _authResponse?.Email ?? "-";
        public string UserRole => _authResponse?.Role?.ToString() ?? "-";
        public string StoreName => _authResponse?.StoreName ?? "GOAT Store";

        public ICommand ShowInventoryCommand { get; }
        public ICommand ShowPosCommand { get; }

        public ShellViewModel(AuthResponseDto? authResponse, IProductApiClient? productApiClient = null)
        {
            _authResponse = authResponse;
            _productApiClient = productApiClient ?? App.CreateProductApiClient();

            ShowInventoryCommand = new RelayCommand(_ => NavigateToInventory());
            ShowPosCommand = new RelayCommand(_ => NavigateToPos());

            NavigateToInventory();
        }

        private void NavigateToInventory()
        {
            IsInventoryActive = true;
            IsPosActive = false;

            if (_inventoryViewModel == null)
                _inventoryViewModel = new InventoryViewModel(_productApiClient);

            CurrentView = _inventoryViewModel;
        }

        private void NavigateToPos()
        {
            IsInventoryActive = false;
            IsPosActive = true;
            
            _posViewModel ??= new MainViewModel(_authResponse, _productApiClient, new WpfDialogService());

            CurrentView = _posViewModel;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
