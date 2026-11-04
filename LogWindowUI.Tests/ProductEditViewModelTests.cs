using System.Threading.Tasks;
using GoatStore.DataAccess.Entities;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class ProductEditViewModelTests
    {
        private readonly FakeProductApiClient _api = new();

        private ProductEditViewModel CreateValidViewModel() => new(_api)
        {
            Title = "Худі Oversize Essential",
            Brand = "Nike",
            Size = "M",
            SelectedCategory = "Одяг",
            SelectedCondition = "Новий",
            Sku = "GST-HD04-M",
            PurchasePrice = "400.00",
            SalePrice = "1200.00"
        };

        [Fact]
        public void InitialState_DefaultConditionIsNew()
        {
            var vm = new ProductEditViewModel(_api);

            Assert.Equal("Новий", vm.SelectedCondition);
        }

        [Fact]
        public void InitialState_DefaultCategoryIsClothing()
        {
            var vm = new ProductEditViewModel(_api);

            Assert.Equal("Одяг", vm.SelectedCategory);
        }

        [Fact]
        public void SaveCommand_CanExecute_TrueWhenAllFieldsValid()
        {
            var vm = CreateValidViewModel();

            Assert.True(vm.SaveCommand.CanExecute(null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SaveCommand_CanExecute_FalseWhenTitleEmpty(string? title)
        {
            var vm = CreateValidViewModel();
            vm.Title = title!;

            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SaveCommand_CanExecute_FalseWhenBrandEmpty(string? brand)
        {
            var vm = CreateValidViewModel();
            vm.Brand = brand!;

            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SaveCommand_CanExecute_FalseWhenSizeEmpty(string? size)
        {
            var vm = CreateValidViewModel();
            vm.Size = size!;

            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        [Theory]
        [InlineData("not-a-number")]
        [InlineData("")]
        public void SaveCommand_CanExecute_FalseWhenPriceInvalid(string price)
        {
            var vm = CreateValidViewModel();
            vm.SalePrice = price;

            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        [Fact]
        public void SaveCommand_Execute_SendsCorrectTitle()
        {
            var vm = CreateValidViewModel();

            vm.SaveCommand.Execute(null);

            Assert.Equal("Худі Oversize Essential", _api.LastCreateRequest?.Title);
        }

        [Fact]
        public void SaveCommand_Execute_SendsCorrectBrand()
        {
            var vm = CreateValidViewModel();

            vm.SaveCommand.Execute(null);

            Assert.Equal("Nike", _api.LastCreateRequest?.Brand);
        }

        [Fact]
        public void SaveCommand_Execute_SendsCorrectSize()
        {
            var vm = CreateValidViewModel();

            vm.SaveCommand.Execute(null);

            Assert.Equal("M", _api.LastCreateRequest?.Size);
        }

        [Fact]
        public void SaveCommand_Execute_SendsCorrectPrices()
        {
            var vm = CreateValidViewModel();

            vm.SaveCommand.Execute(null);

            Assert.Equal(1200.00m, _api.LastCreateRequest?.SalePrice);
        }

        [Fact]
        public void SaveCommand_Execute_MapsUsedConditionCorrectly()
        {
            var vm = CreateValidViewModel();
            vm.SelectedCondition = "Вживаний";

            vm.SaveCommand.Execute(null);

            Assert.Equal(ProductCondition.Used, _api.LastCreateRequest?.Condition);
        }

        [Fact]
        public void SaveCommand_Execute_SuccessClosesDialogWithTrue()
        {
            var vm = CreateValidViewModel();
            bool? closedWith = null;
            vm.RequestClose += result => closedWith = result;

            vm.SaveCommand.Execute(null);

            Assert.True(closedWith);
        }

        [Fact]
        public void CancelCommand_Execute_ClosesDialogWithFalse()
        {
            var vm = CreateValidViewModel();
            bool? closedWith = null;
            vm.RequestClose += result => closedWith = result;

            vm.CancelCommand.Execute(null);

            Assert.False(closedWith);
        }
    }
}
