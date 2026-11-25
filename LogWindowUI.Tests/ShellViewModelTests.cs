using System;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class ShellViewModelTests
    {
        private readonly FakeProductApiClient _api = new();
        private readonly AuthResponseDto _auth = new()
        {
            IsSuccess = true,
            Email = "boss@store.com",
            Role = UserRole.Admin,
            StoreName = "Goat Store Kyiv"
        };

        private ShellViewModel CreateViewModel() => new(_auth, _api);

        [Fact]
        public void InitialState_InventoryIsActive()
        {
            var vm = CreateViewModel();

            Assert.True(vm.IsInventoryActive);
        }

        [Fact]
        public void InitialState_PosIsNotActive()
        {
            var vm = CreateViewModel();

            Assert.False(vm.IsPosActive);
        }

        [Fact]
        public void InitialState_CurrentViewIsInventoryViewModel()
        {
            var vm = CreateViewModel();

            Assert.IsType<InventoryViewModel>(vm.CurrentView);
        }

        [Fact]
        public void ShowPosCommand_SetsPosActive()
        {
            var vm = CreateViewModel();

            vm.ShowPosCommand.Execute(null);

            Assert.True(vm.IsPosActive);
        }

        [Fact]
        public void ShowPosCommand_SetsInventoryInactive()
        {
            var vm = CreateViewModel();

            vm.ShowPosCommand.Execute(null);

            Assert.False(vm.IsInventoryActive);
        }

        [Fact]
        public void ShowPosCommand_SetsCurrentViewToMainViewModel()
        {
            var vm = CreateViewModel();

            vm.ShowPosCommand.Execute(null);

            Assert.IsType<MainViewModel>(vm.CurrentView);
        }

        [Fact]
        public void ShowInventoryCommand_AfterPos_RestoresInventoryView()
        {
            var vm = CreateViewModel();
            vm.ShowPosCommand.Execute(null);

            vm.ShowInventoryCommand.Execute(null);

            Assert.IsType<InventoryViewModel>(vm.CurrentView);
        }

        [Fact]
        public void ShowInventoryCommand_AfterPos_ReusesInventoryViewModel()
        {
            var vm = CreateViewModel();
            var inventory = vm.CurrentView;
            vm.ShowPosCommand.Execute(null);

            vm.ShowInventoryCommand.Execute(null);

            Assert.Same(inventory, vm.CurrentView);
        }

        [Fact]
        public void ShowInventoryCommand_AfterSaleOnPos_ReloadsProducts()
        {
            var vm = CreateViewModel();
            vm.ShowPosCommand.Execute(null);
            _api.Products.Add(new ProductDto { Id = Guid.NewGuid(), Title = "Levi's 501", Status = ProductStatus.Sold });

            vm.ShowInventoryCommand.Execute(null);

            var inventory = (InventoryViewModel)vm.CurrentView!;
            Assert.Equal(ProductStatus.Sold, inventory.Products.Single().Status);
        }

        [Fact]
        public void ShowReturnsCommand_SetsReturnsActive()
        {
            var vm = CreateViewModel();

            vm.ShowReturnsCommand.Execute(null);

            Assert.True(vm.IsReturnsActive);
        }

        [Fact]
        public void ShowReturnsCommand_SetsPosInactive()
        {
            var vm = CreateViewModel();
            vm.ShowPosCommand.Execute(null);

            vm.ShowReturnsCommand.Execute(null);

            Assert.False(vm.IsPosActive);
        }

        [Fact]
        public void ShowReturnsCommand_SetsCurrentViewToReturnsViewModel()
        {
            var vm = CreateViewModel();

            vm.ShowReturnsCommand.Execute(null);

            Assert.IsType<ReturnsViewModel>(vm.CurrentView);
        }

        [Fact]
        public void ShowReturnsCommand_Twice_ReusesReturnsViewModel()
        {
            var vm = CreateViewModel();
            vm.ShowReturnsCommand.Execute(null);
            var returns = vm.CurrentView;
            vm.ShowPosCommand.Execute(null);

            vm.ShowReturnsCommand.Execute(null);

            Assert.Same(returns, vm.CurrentView);
        }

        [Fact]
        public void ShowPosCommand_AfterReturns_SetsReturnsInactive()
        {
            var vm = CreateViewModel();
            vm.ShowReturnsCommand.Execute(null);

            vm.ShowPosCommand.Execute(null);

            Assert.False(vm.IsReturnsActive);
        }

        [Fact]
        public void UserEmail_ReturnsAuthEmail()
        {
            var vm = CreateViewModel();

            Assert.Equal("boss@store.com", vm.UserEmail);
        }

        [Fact]
        public void UserRole_ReturnsAuthRole()
        {
            var vm = CreateViewModel();

            Assert.Equal("Admin", vm.UserRole);
        }

        [Fact]
        public void StoreName_ReturnsAuthStoreName()
        {
            var vm = CreateViewModel();

            Assert.Equal("Goat Store Kyiv", vm.StoreName);
        }

        [Fact]
        public void Constructor_WhenRoleIsSeller_SetsIsAdminFalse()
        {
            var auth = new AuthResponseDto { Role = UserRole.Seller };
            var vm = new ShellViewModel(auth, _api);

            Assert.False(vm.IsAdmin);
        }

        [Fact]
        public void Constructor_WhenRoleIsSeller_SetsPosActive()
        {
            var auth = new AuthResponseDto { Role = UserRole.Seller };
            var vm = new ShellViewModel(auth, _api);

            Assert.True(vm.IsPosActive);
        }

        [Fact]
        public void Constructor_WhenRoleIsSeller_SetsInventoryInactive()
        {
            var auth = new AuthResponseDto { Role = UserRole.Seller };
            var vm = new ShellViewModel(auth, _api);

            Assert.False(vm.IsInventoryActive);
        }

        [Fact]
        public void Constructor_WhenRoleIsSeller_SetsCurrentViewToMainViewModel()
        {
            var auth = new AuthResponseDto { Role = UserRole.Seller };
            var vm = new ShellViewModel(auth, _api);

            Assert.IsType<MainViewModel>(vm.CurrentView);
        }

        [Fact]
        public void Constructor_WhenRoleIsAdmin_SetsIsAdminTrue()
        {
            var vm = CreateViewModel();

            Assert.True(vm.IsAdmin);
        }

        [Fact]
        public void ShowAnalyticsCommand_SetsAnalyticsActive()
        {
            var vm = CreateViewModel();

            vm.ShowAnalyticsCommand.Execute(null);

            Assert.True(vm.IsAnalyticsActive);
        }

        [Fact]
        public void ShowAnalyticsCommand_SetsInventoryInactive()
        {
            var vm = CreateViewModel();

            vm.ShowAnalyticsCommand.Execute(null);

            Assert.False(vm.IsInventoryActive);
        }

        [Fact]
        public void ShowAnalyticsCommand_SetsPosInactive()
        {
            var vm = CreateViewModel();
            vm.ShowPosCommand.Execute(null);

            vm.ShowAnalyticsCommand.Execute(null);

            Assert.False(vm.IsPosActive);
        }

        [Fact]
        public void ShowAnalyticsCommand_SetsCurrentViewToAnalyticsViewModel()
        {
            var vm = CreateViewModel();

            vm.ShowAnalyticsCommand.Execute(null);

            Assert.IsType<AnalyticsViewModel>(vm.CurrentView);
        }

        [Fact]
        public void ShowAnalyticsCommand_Twice_ReusesAnalyticsViewModel()
        {
            var vm = CreateViewModel();
            vm.ShowAnalyticsCommand.Execute(null);
            var analytics = vm.CurrentView;
            vm.ShowPosCommand.Execute(null);

            vm.ShowAnalyticsCommand.Execute(null);

            Assert.Same(analytics, vm.CurrentView);
        }

        [Fact]
        public void LogoutCommand_ClearsAuthSession()
        {
            AuthSession.Start(new AuthResponseDto
            {
                AccessToken = "test-token",
                AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            var vm = CreateViewModel();

            vm.LogoutCommand.Execute(null);

            Assert.False(AuthSession.IsActive);
        }
    }
}
