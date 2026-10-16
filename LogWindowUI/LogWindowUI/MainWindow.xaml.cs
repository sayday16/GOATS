using System.Windows;
using GoatStore.Core.DTOs;
using LogWindowUI.ViewModels;

namespace LogWindowUI
{
    public partial class MainWindow : Window
    {
        public MainWindow() : this(null)
        {
        }

        public MainWindow(AuthResponseDto? authResponse)
        {
            InitializeComponent();
            DataContext = new MainViewModel(authResponse);
        }
    }
}