using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
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
        public void ShowInventoryCommand_AfterPos_RestoresInventoryView()
        {
            var vm = CreateViewModel();
            vm.ShowPosCommand.Execute(null);

            vm.ShowInventoryCommand.Execute(null);

            Assert.IsType<InventoryViewModel>(vm.CurrentView);
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
    }
}
