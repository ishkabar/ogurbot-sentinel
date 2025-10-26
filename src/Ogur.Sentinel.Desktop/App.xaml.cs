using System.Runtime.InteropServices;
using System.Windows;
using System.Windows;

namespace Ogur.Sentinel.Desktop;


public partial class App : Application
{
    private const bool SHOW_CONSOLE = false;

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        if (SHOW_CONSOLE)
        {
            AllocConsole();
            Console.WriteLine("🚀 Sentinel Desktop Console");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (SHOW_CONSOLE)
        {
            FreeConsole();
        }
        base.OnExit(e);
    }
}
