using System;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Xpf.Editors;
using Ogur.Sentinel.Devexpress.ViewModels;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress.Views
{
    public partial class LoginView : Page
    {
        private readonly LoginViewModel _viewModel;
        private readonly MainWindow _mainWindow;

        public LoginView(ApiClient apiClient, MainWindow mainWindow, DesktopSettings settings, bool tryAutoLogin = true)
        {
            InitializeComponent();

            _mainWindow = mainWindow;
            _viewModel = new LoginViewModel(apiClient, settings, tryAutoLogin);
            DataContext = _viewModel;
            

            Messenger.Default.Register<ViewModels.NavigateMessage>(this, OnNavigate);
        }

        private void OnNavigate(ViewModels.NavigateMessage message)
        {
            if (message.Target == "Timers")
            {
                _mainWindow.NavigateToTimers();
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.LoginCommand.CanExecute(null))
            {
                _viewModel.LoginCommand.Execute(null);
            }
        }
    }
}