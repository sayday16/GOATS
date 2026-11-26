using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using LogWindowUI.Services;

namespace LogWindowUI.ViewModels
{
    public class AnalyticsViewModel : INotifyPropertyChanged
    {
        private readonly IReportApiClient _reportApiClient;

        private decimal _monthRevenue;
        private decimal _monthNetProfit;
        private int _monthReceiptsCount;
        private decimal _monthAverageReceipt;
        private decimal _yearTurnover;
        private decimal _annualTurnoverLimit;
        private decimal _limitUsedPercent;
        private bool _isLimitWarning;
        private ObservableCollection<BrandSalesDto> _topBrands = new();
        private ObservableCollection<StaleProductDto> _staleProducts = new();
        private ObservableCollection<RecentOperationDto> _recentOperations = new();
        private bool _isLoading;
        private string? _errorMessage;

        public decimal MonthRevenue
        {
            get => _monthRevenue;
            set { _monthRevenue = value; OnPropertyChanged(); }
        }

        public decimal MonthNetProfit
        {
            get => _monthNetProfit;
            set { _monthNetProfit = value; OnPropertyChanged(); }
        }

        public int MonthReceiptsCount
        {
            get => _monthReceiptsCount;
            set { _monthReceiptsCount = value; OnPropertyChanged(); }
        }

        public decimal MonthAverageReceipt
        {
            get => _monthAverageReceipt;
            set { _monthAverageReceipt = value; OnPropertyChanged(); }
        }

        public decimal YearTurnover
        {
            get => _yearTurnover;
            set { _yearTurnover = value; OnPropertyChanged(); }
        }

        public decimal AnnualTurnoverLimit
        {
            get => _annualTurnoverLimit;
            set { _annualTurnoverLimit = value; OnPropertyChanged(); }
        }

        public decimal LimitUsedPercent
        {
            get => _limitUsedPercent;
            set { _limitUsedPercent = value; OnPropertyChanged(); }
        }

        public bool IsLimitWarning
        {
            get => _isLimitWarning;
            set { _isLimitWarning = value; OnPropertyChanged(); }
        }

        public ObservableCollection<BrandSalesDto> TopBrands
        {
            get => _topBrands;
            set { _topBrands = value; OnPropertyChanged(); }
        }

        public ObservableCollection<StaleProductDto> StaleProducts
        {
            get => _staleProducts;
            set { _staleProducts = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RecentOperationDto> RecentOperations
        {
            get => _recentOperations;
            set { _recentOperations = value; OnPropertyChanged(); }
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

        public ICommand RefreshCommand { get; }

        public AnalyticsViewModel(IReportApiClient? reportApiClient = null)
        {
            _reportApiClient = reportApiClient ?? App.CreateReportApiClient();
            RefreshCommand = new RelayCommand(_ => _ = LoadReportAsync());
            _ = LoadReportAsync();
        }

        public void Refresh() => _ = LoadReportAsync();

        public async Task LoadReportAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var summary = await _reportApiClient.GetSummaryAsync();
                MonthRevenue = summary.MonthRevenue;
                MonthNetProfit = summary.MonthNetProfit;
                MonthReceiptsCount = summary.MonthReceiptsCount;
                MonthAverageReceipt = summary.MonthAverageReceipt;
                YearTurnover = summary.YearTurnover;
                AnnualTurnoverLimit = summary.AnnualTurnoverLimit;
                LimitUsedPercent = summary.LimitUsedPercent;
                IsLimitWarning = summary.IsLimitWarning;
                TopBrands = new ObservableCollection<BrandSalesDto>(summary.TopBrands);
                StaleProducts = new ObservableCollection<StaleProductDto>(summary.StaleProducts);
                RecentOperations = new ObservableCollection<RecentOperationDto>(
                    summary.RecentOperations.Select(ToLocalTime));
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null)
            {
                ErrorMessage = "Не вдалося зв'язатися із сервером. Перевірте, чи запущено API.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Помилка завантаження звіту: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static RecentOperationDto ToLocalTime(RecentOperationDto operation) => new()
        {
            Number = operation.Number,
            CreatedAt = operation.CreatedAt.ToLocalTime(),
            Total = operation.Total,
            PaymentMethod = operation.PaymentMethod,
            HasReturns = operation.HasReturns
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
