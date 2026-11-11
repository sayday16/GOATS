using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using GoatStore.Core.DTOs;
using LogWindowUI.Services;
using LogWindowUI.ViewModels;
using Xunit;

namespace LogWindowUI.Tests
{
    internal static class TestViewModelFactory
    {
        public static MainViewModel Create(AuthResponseDto? authResponse = null)
        {
            return new MainViewModel(authResponse ?? new AuthResponseDto(), new FakeProductApiClient(), new FakeDialogService());
        }
    }

    internal static class CartTestFactory
    {
        public static MainViewModel Create(params (decimal Price, int Quantity)[] items)
        {
            var viewModel = TestViewModelFactory.Create();

            var cartItems = new ObservableCollection<CartItemViewModel>();
            foreach (var item in items)
            {
                cartItems.Add(new CartItemViewModel { SalePrice = item.Price, Quantity = item.Quantity });
            }

            viewModel.CartItems = cartItems;
            viewModel.Discount = 0m;

            return viewModel;
        }
    }

    internal static class TestHelper
    {
        public static JsonElement LoadSection(string section)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "test_data.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.GetProperty(section).Clone();
        }
    }

    public class MainViewModelTests
    {
        private static List<string?> CapturePropertyChanges(INotifyPropertyChanged viewModel)
        {
            var raised = new List<string?>();
            viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            return raised;
        }

        [Fact]
        public void RemoveItemCommand_ItemInCart_RemovesItem()
        {
            var viewModel = CartTestFactory.Create((1000m, 1), (500m, 1));
            var item = viewModel.CartItems[1];

            viewModel.RemoveItemCommand.Execute(item);

            Assert.DoesNotContain(item, viewModel.CartItems);
        }

        [Fact]
        public void RemoveItemCommand_ItemInCart_UpdatesTotalItemsCount()
        {
            var viewModel = CartTestFactory.Create((1000m, 1), (500m, 1));
            var item = viewModel.CartItems[1];

            viewModel.RemoveItemCommand.Execute(item);

            Assert.Equal(1, viewModel.TotalItemsCount);
        }

        [Fact]
        public void RemoveItemCommand_ItemInCart_UpdatesSubtotal()
        {
            var viewModel = CartTestFactory.Create((1000m, 1), (500m, 1));
            var item = viewModel.CartItems[1];

            viewModel.RemoveItemCommand.Execute(item);

            Assert.Equal(1000m, viewModel.Subtotal);
        }

        [Fact]
        public void RemoveItemCommand_NullParameter_DoesNotThrow()
        {
            var viewModel = CartTestFactory.Create((1000m, 1));

            var ex = Record.Exception(() => viewModel.RemoveItemCommand.Execute(null));

            Assert.Null(ex);
        }

        [Fact]
        public void ItemAddedToCart_RaisesTotalsPropertyChanged()
        {
            var viewModel = CartTestFactory.Create((100m, 1));
            var raised = CapturePropertyChanges(viewModel);

            viewModel.CartItems.Add(new CartItemViewModel { SalePrice = 200m, Quantity = 1 });

            Assert.Contains(nameof(MainViewModel.Subtotal), raised);
        }

        public static IEnumerable<object[]> GetTotalPayableTestData()
        {
            foreach (var item in TestHelper.LoadSection("TotalPayable").EnumerateArray())
            {
                yield return new object[]
                {
                    item.GetProperty("Discount").GetDecimal(),
                    item.GetProperty("ExpectedPayable").GetDecimal()
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetTotalPayableTestData))]
        public void TotalPayable_DiscountApplied_NeverBelowZero(decimal discount, decimal expectedPayable)
        {
            var viewModel = CartTestFactory.Create((100.50m, 1));

            viewModel.Discount = discount;

            Assert.Equal(expectedPayable, viewModel.TotalPayable);
        }

        [Fact]
        public void Discount_Negative_SetsDiscountToZero()
        {
            var viewModel = CartTestFactory.Create((100m, 1));

            viewModel.Discount = -50m;

            Assert.Equal(0m, viewModel.Discount);
        }

        [Fact]
        public void Discount_Negative_UpdatesTotalPayable()
        {
            var viewModel = CartTestFactory.Create((100m, 1));

            viewModel.Discount = -50m;

            Assert.Equal(100m, viewModel.TotalPayable);
        }

        [Fact]
        public void ClearCartCommand_CartWithItems_EmptiesCartItems()
        {
            var viewModel = CartTestFactory.Create((500m, 1));

            viewModel.ClearCartCommand.Execute(null);

            Assert.Empty(viewModel.CartItems);
        }

        [Fact]
        public void ClearCartCommand_CartWithItems_ResetsDiscountToZero()
        {
            var viewModel = CartTestFactory.Create((500m, 1));
            viewModel.Discount = 100m;

            viewModel.ClearCartCommand.Execute(null);

            Assert.Equal(0m, viewModel.Discount);
        }

        [Fact]
        public void ClearCartCommand_CartWithItems_ResetsSubtotalToZero()
        {
            var viewModel = CartTestFactory.Create((500m, 1));

            viewModel.ClearCartCommand.Execute(null);

            Assert.Equal(0m, viewModel.Subtotal);
        }

        [Fact]
        public void ClearCartCommand_CartWithItems_ResetsTotalPayableToZero()
        {
            var viewModel = CartTestFactory.Create((500m, 1));
            viewModel.Discount = 100m;

            viewModel.ClearCartCommand.Execute(null);

            Assert.Equal(0m, viewModel.TotalPayable);
        }

        [Fact]
        public void ClearCartCommand_CartWithItems_ResetsTotalItemsCountToZero()
        {
            var viewModel = CartTestFactory.Create((500m, 1));

            viewModel.ClearCartCommand.Execute(null);

            Assert.Equal(0, viewModel.TotalItemsCount);
        }

        [Fact]
        public void RemoveItemCommand_LastItem_ResetsDiscount()
        {
            var viewModel = CartTestFactory.Create((100m, 1));
            viewModel.Discount = 30m;

            viewModel.RemoveItemCommand.Execute(viewModel.CartItems[0]);

            Assert.Equal(0m, viewModel.Discount);
        }

        [Fact]
        public void IsCardPayment_ByDefault_IsTrue()
        {
            var viewModel = CartTestFactory.Create();

            Assert.True(viewModel.IsCardPayment);
        }

        [Fact]
        public void SelectCashCommand_SetsIsCashPaymentToTrue()
        {
            var viewModel = CartTestFactory.Create();

            viewModel.SelectCashCommand.Execute(null);

            Assert.True(viewModel.IsCashPayment);
        }

        [Fact]
        public void SelectCashCommand_SetsIsCardPaymentToFalse()
        {
            var viewModel = CartTestFactory.Create();

            viewModel.SelectCashCommand.Execute(null);

            Assert.False(viewModel.IsCardPayment);
        }

        [Fact]
        public void SelectCardCommand_SetsIsCardPaymentToTrue()
        {
            var viewModel = CartTestFactory.Create();
            viewModel.SelectCashCommand.Execute(null);

            viewModel.SelectCardCommand.Execute(null);

            Assert.True(viewModel.IsCardPayment);
        }

        [Fact]
        public void SelectCardCommand_SetsIsCashPaymentToFalse()
        {
            var viewModel = CartTestFactory.Create();
            viewModel.SelectCashCommand.Execute(null);

            viewModel.SelectCardCommand.Execute(null);

            Assert.False(viewModel.IsCashPayment);
        }

        [Fact]
        public void Constructor_StoreNameProvided_SetsStoreLocationEvenWithoutEmail()
        {
            var viewModel = TestViewModelFactory.Create(new AuthResponseDto { Email = "", StoreName = "GoatShop" });

            Assert.Equal("GoatShop", viewModel.StoreLocation);
        }

        public static TheoryData<string> TextPropertiesData => new()
        {
            nameof(MainViewModel.ShiftInfo),
            nameof(MainViewModel.StoreLocation),
            nameof(MainViewModel.CashRegister),
            nameof(MainViewModel.ReceiptNumber),
            nameof(MainViewModel.SearchText)
        };

        [Theory]
        [MemberData(nameof(TextPropertiesData))]
        public void TextProperty_Set_UpdatesValue(string property)
        {
            var viewModel = TestViewModelFactory.Create();
            var propertyInfo = typeof(MainViewModel).GetProperty(property)!;

            propertyInfo.SetValue(viewModel, "нове значення");

            Assert.Equal("нове значення", propertyInfo.GetValue(viewModel));
        }

        [Theory]
        [MemberData(nameof(TextPropertiesData))]
        public void TextProperty_Set_FiresPropertyChanged(string property)
        {
            var viewModel = TestViewModelFactory.Create();
            var raised = CapturePropertyChanges(viewModel);
            var propertyInfo = typeof(MainViewModel).GetProperty(property)!;

            propertyInfo.SetValue(viewModel, "нове значення");

            Assert.Contains(property, raised);
        }

        public static IEnumerable<object[]> GetPluralTestData()
        {
            foreach (var item in TestHelper.LoadSection("Plurals").EnumerateArray())
            {
                yield return new object[]
                {
                    item.GetProperty("Quantity").GetInt32(),
                    item.GetProperty("Expected").GetString()!
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetPluralTestData))]
        public void TotalItemsCountText_UsesCorrectPlural(int quantity, string expected)
        {
            var items = new (decimal, int)[quantity];
            for (int i = 0; i < quantity; i++)
            {
                items[i] = (10m, 1);
            }

            var viewModel = CartTestFactory.Create(items);

            Assert.Equal(expected, viewModel.TotalItemsCountText);
        }

        public static IEnumerable<object[]> GetEmailTestData()
        {
            foreach (var item in TestHelper.LoadSection("Emails").EnumerateArray())
            {
                yield return new object[]
                {
                    item.GetProperty("Input").GetString()!,
                    item.GetProperty("Expected").GetString()!
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetEmailTestData))]
        public void Constructor_FormatsCashierName_FromEmail(string email, string expectedName)
        {
            var viewModel = TestViewModelFactory.Create(new AuthResponseDto { Email = email });

            Assert.Equal(expectedName, viewModel.CashierName);
        }

        public static IEnumerable<object[]> GetEmailWithoutNameTestData()
        {
            foreach (var item in TestHelper.LoadSection("EmailsWithoutName").EnumerateArray())
            {
                yield return new object[]
                {
                    item.GetProperty("Input").GetString()!
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetEmailWithoutNameTestData))]
        public void Constructor_EmailWithoutName_KeepsDefaultCashierName(string email)
        {
            var defaultName = TestViewModelFactory.Create().CashierName;

            var viewModel = TestViewModelFactory.Create(new AuthResponseDto { Email = email });

            Assert.Equal(defaultName, viewModel.CashierName);
        }
    }

    public class MainViewModelFormattingTests : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUICulture;

        public MainViewModelFormattingTests()
        {
            _originalCulture = Thread.CurrentThread.CurrentCulture;
            _originalUICulture = Thread.CurrentThread.CurrentUICulture;

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = _originalCulture;
            Thread.CurrentThread.CurrentUICulture = _originalUICulture;
        }

        public static IEnumerable<object[]> GetSubtotalTextTestData()
        {
            foreach (var item in TestHelper.LoadSection("SubtotalText").EnumerateArray())
            {
                yield return new object[]
                {
                    item.GetProperty("Price").GetDecimal(),
                    item.GetProperty("Quantity").GetInt32(),
                    item.GetProperty("Expected").GetString()!
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetSubtotalTextTestData))]
        public void SubtotalText_FormatsWithInvariantCulture(decimal price, int quantity, string expected)
        {
            var viewModel = CartTestFactory.Create((price, quantity));

            Assert.Equal(expected, viewModel.SubtotalText);
        }

        public static IEnumerable<object[]> GetDiscountTextTestData()
        {
            foreach (var item in TestHelper.LoadSection("DiscountText").EnumerateArray())
            {
                yield return new object[]
                {
                    item.GetProperty("Discount").GetDecimal(),
                    item.GetProperty("Expected").GetString()!
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetDiscountTextTestData))]
        public void DiscountText_FormatsCorrectly(decimal discount, string expected)
        {
            var viewModel = CartTestFactory.Create((100m, 1));

            viewModel.Discount = discount;

            Assert.Equal(expected, viewModel.DiscountText);
        }

        public static IEnumerable<object[]> GetTotalPayableTextTestData()
        {
            foreach (var item in TestHelper.LoadSection("TotalPayableText").EnumerateArray())
            {
                yield return new object[]
                {
                    item.GetProperty("Price").GetDecimal(),
                    item.GetProperty("Discount").GetDecimal(),
                    item.GetProperty("Expected").GetString()!
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetTotalPayableTextTestData))]
        public void TotalPayableText_FormatsCorrectly(decimal price, decimal discount, string expected)
        {
            var viewModel = CartTestFactory.Create((price, 1));

            viewModel.Discount = discount;

            Assert.Equal(expected, viewModel.TotalPayableText);
        }
    }
}