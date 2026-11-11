using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;

namespace LogWindowUI.ViewModels
{
    public class ProductEditViewModel : INotifyPropertyChanged
    {
        private readonly IProductApiClient _productApiClient;

        public event Action<bool>? RequestClose;

        private string _title = string.Empty;
        private string _brand = string.Empty;
        private string _size = string.Empty;
        private string? _selectedCategory;
        private string? _selectedCondition;
        private string? _sku;
        private string _purchasePrice = string.Empty;
        private string _salePrice = string.Empty;
        private string? _errorMessage;
        private bool _isLoading;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); InvalidateCommands(); }
        }

        public string Brand
        {
            get => _brand;
            set { _brand = value; OnPropertyChanged(); InvalidateCommands(); }
        }

        public string Size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(); InvalidateCommands(); }
        }

        public string? SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); InvalidateCommands(); }
        }

        public string? SelectedCondition
        {
            get => _selectedCondition;
            set { _selectedCondition = value; OnPropertyChanged(); }
        }

        public string? Sku
        {
            get => _sku;
            set { _sku = value; OnPropertyChanged(); }
        }

        public string PurchasePrice
        {
            get => _purchasePrice;
            set { _purchasePrice = value; OnPropertyChanged(); InvalidateCommands(); }
        }

        public string SalePrice
        {
            get => _salePrice;
            set { _salePrice = value; OnPropertyChanged(); InvalidateCommands(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); InvalidateCommands(); }
        }

        public ObservableCollection<string> Categories { get; } = new()
        {
            "Одяг", "Взуття", "Аксесуари", "Верхній одяг"
        };

        public ObservableCollection<string> Conditions { get; } = new()
        {
            "Новий", "Вживаний"
        };

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public ProductEditViewModel(IProductApiClient productApiClient)
        {
            _productApiClient = productApiClient;
            SelectedCondition = "Новий";
            SelectedCategory = "Одяг";

            SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        private static bool TryParsePrice(string? text, out decimal price)
        {
            price = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.Trim().Replace(',', '.');
            return decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out price) && price >= 0;
        }

        private bool CanSave()
        {
            if (IsLoading) return false;
            if (string.IsNullOrWhiteSpace(Title)) return false;
            if (string.IsNullOrWhiteSpace(Brand)) return false;
            if (string.IsNullOrWhiteSpace(Size)) return false;
            if (string.IsNullOrWhiteSpace(SelectedCategory)) return false;
            if (!TryParsePrice(PurchasePrice, out _)) return false;
            if (!TryParsePrice(SalePrice, out _)) return false;
            return true;
        }

        private async System.Threading.Tasks.Task ExecuteSaveAsync()
        {
            if (!CanSave()) return;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                TryParsePrice(PurchasePrice, out decimal purchase);
                TryParsePrice(SalePrice, out decimal sale);

                var request = new SaveProductRequestDto
                {
                    Title = Title.Trim(),
                    Brand = Brand.Trim(),
                    Size = Size.Trim(),
                    Category = SelectedCategory!,
                    Condition = SelectedCondition == "Вживаний" ? ProductCondition.Used : ProductCondition.New,
                    Sku = string.IsNullOrWhiteSpace(Sku) ? null : Sku.Trim(),
                    PurchasePrice = purchase,
                    SalePrice = sale
                };

                var result = await _productApiClient.CreateAsync(request);

                if (result.IsSuccess)
                    RequestClose?.Invoke(true);
                else
                    ErrorMessage = result.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Помилка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void InvalidateCommands()
            => CommandManager.InvalidateRequerySuggested();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
