using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.DataAccess.Entities;

namespace LogWindowUI.ViewModels
{
    public class RegisterViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private string? _email;
        private string? _password;
        private string? _confirmPassword;
        private UserRole _selectedRole = UserRole.Seller;
        private string? _storeName;
        private string? _storePassword;
        private string? _errorMessage;
        private string? _successMessage;
        private bool _isBusy;

        public string? Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string? Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string? ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                _confirmPassword = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public UserRole SelectedRole
        {
            get => _selectedRole;
            set
            {
                _selectedRole = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(IsSeller));
                OnPropertyChanged(nameof(StoreNameLabel));
                OnPropertyChanged(nameof(StoreNamePlaceholder));
                OnPropertyChanged(nameof(StorePasswordPlaceholder));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsAdmin
        {
            get => _selectedRole == UserRole.Admin;
            set { if (value) SelectedRole = UserRole.Admin; }
        }

        public bool IsSeller
        {
            get => _selectedRole == UserRole.Seller;
            set { if (value) SelectedRole = UserRole.Seller; }
        }

        public string StoreNameLabel =>
            _selectedRole == UserRole.Admin ? "Назва магазину (нового)" : "Назва магазину (існуючого)";

        public string StoreNamePlaceholder =>
            _selectedRole == UserRole.Admin ? "Придумайте назву магазину" : "Введіть назву існуючого магазину";

        public string StorePasswordPlaceholder =>
            _selectedRole == UserRole.Admin ? "Придумайте секретний код магазину" : "Введіть секретний код магазину";

        public string? StoreName
        {
            get => _storeName;
            set
            {
                _storeName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string? StorePassword
        {
            get => _storePassword;
            set
            {
                _storePassword = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public string? SuccessMessage
        {
            get => _successMessage;
            set
            {
                _successMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSuccess));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);
        public bool HasSuccess => !string.IsNullOrWhiteSpace(_successMessage);

        public ICommand RegisterCommand { get; }
        public ICommand GoToLoginCommand { get; }

        public RegisterViewModel() : this(App.CreateAuthService())
        {
        }

        public RegisterViewModel(IAuthService authService)
        {
            _authService = authService;
            RegisterCommand = new RelayCommand(ExecuteRegister, CanExecuteRegister);
            GoToLoginCommand = new RelayCommand(ExecuteGoToLogin);
        }

        private bool CanExecuteRegister(object? parameter)
        {
            return !_isBusy &&
                   !string.IsNullOrWhiteSpace(Email) &&
                   !string.IsNullOrWhiteSpace(Password) &&
                   !string.IsNullOrWhiteSpace(ConfirmPassword) &&
                   !string.IsNullOrWhiteSpace(StoreName) &&
                   !string.IsNullOrWhiteSpace(StorePassword);
        }

        private async void ExecuteRegister(object? parameter)
        {
            _isBusy = true;
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                var request = new RegisterRequestDto
                {
                    Email = Email ?? string.Empty,
                    Password = Password ?? string.Empty,
                    ConfirmPassword = ConfirmPassword ?? string.Empty,
                    Role = SelectedRole,
                    StoreName = StoreName ?? string.Empty,
                    StorePassword = StorePassword ?? string.Empty
                };

                AuthResponseDto response = await _authService.RegisterAsync(request);

                if (response.IsSuccess)
                {
                    SuccessMessage = response.Message;
                    ErrorMessage = string.Empty;

                    await System.Threading.Tasks.Task.Delay(1500);
                    ExecuteGoToLogin(null);
                }
                else
                {
                    ErrorMessage = response.Message;
                    SuccessMessage = string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не вдалося зв'язатися із сервером: {ex.Message}";
            }
            finally
            {
                _isBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ExecuteGoToLogin(object? parameter)
        {
            var loginWindow = new Views.LoginView();
            IAuthService authService = App.CreateAuthService();
            loginWindow.DataContext = new LoginViewModel(authService);
            loginWindow.Show();
            CloseCurrentWindow();
            _authService.Dispose();
        }

        private void CloseCurrentWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
