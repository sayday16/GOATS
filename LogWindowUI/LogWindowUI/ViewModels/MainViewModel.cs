using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GoatStore.Core.DTOs;

namespace LogWindowUI.ViewModels
{
    public class CartItemViewModel : INotifyPropertyChanged
    {
        private int _quantity = 1;

        public string Title { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
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
                    OnPropertyChanged(nameof(TotalPriceText));
                }
            }
        }

        public decimal TotalPrice => UnitPrice * Quantity;
        public string TotalPriceText => $"{TotalPrice:N0} ₴";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<CartItemViewModel> _cartItems = new();
        private bool _isCardPayment = true;
        private bool _isCashPayment = false;
        private decimal _discount = 398m;
        private string _searchText = string.Empty;
        private string _cashierName = "Олена Коваль";
        private string _shiftInfo = "З 09:00 · 6 год 24 хв";
        private string _storeLocation = "Київ · Центр";
        private string _cashRegister = "Каса №2 · онлайн";
        private string _receiptNumber = "Чек #000184 · 1 жовтня, 15:24";

        public ObservableCollection<CartItemViewModel> CartItems
        {
            get => _cartItems;
            set
            {
                _cartItems = value;
                OnPropertyChanged();
                RecalculateTotals();
            }
        }

        public string CashierName
        {
            get => _cashierName;
            set { _cashierName = value; OnPropertyChanged(); }
        }

        public string ShiftInfo
        {
            get => _shiftInfo;
            set { _shiftInfo = value; OnPropertyChanged(); }
        }

        public string StoreLocation
        {
            get => _storeLocation;
            set { _storeLocation = value; OnPropertyChanged(); }
        }

        public string CashRegister
        {
            get => _cashRegister;
            set { _cashRegister = value; OnPropertyChanged(); }
        }

        public string ReceiptNumber
        {
            get => _receiptNumber;
            set { _receiptNumber = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public bool IsCardPayment
        {
            get => _isCardPayment;
            set
            {
                if (_isCardPayment != value)
                {
                    _isCardPayment = value;
                    OnPropertyChanged();
                    if (value) IsCashPayment = false;
                }
            }
        }

        public bool IsCashPayment
        {
            get => _isCashPayment;
            set
            {
                if (_isCashPayment != value)
                {
                    _isCashPayment = value;
                    OnPropertyChanged();
                    if (value) IsCardPayment = false;
                }
            }
        }

        public int TotalItemsCount => CartItems.Sum(i => i.Quantity);
        public string TotalItemsCountText => $"{TotalItemsCount} товарів";

        public decimal Subtotal => CartItems.Sum(i => i.TotalPrice);
        public string SubtotalText => $"{Subtotal:N0} ₴";

        public decimal Discount
        {
            get => _discount;
            set
            {
                _discount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiscountText));
                OnPropertyChanged(nameof(TotalPayable));
                OnPropertyChanged(nameof(TotalPayableText));
            }
        }

        public string DiscountText => $"-{Discount:N0} ₴";

        public decimal TotalPayable => Math.Max(0, Subtotal - Discount);
        public string TotalPayableText => $"{TotalPayable:N0} ₴";

        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand SelectCardCommand { get; }
        public ICommand SelectCashCommand { get; }
        public ICommand CheckoutCommand { get; }

        public MainViewModel() : this(null)
        {
        }

        public MainViewModel(AuthResponseDto? authResponse)
        {
            if (authResponse != null && !string.IsNullOrWhiteSpace(authResponse.Email))
            {
                var emailPrefix = authResponse.Email.Split('@')[0];
                var nameParts = emailPrefix.Split('.');
                if (nameParts.Length >= 2)
                {
                    CashierName = $"{char.ToUpper(nameParts[0][0])}{nameParts[0].Substring(1)} {char.ToUpper(nameParts[1][0])}{nameParts[1].Substring(1)}";
                }
                else
                {
                    CashierName = char.ToUpper(emailPrefix[0]) + emailPrefix.Substring(1);
                }

                if (!string.IsNullOrWhiteSpace(authResponse.StoreName))
                {
                    StoreLocation = authResponse.StoreName;
                }
            }

            IncreaseQuantityCommand = new RelayCommand(p =>
            {
                if (p is CartItemViewModel item)
                {
                    item.Quantity++;
                    RecalculateTotals();
                }
            });

            DecreaseQuantityCommand = new RelayCommand(p =>
            {
                if (p is CartItemViewModel item && item.Quantity > 1)
                {
                    item.Quantity--;
                    RecalculateTotals();
                }
            });

            RemoveItemCommand = new RelayCommand(p =>
            {
                if (p is CartItemViewModel item)
                {
                    CartItems.Remove(item);
                    RecalculateTotals();
                }
            });

            ClearCartCommand = new RelayCommand(_ =>
            {
                CartItems.Clear();
                Discount = 0;
                RecalculateTotals();
            });

            SelectCardCommand = new RelayCommand(_ =>
            {
                IsCardPayment = true;
                IsCashPayment = false;
            });

            SelectCashCommand = new RelayCommand(_ =>
            {
                IsCashPayment = true;
                IsCardPayment = false;
            });

            CheckoutCommand = new RelayCommand(_ =>
            {
                if (CartItems.Count == 0)
                {
                    MessageBox.Show("Чек порожній!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var paymentMethod = IsCardPayment ? "Картка" : "Готівка";
                MessageBox.Show($"Чек успішно сформовано!\nСума: {TotalPayableText}\nОплата: {paymentMethod}", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            });

            LoadInitialCart();
        }

        private void LoadInitialCart()
        {
            CartItems = new ObservableCollection<CartItemViewModel>
            {
                new CartItemViewModel
                {
                    Title = "Футболка Basic Heavy",
                    Sku = "TS-1048-BLK",
                    Category = "Чоловіче",
                    Tag = "Чорний · L",
                    UnitPrice = 1199m,
                    Quantity = 2,
                    IconKind = "Tshirt"
                },
                new CartItemViewModel
                {
                    Title = "Джинси Straight Fit",
                    Sku = "JN-2024-BLU",
                    Category = "Жіноче",
                    Tag = "Синій денім · 28",
                    UnitPrice = 2899m,
                    Quantity = 1,
                    IconKind = "Pants"
                },
                new CartItemViewModel
                {
                    Title = "Худі Oversize Core",
                    Sku = "HD-7710-GRY",
                    Category = "Унісекс",
                    Tag = "Сірий меланж · M",
                    UnitPrice = 2199m,
                    Quantity = 1,
                    IconKind = "Hoodie"
                },
                new CartItemViewModel
                {
                    Title = "Куртка Utility Light",
                    Sku = "JK-4402-KHK",
                    Category = "Чоловіче",
                    Tag = "Хакі · L",
                    UnitPrice = 3790m,
                    Quantity = 1,
                    IconKind = "Jacket"
                },
                new CartItemViewModel
                {
                    Title = "Шкарпетки Rib, 3 пари",
                    Sku = "SK-0931-WHT",
                    Category = "Аксесуари",
                    Tag = "Білий · 39-42",
                    UnitPrice = 499m,
                    Quantity = 1,
                    IconKind = "Socks"
                }
            };

            Discount = 398m;
            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
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
