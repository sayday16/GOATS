using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;

namespace LogWindowUI.ViewModels
{
    public class CartItemViewModel : INotifyPropertyChanged
    {
        private int _quantity = 1;

        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Condition { get; set; } = "New";
        public string Sku { get; set; } = string.Empty;
        public decimal SalePrice { get; set; }
        public string Status { get; set; } = "InStock";
        public bool IsUnique { get; set; } = true;
        public int MaxQuantity { get; set; } = 1;
        public bool CanChangeQuantity => !IsUnique;
        public string IconKind { get; set; } = "Tshirt";

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value && value >= 1)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPrice));
                    OnPropertyChanged(nameof(TotalPriceText));
                }
            }
        }

        public decimal TotalPrice => SalePrice * Quantity;
        public string TotalPriceText => $"{TotalPrice:N0} ₴";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IProductApiClient _productApiClient;
        private readonly ISaleApiClient _saleApiClient;
        private readonly IDialogService _dialogService;

        private ObservableCollection<CartItemViewModel> _cartItems = new();
        private bool _isCardPayment = true;
        private bool _isCashPayment = false;
        private bool _isCheckingOut = false;
        private bool _isReturnsTabActive = false;
        private decimal _discount = 0m;
        private string _discountInput = string.Empty;
        private string _searchText = string.Empty;
        private string _cashierName = "Олена Коваль";
        private string _shiftInfo = "З 09:00 · 6 год 24 хв";
        private string _storeLocation = "Київ · Центр";
        private string _cashRegister = "Каса №2 · онлайн";
        private string _receiptNumber = "Новий чек";

        public ObservableCollection<CartItemViewModel> CartItems
        {
            get => _cartItems;
            set
            {
                if (_cartItems != null)
                {
                    _cartItems.CollectionChanged -= CartItems_CollectionChanged;
                    foreach (var item in _cartItems) item.PropertyChanged -= CartItem_PropertyChanged;
                }

                _cartItems = value ?? new ObservableCollection<CartItemViewModel>();

                _cartItems.CollectionChanged += CartItems_CollectionChanged;
                foreach (var item in _cartItems) item.PropertyChanged += CartItem_PropertyChanged;

                OnPropertyChanged();
                RecalculateTotals();
            }
        }


        public ReturnsViewModel ReturnsViewModel { get; }

        public bool IsReturnsTabActive
        {
            get => _isReturnsTabActive;
            set
            {
                if (_isReturnsTabActive != value)
                {
                    _isReturnsTabActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPosTabActive));
                }
            }
        }

        public bool IsPosTabActive => !IsReturnsTabActive;

        public ICommand ShowPosTabCommand { get; }
        public ICommand ShowReturnsTabCommand { get; }

        private void CartItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (CartItemViewModel item in e.OldItems) item.PropertyChanged -= CartItem_PropertyChanged;
            if (e.NewItems != null)
                foreach (CartItemViewModel item in e.NewItems) item.PropertyChanged += CartItem_PropertyChanged;
            RecalculateTotals();
        }

        private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CartItemViewModel.Quantity) or nameof(CartItemViewModel.TotalPrice))
                RecalculateTotals();
        }

        public string CashierName { get => _cashierName; set { _cashierName = value; OnPropertyChanged(); } }
        public string ShiftInfo { get => _shiftInfo; set { _shiftInfo = value; OnPropertyChanged(); } }
        public string StoreLocation { get => _storeLocation; set { _storeLocation = value; OnPropertyChanged(); } }
        public string CashRegister { get => _cashRegister; set { _cashRegister = value; OnPropertyChanged(); } }
        public string ReceiptNumber { get => _receiptNumber; set { _receiptNumber = value; OnPropertyChanged(); } }
        public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); } }
        public string DiscountInput { get => _discountInput; set { _discountInput = value; OnPropertyChanged(); } }

        public bool IsCardPayment
        {
            get => _isCardPayment;
            set { if (_isCardPayment != value) { _isCardPayment = value; OnPropertyChanged(); if (value) IsCashPayment = false; } }
        }

        public bool IsCashPayment
        {
            get => _isCashPayment;
            set { if (_isCashPayment != value) { _isCashPayment = value; OnPropertyChanged(); if (value) IsCardPayment = false; } }
        }

        public int TotalItemsCount
        {
            get
            {
                int total = 0;
                foreach (var item in CartItems) total += item.Quantity;
                return total;
            }
        }

        public string TotalItemsCountText => $"{TotalItemsCount} {Pluralize(TotalItemsCount, "товар", "товари", "товарів")}";

        private static string Pluralize(int n, string one, string few, string many)
        {
            if (n % 100 is >= 11 and <= 14) return many;
            return (n % 10) switch { 1 => one, 2 or 3 or 4 => few, _ => many };
        }

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        public decimal Subtotal
        {
            get
            {
                decimal total = 0m;
                foreach (var item in CartItems) total += item.TotalPrice;
                return total;
            }
        }

        public string SubtotalText => $"{Subtotal:N0} ₴";

        public decimal Discount
        {
            get => _discount;
            set
            {
                _discount = Math.Max(0m, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiscountText));
                OnPropertyChanged(nameof(TotalPayable));
                OnPropertyChanged(nameof(TotalPayableText));
            }
        }

        public string DiscountText => Discount > 0 ? $"-{Discount:N0} ₴" : "0 ₴";
        public decimal TotalPayable => Math.Max(0, Subtotal - Discount);
        public string TotalPayableText => $"{TotalPayable:N0} ₴";

        public ICommand RemoveItemCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand SelectCardCommand { get; }
        public ICommand SelectCashCommand { get; }
        public ICommand ApplyDiscountCommand { get; }
        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand SearchCommand { get; }

        public MainViewModel(
            AuthResponseDto? authResponse,
            IProductApiClient productApiClient,
            IDialogService dialogService,
            ISaleApiClient? saleApiClient = null)
        {
            _productApiClient = productApiClient;
            _dialogService = dialogService;
            _saleApiClient = saleApiClient ?? App.CreateSaleApiClient();
            ReturnsViewModel = new ReturnsViewModel(_saleApiClient, _dialogService);
            ShowPosTabCommand = new RelayCommand(_ => IsReturnsTabActive = false);
            ShowReturnsTabCommand = new RelayCommand(_ => IsReturnsTabActive = true);

            if (authResponse != null && !string.IsNullOrWhiteSpace(authResponse.Email))
            {
                var emailPrefix = authResponse.Email.Trim().Split('@')[0];
                var parts = emailPrefix.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    CashierName = $"{Capitalize(parts[0])} {Capitalize(parts[1])}";
                else if (parts.Length == 1)
                    CashierName = Capitalize(parts[0]);
            }

            if (authResponse != null && !string.IsNullOrWhiteSpace(authResponse.StoreName))
                StoreLocation = authResponse.StoreName;

            RemoveItemCommand = new RelayCommand(p => { if (p is CartItemViewModel item) CartItems.Remove(item); });

            ClearCartCommand = new RelayCommand(_ =>
            {
                CartItems.Clear();
                Discount = 0m;
                DiscountInput = string.Empty;
            });

            SelectCardCommand = new RelayCommand(_ => { IsCardPayment = true; IsCashPayment = false; });
            SelectCashCommand = new RelayCommand(_ => { IsCashPayment = true; IsCardPayment = false; });
            ApplyDiscountCommand = new RelayCommand(_ => ApplyDiscount());

            IncreaseQuantityCommand = new RelayCommand(p =>
            {
                if (p is CartItemViewModel item && !item.IsUnique && item.Quantity < item.MaxQuantity)
                    item.Quantity++;
            });

            DecreaseQuantityCommand = new RelayCommand(p =>
            {
                if (p is CartItemViewModel item && !item.IsUnique && item.Quantity > 1)
                    item.Quantity--;
            });

            SearchCommand = new RelayCommand(async _ => await SearchProductAsync());
            CheckoutCommand = new RelayCommand(async _ => await ProcessCheckoutAsync());

            CartItems = new ObservableCollection<CartItemViewModel>();
        }

        private void ApplyDiscount()
        {
            if (CartItems.Count == 0)
            {
                _dialogService.ShowWarning("Знижка", "Неможливо застосувати знижку до порожнього чека!");
                return;
            }

            string normalized = (DiscountInput ?? string.Empty).Trim().Replace(',', '.');
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedDiscount))
            {
                _dialogService.ShowWarning("Знижка", "Введіть коректну суму знижки!");
                return;
            }

            if (parsedDiscount < 0m)
            {
                _dialogService.ShowWarning("Знижка", "Знижка не може бути від'ємною!");
                return;
            }

            if (parsedDiscount > Subtotal)
            {
                _dialogService.ShowWarning("Знижка", "Знижка не може перевищувати суму чека!");
                return;
            }

            Discount = parsedDiscount;
        }

        private async Task SearchProductAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            try
            {
                var query = SearchText.Trim();
                var filter = new ProductFilterDto { Search = query, Status = ProductStatus.InStock };
                var results = await _productApiClient.GetAllAsync(filter);

                if (results.Count == 0)
                {
                    _dialogService.ShowWarning("Пошук", "Товар не знайдено, або він вже проданий!");
                    return;
                }

                ProductDto? productDto = null;
                foreach (var p in results)
                {
                    if (string.Equals(p.Sku, query, StringComparison.OrdinalIgnoreCase))
                    {
                        productDto = p;
                        break;
                    }
                }

                if (productDto == null && results.Count > 1)
                {
                    ProductDto? exactTitleMatch = null;
                    int exactTitleCount = 0;

                    foreach (var p in results)
                    {
                        if (string.Equals((p.Title ?? string.Empty).Trim(), query, StringComparison.OrdinalIgnoreCase))
                        {
                            exactTitleMatch = p;
                            exactTitleCount++;
                        }
                    }

                    if (exactTitleCount == 1)
                        productDto = exactTitleMatch;
                }

                if (productDto == null && results.Count > 1)
                {
                    _dialogService.ShowInfo("Пошук", "Знайдено кілька товарів. Введіть точний артикул!");
                    return;
                }

                productDto ??= results[0];

                bool alreadyInCart = false;
                foreach (var item in CartItems)
                {
                    if (item.Id == productDto.Id)
                    {
                        alreadyInCart = true;
                        break;
                    }
                }

                if (alreadyInCart)
                {
                    _dialogService.ShowInfo("Увага", "Цей товар вже додано в чек!");
                    return;
                }

                CartItems.Add(new CartItemViewModel
                {
                    Id = productDto.Id,
                    Title = productDto.Title,
                    Brand = productDto.Brand,
                    Size = productDto.Size,
                    Category = productDto.Category,
                    Condition = productDto.Condition.ToString(),
                    Sku = productDto.Sku ?? string.Empty,
                    SalePrice = productDto.SalePrice,
                    Status = productDto.Status.ToString(),
                    IsUnique = productDto.IsUnique,
                    MaxQuantity = productDto.IsUnique ? 1 : Math.Max(1, productDto.Quantity),
                    IconKind = GetIconForCategory(productDto.Category)
                });

                SearchText = string.Empty;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Помилка", $"Помилка зв'язку з сервером: {ex.Message}");
            }
        }

        private async Task ProcessCheckoutAsync()
        {
            if (CartItems.Count == 0)
            {
                _dialogService.ShowWarning("Помилка", "Чек порожній!");
                return;
            }

            if (Discount > Subtotal)
            {
                _dialogService.ShowWarning("Знижка", "Знижка не може перевищувати суму чека!");
                return;
            }

            if (_isCheckingOut) return;
            _isCheckingOut = true;

            try
            {
                var saleItems = new List<CreateSaleItemDto>();
                foreach (var item in CartItems)
                {
                    saleItems.Add(new CreateSaleItemDto
                    {
                        ProductId = item.Id,
                        Quantity = item.Quantity
                    });
                }

                var request = new CreateSaleRequestDto
                {
                    Items = saleItems,
                    PaymentMethod = IsCardPayment ? PaymentMethod.Card : PaymentMethod.Cash,
                    Discount = Discount
                };

                var response = await _saleApiClient.CreateAsync(request);

                if (!response.IsSuccess)
                {
                    string errorMessage = string.IsNullOrWhiteSpace(response.Message)
                        ? "Не вдалося сформувати чек."
                        : response.Message;
                    _dialogService.ShowError("Помилка", errorMessage);
                    return;
                }

                int saleNumber = response.Sale?.Number ?? 0;
                string finalTotalText = response.Sale is { Total: > 0 }
                    ? $"{response.Sale.Total:N0} ₴"
                    : TotalPayableText;
                string paymentMethod = IsCardPayment ? "Картка" : "Готівка";

                ReceiptNumber = $"Чек #{saleNumber}";
                CartItems.Clear();
                Discount = 0m;
                DiscountInput = string.Empty;

                _dialogService.ShowInfo("Успіх", $"Чек #{saleNumber} успішно сформовано!\nСума: {finalTotalText}\nОплата: {paymentMethod}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Помилка", $"Виникла помилка під час оплати: {ex.Message}");
            }
            finally
            {
                _isCheckingOut = false;
            }
        }

        private string GetIconForCategory(string category)
        {
            return category?.ToLower() switch
            {
                "одяг" => "Tshirt",
                "взуття" => "ShoeSneaker",
                "аксесуари" => "Watch",
                "верхній одяг" => "Jacket",
                _ => "Tag"
            };
        }

        private void RecalculateTotals()
        {
            if (CartItems.Count == 0 && Discount != 0m)
            {
                Discount = 0m;
                DiscountInput = string.Empty;
            }

            OnPropertyChanged(nameof(TotalItemsCount));
            OnPropertyChanged(nameof(TotalItemsCountText));
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(SubtotalText));
            OnPropertyChanged(nameof(DiscountText));
            OnPropertyChanged(nameof(TotalPayable));
            OnPropertyChanged(nameof(TotalPayableText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}