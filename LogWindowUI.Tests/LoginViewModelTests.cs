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
            Assert.True(viewModel.HasError);
            Assert.True(viewModel.LoginCommand.CanExecute(null));
        }

        [Fact]
        public void LoginCommand_ServiceThrows_ShowsConnectionError()
        {
            _authService.Exception = new HttpRequestException("Connection refused");
            var viewModel = CreateViewModel();

            viewModel.LoginCommand.Execute(null);

            Assert.StartsWith("Не вдалося зв'язатися із сервером", viewModel.ErrorMessage);
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
            Assert.True(viewModel.LoginCommand.CanExecute(null));
        }

        [Fact]
        public void LoginCommand_NewAttempt_ClearsPreviousError()
        {
            _authService.Pending = new();
            var viewModel = CreateViewModel();
            viewModel.ErrorMessage = "Стара помилка";

            viewModel.LoginCommand.Execute(null);

            Assert.False(viewModel.HasError);
            _authService.Pending.SetResult(new() { IsSuccess = false, Message = "Помилка." });
        }

        [Fact]
        public void LoginCommand_SendsEnteredEmailAndPassword()
        {
            var viewModel = CreateViewModel("owner@test.com", "secret123");

            viewModel.LoginCommand.Execute(null);

            Assert.NotNull(_authService.LastLogin);
            Assert.Equal("owner@test.com", _authService.LastLogin.Email);
            Assert.Equal("secret123", _authService.LastLogin.Password);
        }
    }
}
