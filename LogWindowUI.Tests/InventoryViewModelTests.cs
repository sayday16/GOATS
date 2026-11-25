using System;
using System.Collections.Generic;
using System.Linq;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class InventoryViewModelTests
    {
        private readonly FakeProductApiClient _api = new();
        private readonly FakeSaleApiClient _saleApi = new();

        private InventoryViewModel CreateViewModel() => new(_api, _saleApi);

        [Fact]
        public void Constructor_LoadsProductsFromClient()
        {
            var product = new ProductDto { Id = Guid.NewGuid(), Title = "Nike Air" };
            _api.Products.Add(product);

            var vm = CreateViewModel();

            Assert.Single(vm.Products);
        }

        [Fact]
        public void Constructor_LoadsCategoriesWithAllOption()
        {
            _api.FilterOptions = new ProductFilterOptionsDto
            {
                Categories = new List<string> { "Одяг" }
            };

            var vm = CreateViewModel();

            Assert.Contains("(Всі)", vm.Categories);
        }

        [Fact]
        public void Constructor_SetsDefaultSelectedCategoryToAll()
        {
            var vm = CreateViewModel();

            Assert.Equal("(Всі)", vm.SelectedCategory);
        }

        [Fact]
        public void SearchCommand_PassesSearchTextToClient()
        {
            var vm = CreateViewModel();
            vm.SearchText = "Nike";

            vm.SearchCommand.Execute(null);

            Assert.Equal("Nike", _api.LastFilter?.Search);
        }

        [Fact]
        public void SearchCommand_WhenCategoryIsAll_PassesNullCategory()
        {
            var vm = CreateViewModel();
            vm.SelectedCategory = "(Всі)";

            vm.SearchCommand.Execute(null);

            Assert.Null(_api.LastFilter?.Category);
        }

        [Fact]
        public void SearchCommand_WhenSpecificCategorySelected_PassesCategory()
        {
            var vm = CreateViewModel();
            vm.SelectedCategory = "Одяг";

            vm.SearchCommand.Execute(null);

            Assert.Equal("Одяг", _api.LastFilter?.Category);
        }

        [Fact]
        public void ResetCommand_ClearsSearchText()
        {
            var vm = CreateViewModel();
            vm.SearchText = "Nike";

            vm.ResetCommand.Execute(null);

            Assert.Null(vm.SearchText);
        }

        [Fact]
        public void ResetCommand_ResetsSelectedCategoryToAll()
        {
            var vm = CreateViewModel();
            vm.SelectedCategory = "Одяг";

            vm.ResetCommand.Execute(null);

            Assert.Equal("(Всі)", vm.SelectedCategory);
        }

        [Fact]
        public void SellCommand_CanExecute_TrueForInStockProduct()
        {
            var vm = CreateViewModel();
            var product = new ProductDto { Status = ProductStatus.InStock };

            Assert.True(vm.SellCommand.CanExecute(product));
        }

        [Fact]
        public void SellCommand_CanExecute_FalseForSoldProduct()
        {
            var vm = CreateViewModel();
            var product = new ProductDto { Status = ProductStatus.Sold };

            Assert.False(vm.SellCommand.CanExecute(product));
        }

        [Fact]
        public void SellCommand_CanExecute_FalseForNull()
        {
            var vm = CreateViewModel();

            Assert.False(vm.SellCommand.CanExecute(null));
        }

        [Fact]
        public void SellCommand_Execute_CallsSaleApiClientWithProductId()
        {
            var vm = CreateViewModel();
            var id = Guid.NewGuid();
            var product = new ProductDto { Id = id, Status = ProductStatus.InStock };

            vm.SellCommand.Execute(product);

            Assert.Equal(id, _saleApi.LastCreateRequest?.Items.Single().ProductId);
        }

        [Fact]
        public void SellCommand_Execute_SendsSingleQuantity()
        {
            var vm = CreateViewModel();
            var product = new ProductDto { Id = Guid.NewGuid(), Status = ProductStatus.InStock };

            vm.SellCommand.Execute(product);

            Assert.Equal(1, _saleApi.LastCreateRequest?.Items.Single().Quantity);
        }

        [Fact]
        public void SellCommand_Execute_WhenSaleFails_SetsErrorMessage()
        {
            _saleApi.CreateResponse = new SaleResponseDto { IsSuccess = false, Message = "Товар вже продано." };
            var vm = CreateViewModel();
            var product = new ProductDto { Id = Guid.NewGuid(), Status = ProductStatus.InStock };

            vm.SellCommand.Execute(product);

            Assert.Equal("Товар вже продано.", vm.ErrorMessage);
        }
    }
}
