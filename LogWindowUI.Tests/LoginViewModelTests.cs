using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class LoginViewModelTests
    {
        private readonly FakeAuthService _authService = new();

        private LoginViewModel CreateViewModel(string? email = "owner@test.com", string? password = "password123") =>
            new(_authService) { Username = email, Password = password };

        [Theory]
        [InlineData(null, "password123")]
        [InlineData("", "password123")]
        [InlineData("   ", "password123")]
        [InlineData("owner@test.com", null)]
        [InlineData("owner@test.com", "")]
        public void LoginCommand_EmptyFields_CannotExecute(string? email, string? password)
        {
            var viewModel = CreateViewModel(email, password);

            Assert.False(viewModel.LoginCommand.CanExecute(null));
        }

        [Fact]
        public void DefaultConstructor_CreatesLoginCommand()
        {
            var viewModel = new LoginViewModel();

            Assert.NotNull(viewModel.LoginCommand);
        }

        [Fact]
        public void DefaultConstructor_CreatesGoToRegisterCommand()
        {
            var viewModel = new LoginViewModel();

            Assert.NotNull(viewModel.GoToRegisterCommand);
        }

        [Fact]
        public void LoginCommand_FilledFields_CanExecute()
        {
            Assert.True(CreateViewModel().LoginCommand.CanExecute(null));
        }

        [Fact]
        public void LoginCommand_ServiceReturnsError_ShowsMessage()
        {
            _authService.Response = new() { IsSuccess = false, Message = "Невірний Email або пароль." };
            var viewModel = CreateViewModel();

            viewModel.LoginCommand.Execute(null);

            Assert.Equal("Невірний Email або пароль.", viewModel.ErrorMessage);
        }

        [Fact]
        public void LoginCommand_ServiceReturnsError_SetsHasError()
        {
            _authService.Response = new() { IsSuccess = false, Message = "Невірний Email або пароль." };
            var viewModel = CreateViewModel();

            viewModel.LoginCommand.Execute(null);

            Assert.True(viewModel.HasError);
        }

        [Fact]
        public void LoginCommand_ServiceReturnsError_CanExecuteAgain()
        {
            _authService.Response = new() { IsSuccess = false, Message = "Невірний Email або пароль." };
            var viewModel = CreateViewModel();

            viewModel.LoginCommand.Execute(null);

            Assert.True(viewModel.LoginCommand.CanExecute(null));
        }

        [Fact]
        public void LoginCommand_ServiceThrows_ShowsConnectionError()
        {
            _authService.Exception = new HttpRequestException("Connection refused");
            var viewModel = CreateViewModel();

            viewModel.LoginCommand.Execute(null);

            Assert.StartsWith("Не вдалося зв'язатися із сервером", viewModel.ErrorMessage);
        }

        [Fact]
        public void LoginCommand_ServiceThrows_SetsHasError()
        {
            _authService.Exception = new HttpRequestException("Connection refused");
            var viewModel = CreateViewModel();

            viewModel.LoginCommand.Execute(null);

            Assert.True(viewModel.HasError);
        }

        [Fact]
        public void LoginCommand_WhileRequestInProgress_CannotExecuteAgain()
        {
            _authService.Pending = new();
            var viewModel = CreateViewModel();

            viewModel.LoginCommand.Execute(null);
            var canExecuteWhileBusy = viewModel.LoginCommand.CanExecute(null);
            _authService.Pending.SetResult(new() { IsSuccess = false, Message = "Невірний Email або пароль." });

            Assert.False(canExecuteWhileBusy);
        }

        [Fact]
        public void LoginCommand_AfterRequestCompletes_CanExecuteAgain()
        {
            _authService.Pending = new();
            var viewModel = CreateViewModel();

            viewModel.LoginCommand.Execute(null);
            _authService.Pending.SetResult(new() { IsSuccess = false, Message = "Невірний Email або пароль." });

            Assert.True(viewModel.LoginCommand.CanExecute(null));
        }

        [Fact]
        public void LoginCommand_NewAttempt_ClearsPreviousError()
        {
            _authService.Pending = new();
            var viewModel = CreateViewModel();
            viewModel.ErrorMessage = "Стара помилка";

            viewModel.LoginCommand.Execute(null);
            var hasErrorWhileBusy = viewModel.HasError;
            _authService.Pending.SetResult(new() { IsSuccess = false, Message = "Помилка." });

            Assert.False(hasErrorWhileBusy);
        }

        [Fact]
        public void LoginCommand_SendsEnteredEmail()
        {
            var viewModel = CreateViewModel("owner@test.com", "secret123");

            viewModel.LoginCommand.Execute(null);

            Assert.Equal("owner@test.com", _authService.LastLogin?.Email);
        }

        [Fact]
        public void LoginCommand_SendsEnteredPassword()
        {
            var viewModel = CreateViewModel("owner@test.com", "secret123");

            viewModel.LoginCommand.Execute(null);

            Assert.Equal("secret123", _authService.LastLogin?.Password);
        }
    }
}
