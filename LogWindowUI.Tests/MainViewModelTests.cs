using System.Collections.ObjectModel;
using GoatStore.Core.DTOs;
using LogWindowUI.ViewModels;
using Xunit;

namespace LogWindowUI.Tests
{
    public class MainViewModelTests
    {
        private static MainViewModel CreateViewModel(params (decimal Price, int Quantity)[] items)
        {
            var viewModel = new MainViewModel();
            viewModel.CartItems = new ObservableCollection<CartItemViewModel>(
                items.Select(i => new CartItemViewModel { UnitPrice = i.Price, Quantity = i.Quantity }));
            viewModel.Discount = 0m;
            return viewModel;
        }

        private static List<string?> CapturePropertyChanges(MainViewModel viewModel)
        {
            var raised = new List<string?>();
            viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            return raised;
        }

        [Fact]
        public void TotalPrice_QuantityChanged_EqualsPriceTimesQuantity()
        {
            var item = new CartItemViewModel { UnitPrice = 500m, Quantity = 1 };

            item.Quantity = 3;

            Assert.Equal(1500m, item.TotalPrice);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Quantity_BelowOne_IsIgnored(int invalidQuantity)
        {
            var item = new CartItemViewModel { Quantity = 2 };

            item.Quantity = invalidQuantity;

            Assert.Equal(2, item.Quantity);
        }

        [Fact]
        public void Quantity_Changed_RaisesQuantityAndTotalPriceTextEvents()
        {
            var item = new CartItemViewModel { Quantity = 1 };
            var raised = new List<string?>();
            item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            item.Quantity = 2;

            Assert.Contains(nameof(CartItemViewModel.Quantity), raised);
            Assert.Contains(nameof(CartItemViewModel.TotalPriceText), raised);
        }

        [Fact]
        public void IncreaseQuantityCommand_ItemInCart_UpdatesQuantityAndTotals()
        {
            var viewModel = CreateViewModel((1000m, 2));
            var item = viewModel.CartItems[0];

            viewModel.IncreaseQuantityCommand.Execute(item);

            Assert.Equal(3, item.Quantity);
            Assert.Equal(3, viewModel.TotalItemsCount);
            Assert.Equal(3000m, viewModel.Subtotal);
        }

        [Fact]
        public void DecreaseQuantityCommand_QuantityAboveOne_UpdatesTotals()
        {
            var viewModel = CreateViewModel((1000m, 2));
            var item = viewModel.CartItems[0];

            viewModel.DecreaseQuantityCommand.Execute(item);

            Assert.Equal(1, item.Quantity);
            Assert.Equal(1, viewModel.TotalItemsCount);
            Assert.Equal(1000m, viewModel.Subtotal);
        }

        [Fact]
        public void DecreaseQuantityCommand_QuantityIsOne_DoesNothing()
        {
            var viewModel = CreateViewModel((500m, 1));
            var item = viewModel.CartItems[0];

            viewModel.DecreaseQuantityCommand.Execute(item);

            Assert.Equal(1, item.Quantity);
            Assert.Equal(1, viewModel.TotalItemsCount);
            Assert.Equal(500m, viewModel.Subtotal);
        }

        [Fact]
        public void RemoveItemCommand_ItemInCart_RemovesAndUpdatesTotals()
        {
            var viewModel = CreateViewModel((1000m, 2), (500m, 1));
            var item = viewModel.CartItems[1];

            viewModel.RemoveItemCommand.Execute(item);

            Assert.Single(viewModel.CartItems);
            Assert.DoesNotContain(item, viewModel.CartItems);
            Assert.Equal(2, viewModel.TotalItemsCount);
            Assert.Equal(2000m, viewModel.Subtotal);
        }

        [Fact]
        public void ItemQuantityChangedDirectly_RaisesTotalsPropertyChanged()
        {
            var viewModel = CreateViewModel((100m, 1));
            var raised = CapturePropertyChanges(viewModel);

            viewModel.CartItems[0].Quantity = 3;

            Assert.Contains(nameof(MainViewModel.Subtotal), raised);
            Assert.Contains(nameof(MainViewModel.TotalItemsCount), raised);
        }

        [Fact]
        public void ItemAddedToCart_RaisesTotalsPropertyChanged()
        {
            var viewModel = CreateViewModel();
            var raised = CapturePropertyChanges(viewModel);

            viewModel.CartItems.Add(new CartItemViewModel { UnitPrice = 100m });

            Assert.Contains(nameof(MainViewModel.Subtotal), raised);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(30, 70)]
        [InlineData(100, 0)]
        [InlineData(150, 0)]
        public void TotalPayable_DiscountApplied_NeverBelowZero(int discount, int expectedPayable)
        {
            var viewModel = CreateViewModel((100m, 1));

            viewModel.Discount = discount;

            Assert.Equal(expectedPayable, viewModel.TotalPayable);
        }

        [Fact]
        public void Discount_Negative_IsClampedToZero()
        {
            var viewModel = CreateViewModel((100m, 1));

            viewModel.Discount = -50m;

            Assert.Equal(0m, viewModel.Discount);
            Assert.Equal(100m, viewModel.TotalPayable);
        }

        [Fact]
        public void ClearCartCommand_CartWithItemsAndDiscount_ResetsEverything()
        {
            var viewModel = CreateViewModel((500m, 1));
            viewModel.Discount = 100m;

            viewModel.ClearCartCommand.Execute(null);

            Assert.Empty(viewModel.CartItems);
            Assert.Equal(0m, viewModel.Discount);
            Assert.Equal(0m, viewModel.Subtotal);
            Assert.Equal(0m, viewModel.TotalPayable);
            Assert.Equal(0, viewModel.TotalItemsCount);
        }

        [Fact]
        public void RemoveItemCommand_LastItem_ResetsDiscount()
        {
            var viewModel = CreateViewModel((100m, 1));
            viewModel.Discount = 30m;

            viewModel.RemoveItemCommand.Execute(viewModel.CartItems[0]);

            Assert.Equal(0m, viewModel.Discount);
        }

        [Fact]
        public void PaymentMethods_CardIsDefault_AndSelectionIsMutuallyExclusive()
        {
            var viewModel = CreateViewModel();

            Assert.True(viewModel.IsCardPayment);
            Assert.False(viewModel.IsCashPayment);

            viewModel.SelectCashCommand.Execute(null);

            Assert.True(viewModel.IsCashPayment);
            Assert.False(viewModel.IsCardPayment);

            viewModel.SelectCardCommand.Execute(null);

            Assert.True(viewModel.IsCardPayment);
            Assert.False(viewModel.IsCashPayment);
        }

        [Theory]
        [InlineData(1, "1 товар")]
        [InlineData(2, "2 товари")]
        [InlineData(5, "5 товарів")]
        [InlineData(11, "11 товарів")]
        [InlineData(21, "21 товар")]
        public void TotalItemsCountText_UsesCorrectPlural(int quantity, string expected)
        {
            var viewModel = CreateViewModel((10m, quantity));

            Assert.Equal(expected, viewModel.TotalItemsCountText);
        }

        [Theory]
        [InlineData("ivan.petrenko@gmail.com", "Ivan Petrenko")]
        [InlineData("owner@gmail.com", "Owner")]
        [InlineData("maria.koval@company.com", "Maria Koval")]
        [InlineData("admin@test", "Admin")]
        [InlineData("a..b@x.com", "A B")]
        [InlineData(".john@x.com", "John")]
        [InlineData("john.@x.com", "John")]
        [InlineData("ivan.petrenko.jr@x.com", "Ivan Petrenko")]
        public void Constructor_FormatsCashierName_FromEmail(string email, string expectedName)
        {
            var viewModel = new MainViewModel(new AuthResponseDto { Email = email });

            Assert.Equal(expectedName, viewModel.CashierName);
        }

        [Theory]
        [InlineData("@x.com")]
        [InlineData("..@x.com")]
        [InlineData("   ")]
        public void Constructor_EmailWithoutName_KeepsDefaultCashierName(string email)
        {
            var defaultName = new MainViewModel().CashierName;

            var viewModel = new MainViewModel(new AuthResponseDto { Email = email });

            Assert.Equal(defaultName, viewModel.CashierName);
        }

        [Fact]
        public void Constructor_StoreNameProvided_SetsStoreLocationEvenWithoutEmail()
        {
            var viewModel = new MainViewModel(new AuthResponseDto { Email = "", StoreName = "GoatShop" });

            Assert.Equal("GoatShop", viewModel.StoreLocation);
        }

        [Fact]
        public void Constructor_WithStoreName_ShowsItAsStoreLocation()
        {
            var viewModel = new MainViewModel(new AuthResponseDto { Email = "owner@gmail.com", StoreName = "GoatShop" });

            Assert.Equal("GoatShop", viewModel.StoreLocation);
        }

        [Theory]
        [InlineData(nameof(MainViewModel.ShiftInfo))]
        [InlineData(nameof(MainViewModel.StoreLocation))]
        [InlineData(nameof(MainViewModel.CashRegister))]
        [InlineData(nameof(MainViewModel.ReceiptNumber))]
        [InlineData(nameof(MainViewModel.SearchText))]
        public void TextProperty_Set_UpdatesValueAndFiresPropertyChanged(string property)
        {
            var viewModel = new MainViewModel();
            string? firedFor = null;
            viewModel.PropertyChanged += (_, args) => firedFor = args.PropertyName;

            typeof(MainViewModel).GetProperty(property)!.SetValue(viewModel, "нове значення");

            Assert.Equal("нове значення", typeof(MainViewModel).GetProperty(property)!.GetValue(viewModel));
            Assert.Equal(property, firedFor);
        }

        [Fact]
        public void TotalsText_ShowsFormattedAmounts()
        {
            var viewModel = new MainViewModel();
            viewModel.ClearCartCommand.Execute(null);
            viewModel.CartItems.Add(new CartItemViewModel { UnitPrice = 300m, Quantity = 2 });
            viewModel.CartItems = viewModel.CartItems;
            viewModel.Discount = 100m;

            Assert.Equal("2 товари", viewModel.TotalItemsCountText);
            Assert.Equal("600 ₴", viewModel.SubtotalText);
            Assert.Equal("-100 ₴", viewModel.DiscountText);
            Assert.Equal("500 ₴", viewModel.TotalPayableText);
            Assert.Equal("600 ₴", viewModel.CartItems[0].TotalPriceText);
        }
    }
}