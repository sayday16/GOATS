using System.Windows;
using GoatStore.Core.DTOs;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Views
{
    public partial class ShellWindow : Window
    {
        public ShellWindow(AuthResponseDto? authResponse = null)
        {
            InitializeComponent();
            DataContext = new ShellViewModel(authResponse);
        }
    }
}
