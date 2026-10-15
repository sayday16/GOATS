using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;

namespace LogWindowUI.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private string? _username;
        private string? _password;
        private string? _errorMessage;
        private bool _isBusy;

        public string? Username
        {
            get => _username;
            set
            {
                _username = value;
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

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

        public ICommand LoginCommand { get; }
        public ICommand GoToRegisterCommand { get; }

        public LoginViewModel() : this(App.CreateAuthService())
        {
        }

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
            LoginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);
            GoToRegisterCommand = new RelayCommand(ExecuteGoToRegister);
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !_isBusy &&
                   !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Password);
        }

        private async void ExecuteLogin(object? parameter)
        {
            _isBusy = true;
            ErrorMessage = string.Empty;
            CommandManager.InvalidateRequerySuggested();

            try
            {
                var request = new LoginRequestDto
                {
                    Email = Username ?? string.Empty,
                    Password = Password ?? string.Empty
                };

                AuthResponseDto response = await _authService.LoginAsync(request);

                if (response.IsSuccess)
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    CloseCurrentWindow();
                }
                else
                {
                    ErrorMessage = response.Message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Помилка з'єднання з базою даних: {ex.Message}";
            }
            finally
            {
                _isBusy = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ExecuteGoToRegister(object? parameter)
        {
            IAuthService authService = App.CreateAuthService();
            var registerViewModel = new RegisterViewModel(authService);
            var registerWindow = new Views.RegisterView
            {
                DataContext = registerViewModel
            };
            registerWindow.Show();
            CloseCurrentWindow();
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

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}