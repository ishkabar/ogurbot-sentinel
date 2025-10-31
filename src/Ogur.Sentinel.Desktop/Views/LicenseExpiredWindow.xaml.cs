using System.Windows;

namespace Ogur.Sentinel.Desktop.Views;

public partial class LicenseExpiredWindow : Window
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