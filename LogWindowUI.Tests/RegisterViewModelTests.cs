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
        public void RegisterCommand_ServiceReturnsError_ShowsErrorMessage()
        {
            _authService.Response = new() { IsSuccess = false, Message = "Користувач із таким Email вже існує." };
            var viewModel = CreateFilledViewModel();
            viewModel.SuccessMessage = "старе повідомлення";

            viewModel.RegisterCommand.Execute(null);

            Assert.Equal("Користувач із таким Email вже існує.", viewModel.ErrorMessage);
        }

        [Fact]
        public void RegisterCommand_ServiceReturnsError_SetsHasError()
        {
            _authService.Response = new() { IsSuccess = false, Message = "Користувач із таким Email вже існує." };
            var viewModel = CreateFilledViewModel();
            viewModel.SuccessMessage = "старе повідомлення";

            viewModel.RegisterCommand.Execute(null);

            Assert.True(viewModel.HasError);
        }

        [Fact]
        public void RegisterCommand_ServiceReturnsError_ClearsHasSuccess()
        {
            _authService.Response = new() { IsSuccess = false, Message = "Користувач із таким Email вже існує." };
            var viewModel = CreateFilledViewModel();
            viewModel.SuccessMessage = "старе повідомлення";

            viewModel.RegisterCommand.Execute(null);

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
        }

        [Fact]
        public void RegisterCommand_AfterRequestCompletes_CanExecuteAgain()
        {
            _authService.Pending = new();
            var viewModel = CreateFilledViewModel();

            viewModel.RegisterCommand.Execute(null);
            _authService.Pending.SetResult(new() { IsSuccess = false, Message = "Помилка." });

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
        public void RegisterCommand_SendsSelectedRole(bool isAdmin, UserRole expectedRole)
        {
            var viewModel = CreateFilledViewModel();
            if (isAdmin) viewModel.IsAdmin = true; else viewModel.IsSeller = true;

            viewModel.RegisterCommand.Execute(null);

            Assert.Equal(expectedRole, _authService.LastRegister?.Role);
        }

        [Fact]
        public void RegisterCommand_SendsEmail()
        {
            var viewModel = CreateFilledViewModel();

            viewModel.RegisterCommand.Execute(null);

            Assert.Equal("owner@test.com", _authService.LastRegister?.Email);
        }

        [Fact]
        public void RegisterCommand_SendsStoreName()
        {
            var viewModel = CreateFilledViewModel();

            viewModel.RegisterCommand.Execute(null);

            Assert.Equal("GoatShop", _authService.LastRegister?.StoreName);
        }

        [Fact]
        public void RegisterCommand_SendsStorePassword()
        {
            var viewModel = CreateFilledViewModel();

            viewModel.RegisterCommand.Execute(null);

            Assert.Equal("storecode123", _authService.LastRegister?.StorePassword);
        }

        [Fact]
        public void DefaultConstructor_CreatesRegisterCommand()
        {
            var viewModel = new RegisterViewModel();

            Assert.NotNull(viewModel.RegisterCommand);
        }

        [Fact]
        public void DefaultConstructor_CreatesGoToLoginCommand()
        {
            var viewModel = new RegisterViewModel();

            Assert.NotNull(viewModel.GoToLoginCommand);
        }

        [Fact]
        public void SuccessMessage_Set_UpdatesProperty()
        {
            var viewModel = CreateFilledViewModel();

            viewModel.SuccessMessage = "Магазин створено";

            Assert.Equal("Магазин створено", viewModel.SuccessMessage);
        }

        [Fact]
        public void SuccessMessage_Set_SetsHasSuccess()
        {
            var viewModel = CreateFilledViewModel();

            viewModel.SuccessMessage = "Магазин створено";

            Assert.True(viewModel.HasSuccess);
        }

        [Fact]
        public void SelectedRole_ChangesStoreNameLabel()
        {
            var viewModel = CreateFilledViewModel();
            viewModel.IsSeller = true;
            var sellerLabel = viewModel.StoreNameLabel;
            viewModel.IsAdmin = true;

            Assert.NotEqual(sellerLabel, viewModel.StoreNameLabel);
        }

        [Fact]
        public void SelectedRole_ChangesStorePasswordPlaceholder()
        {
            var viewModel = CreateFilledViewModel();
            viewModel.IsSeller = true;
            var sellerPlaceholder = viewModel.StorePasswordPlaceholder;
            viewModel.IsAdmin = true;

            Assert.NotEqual(sellerPlaceholder, viewModel.StorePasswordPlaceholder);
        }

        [Fact]
        public void SelectedRole_ChangesStoreNamePlaceholder()
        {
            var viewModel = CreateFilledViewModel();
            viewModel.IsSeller = true;
            var sellerNamePlaceholder = viewModel.StoreNamePlaceholder;
            viewModel.IsAdmin = true;

            Assert.NotEqual(sellerNamePlaceholder, viewModel.StoreNamePlaceholder);
        }

        [Fact]
        public void SelectedRole_Admin_SetsIsAdminTrue()
        {
            var viewModel = CreateFilledViewModel();
            viewModel.IsSeller = true;
            viewModel.IsAdmin = true;

            Assert.True(viewModel.IsAdmin);
        }

        [Fact]
        public void SelectedRole_Admin_SetsIsSellerFalse()
        {
            var viewModel = CreateFilledViewModel();
            viewModel.IsSeller = true;
            viewModel.IsAdmin = true;

            Assert.False(viewModel.IsSeller);
        }
    }
}
