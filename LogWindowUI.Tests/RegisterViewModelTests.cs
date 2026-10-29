using GoatStore.DataAccess.Entities;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class RegisterViewModelTests
    {
        private readonly FakeAuthService _authService = new();

        private RegisterViewModel CreateFilledViewModel() => new(_authService)
        {
            Email = "owner@test.com",
            Password = "password123",
            ConfirmPassword = "password123",
            StoreName = "GoatShop",
            StorePassword = "storecode123"
        };

        [Theory]
        [InlineData(nameof(RegisterViewModel.Email))]
        [InlineData(nameof(RegisterViewModel.Password))]
        [InlineData(nameof(RegisterViewModel.ConfirmPassword))]
        [InlineData(nameof(RegisterViewModel.StoreName))]
        [InlineData(nameof(RegisterViewModel.StorePassword))]
        public void RegisterCommand_AnyFieldEmpty_CannotExecute(string field)
        {
            var viewModel = CreateFilledViewModel();
            typeof(RegisterViewModel).GetProperty(field)!.SetValue(viewModel, "  ");

            Assert.False(viewModel.RegisterCommand.CanExecute(null));
        }

        [Fact]
        public void RegisterCommand_AllFieldsFilled_CanExecute()
        {
            Assert.True(CreateFilledViewModel().RegisterCommand.CanExecute(null));
        }

        [Fact]
        public void RegisterCommand_ServiceReturnsError_ShowsErrorAndClearsSuccess()
        {
            _authService.Response = new() { IsSuccess = false, Message = "Користувач із таким Email вже існує." };
            var viewModel = CreateFilledViewModel();
            viewModel.SuccessMessage = "старе повідомлення";

            viewModel.RegisterCommand.Execute(null);

            Assert.Equal("Користувач із таким Email вже існує.", viewModel.ErrorMessage);
            Assert.True(viewModel.HasError);
            Assert.False(viewModel.HasSuccess);
        }

        [Fact]
        public void RegisterCommand_WhileRequestInProgress_CannotExecuteAgain()
        {
            _authService.Pending = new();
            var viewModel = CreateFilledViewModel();

            viewModel.RegisterCommand.Execute(null);
            var canExecuteWhileBusy = viewModel.RegisterCommand.CanExecute(null);
            _authService.Pending.SetResult(new() { IsSuccess = false, Message = "Помилка." });

            Assert.False(canExecuteWhileBusy);
            Assert.True(viewModel.RegisterCommand.CanExecute(null));
        }

        [Fact]
        public void RegisterCommand_ServiceThrows_ShowsConnectionError()
        {
            _authService.Exception = new HttpRequestException("Connection refused");
            var viewModel = CreateFilledViewModel();

            viewModel.RegisterCommand.Execute(null);

            Assert.StartsWith("Не вдалося зв'язатися із сервером", viewModel.ErrorMessage);
        }

        [Theory]
        [InlineData(true, UserRole.Admin)]
        [InlineData(false, UserRole.Seller)]
        public void RegisterCommand_SendsSelectedRoleAndFields(bool isAdmin, UserRole expectedRole)
        {
            var viewModel = CreateFilledViewModel();
            if (isAdmin) viewModel.IsAdmin = true; else viewModel.IsSeller = true;

            viewModel.RegisterCommand.Execute(null);

            var request = _authService.LastRegister;
            Assert.NotNull(request);
            Assert.Equal(expectedRole, request.Role);
            Assert.Equal("owner@test.com", request.Email);
            Assert.Equal("GoatShop", request.StoreName);
            Assert.Equal("storecode123", request.StorePassword);
        }

        [Fact]
        public void DefaultConstructor_CreatesCommands()
        {
            var viewModel = new RegisterViewModel();

            Assert.NotNull(viewModel.RegisterCommand);
            Assert.NotNull(viewModel.GoToLoginCommand);
        }

        [Fact]
        public void SuccessMessage_Set_ShowsSuccess()
        {
            var viewModel = CreateFilledViewModel();

            viewModel.SuccessMessage = "Магазин створено";

            Assert.Equal("Магазин створено", viewModel.SuccessMessage);
            Assert.True(viewModel.HasSuccess);
        }

        [Fact]
        public void SelectedRole_ChangesStoreLabels()
        {
            var viewModel = CreateFilledViewModel();

            viewModel.IsSeller = true;
            var sellerLabel = viewModel.StoreNameLabel;
            var sellerPlaceholder = viewModel.StorePasswordPlaceholder;
            var sellerNamePlaceholder = viewModel.StoreNamePlaceholder;
            viewModel.IsAdmin = true;

            Assert.NotEqual(sellerLabel, viewModel.StoreNameLabel);
            Assert.NotEqual(sellerPlaceholder, viewModel.StorePasswordPlaceholder);
            Assert.NotEqual(sellerNamePlaceholder, viewModel.StoreNamePlaceholder);
            Assert.True(viewModel.IsAdmin);
            Assert.False(viewModel.IsSeller);
        }
    }
}
