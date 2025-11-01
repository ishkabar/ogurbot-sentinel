using System.Windows.Controls;
using Ogur.Sentinel.Devexpress.ViewModels;
using Ogur.Sentinel.Devexpress.Services;
using Ogur.Sentinel.Devexpress.Config;

namespace Ogur.Sentinel.Devexpress.Views
{
    public partial class SettingsView : Page
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsView(ApiClient apiClient, DesktopSettings settings)
        {
            InitializeComponent();

            _viewModel = new SettingsViewModel(apiClient, settings);
            DataContext = _viewModel;
        }
    }
}