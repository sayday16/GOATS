using System.Windows;
using System.Windows.Controls;

namespace LogWindowUI.Views
{
    public partial class RegisterView : Window
    {
        private bool _isPasswordVisible = false;
        private bool _isConfirmPasswordVisible = false;
        private bool _isStorePasswordVisible = false;

        public RegisterView()
        {
            InitializeComponent();
        }

        private void HiddenPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(HiddenPasswordBox.Password) ? Visibility.Visible : Visibility.Hidden;

            if (!_isPasswordVisible)
            {
                VisiblePasswordBox.Text = HiddenPasswordBox.Password;
            }

            if (DataContext is ViewModels.RegisterViewModel vm)
            {
                vm.Password = HiddenPasswordBox.Password;
            }
        }

        private void VisiblePasswordBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(VisiblePasswordBox.Text) ? Visibility.Visible : Visibility.Hidden;

            if (_isPasswordVisible)
            {
                HiddenPasswordBox.Password = VisiblePasswordBox.Text;
            }
        }

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                VisiblePasswordBox.Visibility = Visibility.Visible;
                HiddenPasswordBox.Visibility = Visibility.Collapsed;
                IconEyeOpen.Visibility = Visibility.Collapsed;
                IconEyeClosed.Visibility = Visibility.Visible;
            }
            else
            {
                VisiblePasswordBox.Visibility = Visibility.Collapsed;
                HiddenPasswordBox.Visibility = Visibility.Visible;
                IconEyeOpen.Visibility = Visibility.Visible;
                IconEyeClosed.Visibility = Visibility.Collapsed;
                HiddenPasswordBox.Focus();
            }
        }

        private void HiddenConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(HiddenConfirmPasswordBox.Password) ? Visibility.Visible : Visibility.Hidden;

            if (!_isConfirmPasswordVisible)
            {
                VisibleConfirmPasswordBox.Text = HiddenConfirmPasswordBox.Password;
            }

            if (DataContext is ViewModels.RegisterViewModel vm)
            {
                vm.ConfirmPassword = HiddenConfirmPasswordBox.Password;
            }
        }

        private void VisibleConfirmPasswordBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(VisibleConfirmPasswordBox.Text) ? Visibility.Visible : Visibility.Hidden;

            if (_isConfirmPasswordVisible)
            {
                HiddenConfirmPasswordBox.Password = VisibleConfirmPasswordBox.Text;
            }
        }

        private void ToggleConfirmPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _isConfirmPasswordVisible = !_isConfirmPasswordVisible;

            if (_isConfirmPasswordVisible)
            {
                VisibleConfirmPasswordBox.Visibility = Visibility.Visible;
                HiddenConfirmPasswordBox.Visibility = Visibility.Collapsed;
                IconConfirmEyeOpen.Visibility = Visibility.Collapsed;
                IconConfirmEyeClosed.Visibility = Visibility.Visible;
            }
            else
            {
                VisibleConfirmPasswordBox.Visibility = Visibility.Collapsed;
                HiddenConfirmPasswordBox.Visibility = Visibility.Visible;
                IconConfirmEyeOpen.Visibility = Visibility.Visible;
                IconConfirmEyeClosed.Visibility = Visibility.Collapsed;
                HiddenConfirmPasswordBox.Focus();
            }
        }

        private void HiddenStorePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            StorePasswordPlaceholder.Visibility = string.IsNullOrEmpty(HiddenStorePasswordBox.Password) ? Visibility.Visible : Visibility.Hidden;

            if (!_isStorePasswordVisible)
            {
                VisibleStorePasswordBox.Text = HiddenStorePasswordBox.Password;
            }

            if (DataContext is ViewModels.RegisterViewModel vm)
            {
                vm.StorePassword = HiddenStorePasswordBox.Password;
            }
        }

        private void VisibleStorePasswordBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            StorePasswordPlaceholder.Visibility = string.IsNullOrEmpty(VisibleStorePasswordBox.Text) ? Visibility.Visible : Visibility.Hidden;

            if (_isStorePasswordVisible)
            {
                HiddenStorePasswordBox.Password = VisibleStorePasswordBox.Text;
            }
        }

        private void ToggleStorePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            _isStorePasswordVisible = !_isStorePasswordVisible;

            if (_isStorePasswordVisible)
            {
                VisibleStorePasswordBox.Visibility = Visibility.Visible;
                HiddenStorePasswordBox.Visibility = Visibility.Collapsed;
                IconStoreEyeOpen.Visibility = Visibility.Collapsed;
                IconStoreEyeClosed.Visibility = Visibility.Visible;
            }
            else
            {
                VisibleStorePasswordBox.Visibility = Visibility.Collapsed;
                HiddenStorePasswordBox.Visibility = Visibility.Visible;
                IconStoreEyeOpen.Visibility = Visibility.Visible;
                IconStoreEyeClosed.Visibility = Visibility.Collapsed;
                HiddenStorePasswordBox.Focus();
            }
        }
    }
}