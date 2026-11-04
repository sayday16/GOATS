using System;
using System.Collections.Generic;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class InventoryViewModelTests
    {
        private readonly FakeProductApiClient _api = new();

        [Fact]
        public void Constructor_LoadsProductsFromClient()
        {
            var product = new ProductDto { Id = Guid.NewGuid(), Title = "Nike Air" };
            _api.ProductsToReturn = new List<ProductDto> { product };

            var vm = new InventoryViewModel(_api);

            Assert.Single(vm.Products);
        }

        [Fact]
        public void Constructor_LoadsCategoriesWithAllOption()
        {
            _api.FilterOptionsToReturn = new ProductFilterOptionsDto
            {
                Categories = new List<string> { "Одяг" }
            };

            var vm = new InventoryViewModel(_api);

            Assert.Contains("(Всі)", vm.Categories);
        }

        [Fact]
        public void Constructor_SetsDefaultSelectedCategoryToAll()
        {
            var vm = new InventoryViewModel(_api);

            Assert.Equal("(Всі)", vm.SelectedCategory);
        }

        [Fact]
        public void SearchCommand_PassesSearchTextToClient()
        {
            var vm = new InventoryViewModel(_api) { SearchText = "Nike" };

            vm.SearchCommand.Execute(null);

            Assert.Equal("Nike", _api.LastGetAllFilter?.Search);
        }

        [Fact]
        public void SearchCommand_WhenCategoryIsAll_PassesNullCategory()
        {
            var vm = new InventoryViewModel(_api)
            {
                SelectedCategory = "(Всі)"
            };

            vm.SearchCommand.Execute(null);

            Assert.Null(_api.LastGetAllFilter?.Category);
        }

        [Fact]
        public void SearchCommand_WhenSpecificCategorySelected_PassesCategory()
        {
            var vm = new InventoryViewModel(_api)
            {
                SelectedCategory = "Одяг"
            };

            vm.SearchCommand.Execute(null);

            Assert.Equal("Одяг", _api.LastGetAllFilter?.Category);
        }

        [Fact]
        public void ResetCommand_ClearsSearchText()
        {
            var vm = new InventoryViewModel(_api) { SearchText = "Nike" };

            vm.ResetCommand.Execute(null);

            Assert.Null(vm.SearchText);
        }

        [Fact]
        public void ResetCommand_ResetsSelectedCategoryToAll()
        {
            var vm = new InventoryViewModel(_api) { SelectedCategory = "Одяг" };

            vm.ResetCommand.Execute(null);

            Assert.Equal("(Всі)", vm.SelectedCategory);
        }

        [Fact]
        public void SellCommand_CanExecute_TrueForInStockProduct()
        {
            var vm = new InventoryViewModel(_api);
            var product = new ProductDto { Status = ProductStatus.InStock };

            Assert.True(vm.SellCommand.CanExecute(product));
        }

        [Fact]
        public void SellCommand_CanExecute_FalseForSoldProduct()
        {
            var vm = new InventoryViewModel(_api);
            var product = new ProductDto { Status = ProductStatus.Sold };

            Assert.False(vm.SellCommand.CanExecute(product));
        }

        [Fact]
        public void SellCommand_CanExecute_FalseForNull()
        {
            var vm = new InventoryViewModel(_api);

            Assert.False(vm.SellCommand.CanExecute(null));
        }

        [Fact]
        public void SellCommand_Execute_CallsSellAsyncWithProductId()
        {
            var vm = new InventoryViewModel(_api);
            var id = Guid.NewGuid();
            var product = new ProductDto { Id = id, Status = ProductStatus.InStock };

            vm.SellCommand.Execute(product);

            Assert.Equal(id, _api.LastSellId);
        }
    }
}
