using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;

namespace LogWindowUI.ViewModels
{
    public class ReturnableItemViewModel : INotifyPropertyChanged
    {
        private int _quantityToReturn;

        public Guid SaleItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int PurchasedQuantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountRatio { get; set; } = 1m;

        public int AvailableToReturn => Math.Max(0, PurchasedQuantity - ReturnedQuantity);
        public bool CanReturn => AvailableToReturn > 0;
        public string UnitPriceText => $"{UnitPrice:N0} ₴";
        public string AvailabilityText => $"Доступно: {AvailableToReturn} з {PurchasedQuantity} шт.";

        public int QuantityToReturn
        {
            get => _quantityToReturn;
            set
            {
                int clamped = Math.Clamp(value, 0, AvailableToReturn);
                if (_quantityToReturn != clamped)
                {
                    _quantityToReturn = clamped;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ReturnLineTotal));
                    OnPropertyChanged(nameof(ReturnLineTotalText));
                }
            }
        }

        public decimal ReturnLineTotal => Math.Round(UnitPrice * QuantityToReturn * DiscountRatio, 2);
        public string ReturnLineTotalText => $"{ReturnLineTotal:N0} ₴";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ReturnsViewModel : INotifyPropertyChanged
    {
        private readonly ISaleApiClient _saleApiClient;
        private readonly IDialogService _dialogService;

        private string _receiptNumberInput = string.Empty;
        private SaleDto? _currentSale;
        private string _statusMessage = string.Empty;
        private bool _isProcessingReturn;

        public ObservableCollection<ReturnableItemViewModel> Items { get; } = new();

        public string ReceiptNumberInput
        {
            get => _receiptNumberInput;
            set
            {
                _receiptNumberInput = value;
                OnPropertyChanged();
            }
        }

        public SaleDto? CurrentSale
        {
            get => _currentSale;
            private set
            {
                _currentSale = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSale));
                OnPropertyChanged(nameof(SaleHeaderText));
                OnPropertyChanged(nameof(SaleDateText));
                OnPropertyChanged(nameof(SalePaymentText));
                OnPropertyChanged(nameof(SaleTotalText));
                OnPropertyChanged(nameof(SaleDiscountText));
                OnPropertyChanged(nameof(RefundedTotal));
                OnPropertyChanged(nameof(RefundedTotalText));
            }
        }

        public bool HasSale => CurrentSale != null;
        public string SaleHeaderText => CurrentSale != null ? $"Чек #{CurrentSale.Number}" : string.Empty;
        public string SaleDateText => CurrentSale != null ? CurrentSale.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : string.Empty;
        public string SalePaymentText => CurrentSale?.PaymentMethod == PaymentMethod.Cash ? "Готівка" : "Картка";
        public string SaleTotalText => CurrentSale != null ? $"{CurrentSale.Total:N0} ₴" : "0 ₴";
        public string SaleDiscountText => CurrentSale != null && CurrentSale.Discount > 0 ? $"-{CurrentSale.Discount:N0} ₴" : "0 ₴";
        public decimal RefundedTotal => CurrentSale?.RefundedTotal ?? 0m;
        public string RefundedTotalText => $"{RefundedTotal:N0} ₴";

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

        public int SelectedReturnCount
        {
            get
            {
                int total = 0;
                foreach (var item in Items)
                    total += item.QuantityToReturn;
                return total;
            }
        }

        public decimal SelectedReturnSubtotal
        {
            get
            {
                decimal total = 0m;
                foreach (var item in Items)
                    total += item.UnitPrice * item.QuantityToReturn * item.DiscountRatio;
                return Math.Round(total, 2);
            }
        }

        public string SelectedReturnSubtotalText => $"{SelectedReturnSubtotal:N0} ₴";

        public ICommand FindSaleCommand { get; }
        public ICommand IncreaseReturnQuantityCommand { get; }
        public ICommand DecreaseReturnQuantityCommand { get; }
        public ICommand ProcessReturnCommand { get; }

        public ReturnsViewModel(ISaleApiClient? saleApiClient = null, IDialogService? dialogService = null)
        {
            _saleApiClient = saleApiClient ?? App.CreateSaleApiClient();
            _dialogService = dialogService ?? new WpfDialogService();

            FindSaleCommand = new RelayCommand(async _ => await FindSaleAsync());
            IncreaseReturnQuantityCommand = new RelayCommand(p =>
            {
                if (p is ReturnableItemViewModel item && item.QuantityToReturn < item.AvailableToReturn)
                    item.QuantityToReturn++;
            });
            DecreaseReturnQuantityCommand = new RelayCommand(p =>
            {
                if (p is ReturnableItemViewModel item && item.QuantityToReturn > 0)
                    item.QuantityToReturn--;
            });
            ProcessReturnCommand = new RelayCommand(async _ => await ProcessReturnAsync());
        }

        private async Task FindSaleAsync()
        {
            string trimmed = (ReceiptNumberInput ?? string.Empty).Trim().TrimStart('#');
            if (!int.TryParse(trimmed, out int number) || number <= 0)
            {
                _dialogService.ShowWarning("Повернення", "Введіть коректний номер чека!");
                return;
            }

            try
            {
                StatusMessage = string.Empty;
                var response = await _saleApiClient.GetByNumberAsync(number);

                if (!response.IsSuccess || response.Sale == null)
                {
                    ClearLoadedSale();
                    string message = string.IsNullOrWhiteSpace(response.Message)
                        ? "Чек не знайдено."
                        : response.Message;
                    _dialogService.ShowWarning("Повернення", message);
                    return;
                }

                LoadSale(response.Sale);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Помилка", $"Помилка зв'язку з сервером: {ex.Message}");
            }
        }

        private async Task ProcessReturnAsync()
        {
            if (CurrentSale == null)
            {
                _dialogService.ShowWarning("Повернення", "Спочатку знайдіть чек за номером!");
                return;
            }

            var returnItems = new List<ReturnItemDto>();
            foreach (var item in Items)
            {
                if (item.QuantityToReturn > 0)
                {
                    returnItems.Add(new ReturnItemDto
                    {
                        SaleItemId = item.SaleItemId,
                        Quantity = item.QuantityToReturn
                    });
                }
            }

            if (returnItems.Count == 0)
            {
                _dialogService.ShowWarning("Повернення", "Оберіть хоча б одну позицію для повернення!");
                return;
            }

            if (_isProcessingReturn) return;
            _isProcessingReturn = true;

            try
            {
                var request = new ReturnSaleRequestDto { Items = returnItems };
                var response = await _saleApiClient.ReturnAsync(CurrentSale.Number, request);

                if (!response.IsSuccess)
                {
                    string message = string.IsNullOrWhiteSpace(response.Message)
                        ? "Не вдалося оформити повернення."
                        : response.Message;
                    _dialogService.ShowError("Помилка", message);
                    return;
                }

                if (response.Sale != null)
                    LoadSale(response.Sale);

                decimal refunded = CurrentSale.RefundedTotal;
                StatusMessage = $"Повернення оформлено! Повернена сума за чеком #{CurrentSale.Number}: {refunded:N0} ₴";
                _dialogService.ShowInfo("Повернення", $"Повернення за чеком #{CurrentSale.Number} оформлено!\nПовернена сума: {refunded:N0} ₴");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Помилка", $"Виникла помилка під час повернення: {ex.Message}");
            }
            finally
            {
                _isProcessingReturn = false;
            }
        }

        private void ClearLoadedSale()
        {
            foreach (var item in Items)
                item.PropertyChanged -= Item_PropertyChanged;
            Items.Clear();
            CurrentSale = null;
            NotifySelectionTotals();
        }

        private void LoadSale(SaleDto sale)
        {
            foreach (var item in Items)
                item.PropertyChanged -= Item_PropertyChanged;
            Items.Clear();

            CurrentSale = sale;
            decimal discountRatio = sale.Subtotal > 0m ? (sale.Subtotal - sale.Discount) / sale.Subtotal : 1m;

            foreach (var line in sale.Items)
            {
                var vm = new ReturnableItemViewModel
                {
                    SaleItemId = line.Id,
                    Title = line.Title,
                    Sku = line.Sku ?? string.Empty,
                    PurchasedQuantity = line.Quantity,
                    ReturnedQuantity = line.ReturnedQuantity,
                    UnitPrice = line.UnitPrice,
                    DiscountRatio = discountRatio
                };
                vm.PropertyChanged += Item_PropertyChanged;
                Items.Add(vm);
            }

            NotifySelectionTotals();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ReturnableItemViewModel.QuantityToReturn) or nameof(ReturnableItemViewModel.ReturnLineTotal))
                NotifySelectionTotals();
        }

        private void NotifySelectionTotals()
        {
            OnPropertyChanged(nameof(SelectedReturnCount));
            OnPropertyChanged(nameof(SelectedReturnSubtotal));
            OnPropertyChanged(nameof(SelectedReturnSubtotalText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}