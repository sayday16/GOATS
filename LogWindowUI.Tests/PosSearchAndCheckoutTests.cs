using System;
using System.Collections.Generic;
using System.Net.Http;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.ViewModels;
using Xunit;

namespace LogWindowUI.Tests
{
    public class PosSearchAndCheckoutTests
    {
        private const string AlreadySoldMessage = "Цей товар уже продано.";

        private readonly FakeProductApiClient _productApi = new();
        private readonly FakeSaleApiClient _saleApi = new();
        private readonly FakeDialogService _dialog = new();

        private MainViewModel CreateViewModel() => new(new AuthResponseDto(), _productApi, _dialog, _saleApi);

        private static ProductDto Product(string title, string? sku = null, bool isUnique = true, int quantity = 1) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Brand = "Nike",
            Size = "M",
            Category = "Одяг",
            Sku = sku,
            SalePrice = 1000m,
            Status = ProductStatus.InStock,
            IsUnique = isUnique,
            Quantity = quantity
        };

        private static List<string> GetCartTitles(MainViewModel viewModel)
        {
            var titles = new List<string>();
            foreach (var item in viewModel.CartItems)
                titles.Add(item.Title);
            return titles;
        }

        private static List<Guid> GetRequestedProductIds(CreateSaleRequestDto? request)
        {
            var ids = new List<Guid>();
            if (request == null) return ids;
            foreach (var item in request.Items)
                ids.Add(item.ProductId);
            return ids;
        }

        private MainViewModel Search(string text)
        {
            var viewModel = CreateViewModel();
            viewModel.SearchText = text;
            viewModel.SearchCommand.Execute(null);
            return viewModel;
        }

        private MainViewModel SearchOneProduct()
        {
            _productApi.Products.Add(Product("Худі Oversize", "GST-HD01-M"));
            return Search("худі");
        }

        private MainViewModel SearchAmongSeveral(string text)
        {
            _productApi.Products.Add(Product("Худі Oversize", "GST-HD01-M"));
            _productApi.Products.Add(Product("Худі Basic", "GST-HD02-L"));
            return Search(text);
        }

        private (MainViewModel ViewModel, List<CartItemViewModel> Items) CartWithItems(int count)
        {
            var viewModel = CreateViewModel();
            var items = new List<CartItemViewModel>();
            for (int i = 1; i <= count; i++)
            {
                var item = new CartItemViewModel { Id = Guid.NewGuid(), Title = $"Річ {i}", SalePrice = 1000m };
                items.Add(item);
                viewModel.CartItems.Add(item);
            }
            return (viewModel, items);
        }

        private (MainViewModel ViewModel, List<CartItemViewModel> Items) CheckoutWithFailure()
        {
            var (viewModel, items) = CartWithItems(3);
            _saleApi.CreateResponse = new SaleResponseDto
            {
                IsSuccess = false,
                Message = AlreadySoldMessage
            };
            viewModel.CheckoutCommand.Execute(null);
            return (viewModel, items);
        }

        [Fact]
        public void SearchCommand_OneProductFound_AddsItToCart()
        {
            var viewModel = SearchOneProduct();

            Assert.Equal(new List<string> { "Худі Oversize" }, GetCartTitles(viewModel));
        }

        [Fact]
        public void SearchCommand_OneProductFound_ClearsSearchText()
        {
            var viewModel = SearchOneProduct();

            Assert.Equal(string.Empty, viewModel.SearchText);
        }

        [Fact]
        public void SearchCommand_OneProductFound_CopiesSalePrice()
        {
            var viewModel = SearchOneProduct();

            Assert.Equal(1000m, viewModel.Subtotal);
        }

        [Fact]
        public void SearchCommand_SearchesOnlyInStockProducts()
        {
            SearchOneProduct();

            Assert.Equal(ProductStatus.InStock, _productApi.LastFilter?.Status);
        }

        [Fact]
        public void SearchCommand_TrimsSearchText()
        {
            _productApi.Products.Add(Product("Худі Oversize"));

            Search("  худі  ");

            Assert.Equal("худі", _productApi.LastFilter?.Search);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void SearchCommand_EmptyText_DoesNotCallApi(string text)
        {
            Search(text);

            Assert.Null(_productApi.LastFilter);
        }

        [Fact]
        public void SearchCommand_NothingFound_ShowsNotFoundMessage()
        {
            Search("adidas");

            Assert.StartsWith("Товар не знайдено", _dialog.LastMessage);
        }

        [Fact]
        public void SearchCommand_NothingFound_LeavesCartEmpty()
        {
            var viewModel = Search("adidas");

            Assert.Empty(viewModel.CartItems);
        }

        [Fact]
        public void SearchCommand_SeveralFoundWithoutExactSku_AsksForSku()
        {
            SearchAmongSeveral("худі");

            Assert.Equal("Знайдено кілька товарів. Введіть точний артикул!", _dialog.LastMessage);
        }

        [Fact]
        public void SearchCommand_SeveralFoundWithoutExactSku_DoesNotAddToCart()
        {
            var viewModel = SearchAmongSeveral("худі");

            Assert.Empty(viewModel.CartItems);
        }

        [Theory]
        [InlineData("GST-HD02-L")]
        [InlineData("gst-hd02-l")]
        public void SearchCommand_ExactSkuAmongSeveral_AddsThatProduct(string sku)
        {
            var viewModel = SearchAmongSeveral(sku);

            Assert.Equal(new List<string> { "Худі Basic" }, GetCartTitles(viewModel));
        }

        [Fact]
        public void SearchCommand_ProductAlreadyInCart_DoesNotAddItTwice()
        {
            var viewModel = SearchOneProduct();

            viewModel.SearchText = "худі";
            viewModel.SearchCommand.Execute(null);

            Assert.Single(viewModel.CartItems);
        }

        [Fact]
        public void SearchCommand_ProductAlreadyInCart_ShowsMessage()
        {
            var viewModel = SearchOneProduct();

            viewModel.SearchText = "худі";
            viewModel.SearchCommand.Execute(null);

            Assert.Equal("Цей товар вже додано в чек!", _dialog.LastMessage);
        }

        [Fact]
        public void SearchCommand_ServerUnavailable_ShowsConnectionError()
        {
            _productApi.GetAllException = new HttpRequestException("Connection refused");

            Search("худі");

            Assert.StartsWith("Помилка зв'язку з сервером", _dialog.LastMessage);
        }

        [Fact]
        public void CheckoutCommand_EmptyCart_ShowsMessage()
        {
            CreateViewModel().CheckoutCommand.Execute(null);

            Assert.Equal("Чек порожній!", _dialog.LastMessage);
        }

        [Fact]
        public void CheckoutCommand_EmptyCart_DoesNotCallSaleApi()
        {
            CreateViewModel().CheckoutCommand.Execute(null);

            Assert.Null(_saleApi.LastCreateRequest);
        }

        [Fact]
        public void CheckoutCommand_Success_SendsAllItemsInOneRequest()
        {
            var (viewModel, items) = CartWithItems(2);
            var expectedIds = new List<Guid> { items[0].Id, items[1].Id };

            viewModel.CheckoutCommand.Execute(null);

            Assert.Equal(expectedIds, GetRequestedProductIds(_saleApi.LastCreateRequest));
        }

        [Fact]
        public void CheckoutCommand_Success_ClearsCart()
        {
            var (viewModel, _) = CartWithItems(2);

            viewModel.CheckoutCommand.Execute(null);

            Assert.Empty(viewModel.CartItems);
        }

        [Fact]
        public void CheckoutCommand_Success_ResetsDiscount()
        {
            var (viewModel, _) = CartWithItems(2);
            viewModel.DiscountInput = "200";
            viewModel.ApplyDiscountCommand.Execute(null);

            viewModel.CheckoutCommand.Execute(null);

            Assert.Equal(0m, viewModel.Discount);
        }

        [Fact]
        public void CheckoutCommand_Success_ShowsSaleNumberInMessage()
        {
            var (viewModel, _) = CartWithItems(1);
            _saleApi.CreateResponse = new SaleResponseDto
            {
                IsSuccess = true,
                Sale = new SaleDto { Number = 42, Total = 1000m }
            };

            viewModel.CheckoutCommand.Execute(null);

            Assert.Contains("#42", _dialog.LastMessage);
        }

        [Fact]
        public void CheckoutCommand_CardSelected_SendsCardPaymentMethod()
        {
            var (viewModel, _) = CartWithItems(1);

            viewModel.CheckoutCommand.Execute(null);

            Assert.Equal(PaymentMethod.Card, _saleApi.LastCreateRequest?.PaymentMethod);
        }

        [Fact]
        public void CheckoutCommand_CashSelected_SendsCashPaymentMethod()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.SelectCashCommand.Execute(null);

            viewModel.CheckoutCommand.Execute(null);

            Assert.Equal(PaymentMethod.Cash, _saleApi.LastCreateRequest?.PaymentMethod);
        }

        [Fact]
        public void CheckoutCommand_WithDiscount_SendsDiscountToApi()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.DiscountInput = "150";
            viewModel.ApplyDiscountCommand.Execute(null);

            viewModel.CheckoutCommand.Execute(null);

            Assert.Equal(150m, _saleApi.LastCreateRequest?.Discount);
        }

        [Fact]
        public void CheckoutCommand_SaleFails_ShowsServerMessage()
        {
            CheckoutWithFailure();

            Assert.Equal(AlreadySoldMessage, _dialog.LastMessage);
        }

        [Fact]
        public void CheckoutCommand_SaleFails_DoesNotChangeCart()
        {
            var (viewModel, items) = CheckoutWithFailure();

            Assert.Equal(items, viewModel.CartItems);
        }

        [Fact]
        public void CheckoutCommand_ServerThrows_DoesNotChangeCart()
        {
            var (viewModel, items) = CartWithItems(2);
            _saleApi.Exception = new HttpRequestException("Timeout");

            viewModel.CheckoutCommand.Execute(null);

            Assert.Equal(items, viewModel.CartItems);
        }

        [Fact]
        public void ApplyDiscountCommand_ValidAmount_SetsDiscount()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.DiscountInput = "250";

            viewModel.ApplyDiscountCommand.Execute(null);

            Assert.Equal(250m, viewModel.Discount);
        }

        [Fact]
        public void ApplyDiscountCommand_ValidAmount_ReducesTotalPayable()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.DiscountInput = "250";

            viewModel.ApplyDiscountCommand.Execute(null);

            Assert.Equal(750m, viewModel.TotalPayable);
        }

        [Fact]
        public void ApplyDiscountCommand_EmptyCart_ShowsWarning()
        {
            var viewModel = CreateViewModel();
            viewModel.DiscountInput = "100";

            viewModel.ApplyDiscountCommand.Execute(null);

            Assert.Equal("Неможливо застосувати знижку до порожнього чека!", _dialog.LastMessage);
        }

        [Fact]
        public void ApplyDiscountCommand_NegativeAmount_DoesNotChangeDiscount()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.DiscountInput = "-50";

            viewModel.ApplyDiscountCommand.Execute(null);

            Assert.Equal(0m, viewModel.Discount);
        }

        [Fact]
        public void ApplyDiscountCommand_GreaterThanSubtotal_ShowsWarning()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.DiscountInput = "1500";

            viewModel.ApplyDiscountCommand.Execute(null);

            Assert.Equal("Знижка не може перевищувати суму чека!", _dialog.LastMessage);
        }

        [Fact]
        public void ApplyDiscountCommand_GreaterThanSubtotal_DoesNotChangeDiscount()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.DiscountInput = "1500";

            viewModel.ApplyDiscountCommand.Execute(null);

            Assert.Equal(0m, viewModel.Discount);
        }

        [Fact]
        public void ApplyDiscountCommand_InvalidText_ShowsWarning()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.DiscountInput = "abc";

            viewModel.ApplyDiscountCommand.Execute(null);

            Assert.Equal("Введіть коректну суму знижки!", _dialog.LastMessage);
        }

        [Fact]
        public void IncreaseQuantityCommand_NonUniqueBelowMax_IncreasesQuantity()
        {
            _productApi.Products.Add(Product("Шкарпетки Nike", "GST-SK01", isUnique: false, quantity: 5));
            var viewModel = Search("шкарпетки");
            var item = viewModel.CartItems[0];

            viewModel.IncreaseQuantityCommand.Execute(item);

            Assert.Equal(2, item.Quantity);
        }

        [Fact]
        public void IncreaseQuantityCommand_NonUniqueAtMax_DoesNotExceedMax()
        {
            _productApi.Products.Add(Product("Шкарпетки Nike", "GST-SK01", isUnique: false, quantity: 2));
            var viewModel = Search("шкарпетки");
            var item = viewModel.CartItems[0];

            viewModel.IncreaseQuantityCommand.Execute(item);
            viewModel.IncreaseQuantityCommand.Execute(item);

            Assert.Equal(2, item.Quantity);
        }

        [Fact]
        public void IncreaseQuantityCommand_UniqueItem_DoesNotIncreaseQuantity()
        {
            var viewModel = SearchOneProduct();
            var item = viewModel.CartItems[0];

            viewModel.IncreaseQuantityCommand.Execute(item);

            Assert.Equal(1, item.Quantity);
        }

        [Fact]
        public void DecreaseQuantityCommand_NonUniqueAboveOne_DecreasesQuantity()
        {
            _productApi.Products.Add(Product("Шкарпетки Nike", "GST-SK01", isUnique: false, quantity: 5));
            var viewModel = Search("шкарпетки");
            var item = viewModel.CartItems[0];
            viewModel.IncreaseQuantityCommand.Execute(item);

            viewModel.DecreaseQuantityCommand.Execute(item);

            Assert.Equal(1, item.Quantity);
        }

        [Fact]
        public void SearchCommand_ExactTitleAmongSeveral_AddsThatProduct()
        {
            _productApi.Products.Add(Product("Шкарпетки Nike Everyday"));
            _productApi.Products.Add(Product("Шкарпетки Nike Everyday Cushion"));

            var viewModel = Search("Шкарпетки Nike Everyday");

            Assert.Equal(new List<string> { "Шкарпетки Nike Everyday" }, GetCartTitles(viewModel));
        }
    }
}