using System;
using System.Linq;
using System.Net.Http;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.ViewModels;
using Xunit;

namespace LogWindowUI.Tests
{
    public class PosSuggestionsTests
    {
        private readonly FakeProductApiClient _productApi = new();
        private readonly FakeSaleApiClient _saleApi = new();
        private readonly FakeDialogService _dialog = new();

        private MainViewModel CreateViewModel() => new(new AuthResponseDto(), _productApi, _dialog, _saleApi);

        private ProductDto AddProduct(string title, string brand = "Nike", string? sku = null)
        {
            var product = new ProductDto
            {
                Id = Guid.NewGuid(),
                Title = title,
                Brand = brand,
                Size = "M",
                Category = "Одяг",
                Sku = sku,
                SalePrice = 1000m,
                Status = ProductStatus.InStock,
                Quantity = 1
            };
            _productApi.Products.Add(product);
            return product;
        }

        private MainViewModel OpenWithSampleProducts()
        {
            AddProduct("Худі Oversize", "Stussy", "GST-HD01");
            AddProduct("Джинси 501", "Levi's", "GST-JN01");
            AddProduct("Кросівки Air Max", "Nike", "GST-SN01");
            var vm = CreateViewModel();
            vm.OpenSuggestionsCommand.Execute(null);
            return vm;
        }

        private static string[] SuggestionTitles(MainViewModel vm) => vm.Suggestions.Select(p => p.Title).ToArray();

        [Fact]
        public void OpenSuggestionsCommand_WithProducts_ShowsAllAvailable()
        {
            var vm = OpenWithSampleProducts();

            Assert.Equal(3, vm.Suggestions.Count);
        }

        [Fact]
        public void OpenSuggestionsCommand_WithProducts_OpensPopup()
        {
            var vm = OpenWithSampleProducts();

            Assert.True(vm.IsSuggestionsOpen);
        }

        [Fact]
        public void OpenSuggestionsCommand_Always_RequestsOnlyInStockProducts()
        {
            OpenWithSampleProducts();

            Assert.Equal(ProductStatus.InStock, _productApi.LastFilter?.Status);
        }

        [Fact]
        public void OpenSuggestionsCommand_WithProducts_SortsByTitle()
        {
            var vm = OpenWithSampleProducts();

            Assert.Equal(new[] { "Джинси 501", "Кросівки Air Max", "Худі Oversize" }, SuggestionTitles(vm));
        }

        [Fact]
        public void OpenSuggestionsCommand_ManyProducts_LimitsListSize()
        {
            for (var i = 0; i < 40; i++)
                AddProduct($"Футболка {i:D2}");
            var vm = CreateViewModel();

            vm.OpenSuggestionsCommand.Execute(null);

            Assert.Equal(30, vm.Suggestions.Count);
        }

        [Fact]
        public void SearchText_PartOfTitle_FiltersSuggestions()
        {
            var vm = OpenWithSampleProducts();

            vm.SearchText = "худі";

            Assert.Equal(new[] { "Худі Oversize" }, SuggestionTitles(vm));
        }

        [Fact]
        public void SearchText_Brand_FiltersSuggestions()
        {
            var vm = OpenWithSampleProducts();

            vm.SearchText = "levi";

            Assert.Equal(new[] { "Джинси 501" }, SuggestionTitles(vm));
        }

        [Fact]
        public void SearchText_PartOfSku_FiltersSuggestions()
        {
            var vm = OpenWithSampleProducts();

            vm.SearchText = "sn01";

            Assert.Equal(new[] { "Кросівки Air Max" }, SuggestionTitles(vm));
        }

        [Fact]
        public void SearchText_TypedWithoutOpening_OpensPopup()
        {
            AddProduct("Худі Oversize");
            var vm = CreateViewModel();

            vm.SearchText = "худі";

            Assert.True(vm.IsSuggestionsOpen);
        }

        [Fact]
        public void SearchText_Cleared_ClosesPopup()
        {
            var vm = OpenWithSampleProducts();
            vm.SearchText = "худі";

            vm.SearchText = string.Empty;

            Assert.False(vm.IsSuggestionsOpen);
        }

        [Fact]
        public void SearchText_NoMatches_ShowsNothingFoundMessage()
        {
            var vm = OpenWithSampleProducts();

            vm.SearchText = "пальто";

            Assert.Equal("Нічого не знайдено серед товарів у наявності.", vm.SuggestionsMessage);
        }

        [Fact]
        public void SearchText_HasMatches_HidesMessage()
        {
            var vm = OpenWithSampleProducts();
            vm.SearchText = "пальто";

            vm.SearchText = "худі";

            Assert.False(vm.HasSuggestionsMessage);
        }

        [Fact]
        public void AddSuggestionCommand_Product_AddsItToCart()
        {
            var vm = OpenWithSampleProducts();

            vm.AddSuggestionCommand.Execute(vm.Suggestions[0]);

            Assert.Equal("Джинси 501", vm.CartItems.Single().Title);
        }

        [Fact]
        public void AddSuggestionCommand_Product_ClosesPopup()
        {
            var vm = OpenWithSampleProducts();

            vm.AddSuggestionCommand.Execute(vm.Suggestions[0]);

            Assert.False(vm.IsSuggestionsOpen);
        }

        [Fact]
        public void AddSuggestionCommand_AfterTyping_ClearsSearchText()
        {
            var vm = OpenWithSampleProducts();
            vm.SearchText = "худі";

            vm.AddSuggestionCommand.Execute(vm.Suggestions[0]);

            Assert.Equal(string.Empty, vm.SearchText);
        }

        [Fact]
        public void AddSuggestionCommand_ProductAlreadyInCart_ShowsWarning()
        {
            var vm = OpenWithSampleProducts();
            var product = vm.Suggestions[0];
            vm.AddSuggestionCommand.Execute(product);

            vm.AddSuggestionCommand.Execute(product);

            Assert.Equal("Цей товар вже додано в чек!", _dialog.LastMessage);
        }

        [Fact]
        public void OpenSuggestionsCommand_ProductInCart_ExcludesIt()
        {
            var vm = OpenWithSampleProducts();
            vm.AddSuggestionCommand.Execute(vm.Suggestions[0]);

            vm.OpenSuggestionsCommand.Execute(null);

            Assert.DoesNotContain("Джинси 501", SuggestionTitles(vm));
        }

        [Fact]
        public void OpenSuggestionsCommand_ApiFails_ShowsLoadErrorMessage()
        {
            _productApi.GetAllException = new HttpRequestException("Connection refused");
            var vm = CreateViewModel();

            vm.OpenSuggestionsCommand.Execute(null);

            Assert.Equal("Не вдалося завантажити товари. Перевірте зв'язок із сервером.", vm.SuggestionsMessage);
        }

        [Fact]
        public void SearchText_ApiFails_DoesNotShowDialog()
        {
            _productApi.GetAllException = new HttpRequestException("Connection refused");
            var vm = CreateViewModel();

            vm.SearchText = "худі";

            Assert.Null(_dialog.LastMessage);
        }

        [Fact]
        public void SearchText_AfterFailedLoad_RetriesLoading()
        {
            AddProduct("Худі Oversize");
            _productApi.GetAllException = new HttpRequestException("Connection refused");
            var vm = CreateViewModel();
            vm.SearchText = "ху";
            _productApi.GetAllException = null;

            vm.SearchText = "худі";

            Assert.Single(vm.Suggestions);
        }

        [Fact]
        public void SearchText_SecondKeystroke_UsesLoadedList()
        {
            var vm = OpenWithSampleProducts();
            AddProduct("Худі Zip");

            vm.SearchText = "худі";

            Assert.Single(vm.Suggestions);
        }

        [Fact]
        public void OpenSuggestionsCommand_SecondTime_ReloadsProducts()
        {
            var vm = OpenWithSampleProducts();
            AddProduct("Худі Zip");

            vm.OpenSuggestionsCommand.Execute(null);

            Assert.Equal(4, vm.Suggestions.Count);
        }

        [Fact]
        public void CheckoutCommand_Success_ReloadsProductsOnNextSearch()
        {
            var vm = OpenWithSampleProducts();
            vm.AddSuggestionCommand.Execute(vm.Suggestions[0]);
            vm.CheckoutCommand.Execute(null);
            _productApi.Products.RemoveAt(0);

            vm.SearchText = "худі";

            Assert.Empty(vm.Suggestions);
        }
    }
}
