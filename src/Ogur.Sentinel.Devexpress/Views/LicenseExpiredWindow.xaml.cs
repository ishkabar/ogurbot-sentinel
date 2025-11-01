using System;
using System.Windows;
using DevExpress.Xpf.Core;

namespace Ogur.Sentinel.Devexpress.Views
{
    public partial class LicenseExpiredWindow : ThemedWindow
    {
        public LicenseExpiredWindow(DateTime expirationDate)
        {
            InitializeComponent();

            ExpirationDateText.Text = $"(Wygasła: {expirationDate:dd.MM.yyyy})";

            System.Media.SystemSounds.Hand.Play();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}