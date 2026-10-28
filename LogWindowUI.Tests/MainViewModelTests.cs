using Xunit;
using LogWindowUI.ViewModels;
using GoatStore.Core.DTOs;

namespace LogWindowUI.Tests
{
    public class MainViewModelTests
    {

        [Fact]
        public void TotalPrice_Calculates_PriceTimesQuantity()
        {
            var item = new CartItemViewModel
            {
                UnitPrice = 500m,
                Quantity = 1
            };

            item.Quantity = 3;
            Assert.Equal(1500m, item.TotalPrice);
        }

        [Fact]
        public void Quantity_CannotBe_LessThanOne()
        {
            var item = new CartItemViewModel
            {
                Quantity = 2
            };

            item.Quantity = 0; 
            item.Quantity = -5; 

            Assert.Equal(2, item.Quantity);
        }

        [Fact]
        public void QuantityChanged_Fires_PropertyChangedEvent()
        {
            var item = new CartItemViewModel { Quantity = 1 };
            bool isQuantityFired = false;
            bool isTotalPriceTextFired = false;

            item.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(item.Quantity))
                    isQuantityFired = true;
                if (args.PropertyName == nameof(item.TotalPriceText))
                    isTotalPriceTextFired = true;
            };

            item.Quantity = 2;

            Assert.True(isQuantityFired, "Подія для Quantity не спрацювала.");
            Assert.True(isTotalPriceTextFired, "Подія для TotalPriceText не спрацювала.");
        }


        [Fact]
        public void IncreaseQuantityCommand_IncreasesQuantity_AndRecalculatesTotals()
        {
            var viewModel = new MainViewModel();
            var firstItem = viewModel.CartItems[0]; 
            var initialTotalItems = viewModel.TotalItemsCount;
            var initialSubtotal = viewModel.Subtotal;

            viewModel.IncreaseQuantityCommand.Execute(firstItem);

            Assert.Equal(3, firstItem.Quantity);
            Assert.Equal(initialTotalItems + 1, viewModel.TotalItemsCount);
            Assert.Equal(initialSubtotal + 1199m, viewModel.Subtotal);
        }

        [Fact]
        public void DecreaseQuantityCommand_DecreasesQuantity_AndRecalculatesTotals()
        {
            var viewModel = new MainViewModel();
            var firstItem = viewModel.CartItems[0]; 
            var initialTotalItems = viewModel.TotalItemsCount;
            var initialSubtotal = viewModel.Subtotal;

            viewModel.DecreaseQuantityCommand.Execute(firstItem);

            Assert.Equal(1, firstItem.Quantity);
            Assert.Equal(initialTotalItems - 1, viewModel.TotalItemsCount);
            Assert.Equal(initialSubtotal - 1199m, viewModel.Subtotal);
        }

        [Fact]
        public void DecreaseQuantityCommand_CannotDecrease_BelowOne()
        {
            var viewModel = new MainViewModel();
            var secondItem = viewModel.CartItems[1];
            var initialTotalItems = viewModel.TotalItemsCount;

            viewModel.DecreaseQuantityCommand.Execute(secondItem); 

            Assert.Equal(1, secondItem.Quantity);
            Assert.Equal(initialTotalItems, viewModel.TotalItemsCount);
        }

        [Fact]
        public void RemoveItemCommand_RemovesItem_AndRecalculatesTotals()
        {
            var viewModel = new MainViewModel();
            var secondItem = viewModel.CartItems[1]; 
            var initialItemsCount = viewModel.CartItems.Count;
            var initialSubtotal = viewModel.Subtotal;

            viewModel.RemoveItemCommand.Execute(secondItem);

            Assert.Equal(initialItemsCount - 1, viewModel.CartItems.Count);
            Assert.DoesNotContain(secondItem, viewModel.CartItems);
            Assert.Equal(initialSubtotal - 2899m, viewModel.Subtotal);
        }

        [Fact]
        public void TotalPayable_DiscountGreaterThanSubtotal_ReturnsZero()
        {
            var viewModel = new MainViewModel();

            viewModel.Discount = 15000m;

            Assert.Equal(0m, viewModel.TotalPayable);
        }

        [Fact]
        public void ClearCartCommand_WhenExecuted_ClearsCartAndDiscount()
        {
            var viewModel = new MainViewModel();

            viewModel.ClearCartCommand.Execute(null);

            Assert.Empty(viewModel.CartItems);
            Assert.Equal(0m, viewModel.Discount);
            Assert.Equal(0m, viewModel.Subtotal);
            Assert.Equal(0m, viewModel.TotalPayable);
            Assert.Equal(0, viewModel.TotalItemsCount);
        }

        [Fact]
        public void PaymentMethods_CardAndCash_AreMutuallyExclusive()
        {
            var viewModel = new MainViewModel();

            viewModel.SelectCashCommand.Execute(null);

            Assert.True(viewModel.IsCashPayment);
            Assert.False(viewModel.IsCardPayment);

            viewModel.SelectCardCommand.Execute(null);

            Assert.True(viewModel.IsCardPayment);
            Assert.False(viewModel.IsCashPayment);
        }

        [Theory]
        [InlineData("ivan.petrenko@gmail.com", "Ivan Petrenko")]
        [InlineData("owner@gmail.com", "Owner")]
        [InlineData("maria.koval@company.com", "Maria Koval")]
        [InlineData("admin@test", "Admin")]
        public void Constructor_FormatsCashierName_FromEmailCorrectly(string email, string expectedName)
        {
            var authResponse = new AuthResponseDto
            {
                Email = email
            };

            var viewModel = new MainViewModel(authResponse);

            Assert.Equal(expectedName, viewModel.CashierName);
        }
    }
}