using System.Windows;
using System.Windows.Controls;

namespace LogWindowUI.Views
{
    public partial class LoginView : Window
    {
        private bool _isPasswordVisible = false;

        public LoginView()
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

            if (DataContext is ViewModels.LoginViewModel vm)
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
    }
}