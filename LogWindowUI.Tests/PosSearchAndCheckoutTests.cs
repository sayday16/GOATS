using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class PosSearchAndCheckoutTests
    {
        private const string AlreadySoldMessage = "Цей товар уже продано.";

        private readonly FakeProductApiClient _api = new();
        private readonly FakeDialogService _dialog = new();

        private MainViewModel CreateViewModel() => new(new AuthResponseDto(), _api, _dialog);

        private static ProductDto Product(string title, string? sku = null) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Brand = "Nike",
            Size = "M",
            Category = "Одяг",
            Sku = sku,
            SalePrice = 1000m,
            Status = ProductStatus.InStock
        };

        private MainViewModel Search(string text)
        {
            var viewModel = CreateViewModel();
            viewModel.SearchText = text;
            viewModel.SearchCommand.Execute(null);
            return viewModel;
        }

        private MainViewModel SearchOneProduct()
        {
            _api.Products.Add(Product("Худі Oversize", "GST-HD01-M"));
            return Search("худі");
        }

        private MainViewModel SearchAmongSeveral(string text)
        {
            _api.Products.Add(Product("Худі Oversize", "GST-HD01-M"));
            _api.Products.Add(Product("Худі Basic", "GST-HD02-L"));
            return Search(text);
        }

        private (MainViewModel ViewModel, CartItemViewModel[] Items) CartWithItems(int count)
        {
            var viewModel = CreateViewModel();
            var items = Enumerable.Range(1, count)
                .Select(i => new CartItemViewModel { Id = Guid.NewGuid(), Title = $"Річ {i}", SalePrice = 1000m })
                .ToArray();
            foreach (var item in items)
                viewModel.CartItems.Add(item);
            return (viewModel, items);
        }

        private (MainViewModel ViewModel, CartItemViewModel[] Items) CheckoutWithSecondItemFailing()
        {
            var (viewModel, items) = CartWithItems(3);
            _api.FailingSales[items[1].Id] = AlreadySoldMessage;
            viewModel.CheckoutCommand.Execute(null);
            return (viewModel, items);
        }

        [Fact]
        public void SearchCommand_OneProductFound_AddsItToCart()
        {
            var viewModel = SearchOneProduct();

            Assert.Equal(new[] { "Худі Oversize" }, viewModel.CartItems.Select(i => i.Title));
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

            Assert.Equal(ProductStatus.InStock, _api.LastFilter?.Status);
        }

        [Fact]
        public void SearchCommand_TrimsSearchText()
        {
            _api.Products.Add(Product("Худі Oversize"));

            Search("  худі  ");

            Assert.Equal("худі", _api.LastFilter?.Search);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void SearchCommand_EmptyText_DoesNotCallApi(string text)
        {
            Search(text);

            Assert.Null(_api.LastFilter);
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

            Assert.Equal(new[] { "Худі Basic" }, viewModel.CartItems.Select(i => i.Title));
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
            _api.GetAllException = new HttpRequestException("Connection refused");

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
        public void CheckoutCommand_AllSold_SellsEveryItem()
        {
            var (viewModel, items) = CartWithItems(2);

            viewModel.CheckoutCommand.Execute(null);

            Assert.Equal(items.Select(i => i.Id), _api.SoldIds);
        }

        [Fact]
        public void CheckoutCommand_AllSold_ClearsCart()
        {
            var (viewModel, _) = CartWithItems(2);

            viewModel.CheckoutCommand.Execute(null);

            Assert.Empty(viewModel.CartItems);
        }

        [Fact]
        public void CheckoutCommand_AllSold_ShowsSuccessMessage()
        {
            var (viewModel, _) = CartWithItems(2);

            viewModel.CheckoutCommand.Execute(null);

            Assert.StartsWith("Чек успішно сформовано!", _dialog.LastMessage);
        }

        [Fact]
        public void CheckoutCommand_CardSelected_ShowsCardPayment()
        {
            var (viewModel, _) = CartWithItems(1);

            viewModel.CheckoutCommand.Execute(null);

            Assert.EndsWith("Оплата: Картка", _dialog.LastMessage);
        }

        [Fact]
        public void CheckoutCommand_CashSelected_ShowsCashPayment()
        {
            var (viewModel, _) = CartWithItems(1);
            viewModel.SelectCashCommand.Execute(null);

            viewModel.CheckoutCommand.Execute(null);

            Assert.EndsWith("Оплата: Готівка", _dialog.LastMessage);
        }

        [Fact]
        public void CheckoutCommand_SaleFails_ShowsServerMessage()
        {
            CheckoutWithSecondItemFailing();

            Assert.EndsWith(AlreadySoldMessage, _dialog.LastMessage);
        }

        [Fact]
        public void CheckoutCommand_SaleFails_RemovesAlreadySoldItems()
        {
            var (viewModel, items) = CheckoutWithSecondItemFailing();

            Assert.DoesNotContain(items[0], viewModel.CartItems);
        }

        [Fact]
        public void CheckoutCommand_SaleFails_KeepsFailedAndFollowingItemsInCart()
        {
            var (viewModel, items) = CheckoutWithSecondItemFailing();

            Assert.Equal(new[] { items[1], items[2] }, viewModel.CartItems);
        }

        [Fact]
        public void CheckoutCommand_SaleFails_DoesNotSellFollowingItems()
        {
            var (_, items) = CheckoutWithSecondItemFailing();

            Assert.Equal(new[] { items[0].Id }, _api.SoldIds);
        }
    }
}
