using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;
using LogWindowUI.Views;

namespace LogWindowUI.ViewModels
{
    public class InventoryViewModel : INotifyPropertyChanged
    {
        private readonly IProductApiClient _productApiClient;
        private readonly ISaleApiClient _saleApiClient;

        private ObservableCollection<ProductDto> _products = new();
        private string? _searchText;
        private string? _selectedCategory;
        private ObservableCollection<string> _categories = new();
        private bool _isLoading;
        private string? _errorMessage;

        public ObservableCollection<ProductDto> Products
        {
            get => _products;
            set { _products = value; OnPropertyChanged(); }
        }

        public string? SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public string? SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Categories
        {
            get => _categories;
            set { _categories = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

        public ICommand SearchCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SellCommand { get; }

        public InventoryViewModel(IProductApiClient productApiClient, ISaleApiClient? saleApiClient = null)
        {
            _productApiClient = productApiClient;
            _saleApiClient = saleApiClient ?? App.CreateSaleApiClient();

            SearchCommand = new RelayCommand(_ => LoadProductsAsync());
            ResetCommand = new RelayCommand(_ => ResetFilter());
            AddProductCommand = new RelayCommand(_ => OpenAddProduct());
            RefreshCommand = new RelayCommand(_ => LoadProductsAsync());
            SellCommand = new RelayCommand(async p =>
            {
                if (p is ProductDto product)
                    await SellProductAsync(product);
            }, p => p is ProductDto dto && dto.Status == ProductStatus.InStock);

            LoadProductsAsync();
            LoadFilterOptionsAsync();
        }

        public void Refresh() => LoadProductsAsync();

        private async void LoadProductsAsync()
        {
            IsLoading = true;
            ErrorMessage = null;
            try
            {
                var filter = new ProductFilterDto
                {
                    Search = SearchText,
                    Category = SelectedCategory == "(Всі)" ? null : SelectedCategory
                };
                var list = await _productApiClient.GetAllAsync(filter);
                Products = new ObservableCollection<ProductDto>(list);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Помилка завантаження: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void LoadFilterOptionsAsync()
        {
            try
            {
                var options = await _productApiClient.GetFilterOptionsAsync();
                Categories.Clear();
                Categories.Add("(Всі)");
                foreach (var c in options.Categories)
                    Categories.Add(c);
                SelectedCategory = "(Всі)";
            }
            catch
            {
                Categories.Clear();
                foreach (var c in new[] { "(Всі)", "Одяг", "Взуття", "Аксесуари", "Верхній одяг" })
                    Categories.Add(c);
                SelectedCategory = "(Всі)";
            }
        }

        private void ResetFilter()
        {
            SearchText = null;
            SelectedCategory = "(Всі)";
            LoadProductsAsync();
        }

        private void OpenAddProduct()
        {
            var vm = new ProductEditViewModel(_productApiClient);
            var dialog = new ProductEditView { DataContext = vm };
            vm.RequestClose += (saved) =>
            {
                dialog.Close();
                if (saved) LoadProductsAsync();
            };
            dialog.ShowDialog();
        }

        private async System.Threading.Tasks.Task SellProductAsync(ProductDto product)
        {
            IsLoading = true;
            ErrorMessage = null;
            try
            {
                var request = new CreateSaleRequestDto
                {
                    Items = [new CreateSaleItemDto { ProductId = product.Id, Quantity = 1 }],
                    PaymentMethod = PaymentMethod.Card,
                    Discount = 0
                };
                var result = await _saleApiClient.CreateAsync(request);
                if (result.IsSuccess)
                {
                    LoadProductsAsync();
                }
                else
                {
                    ErrorMessage = result.Message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Помилка продажу: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
