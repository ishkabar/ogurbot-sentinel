using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using DevExpress.Xpf.Core;

namespace Ogur.Sentinel.Devexpress
{
    public partial class App : Application
    {
        private const bool SHOW_CONSOLE = true;

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
            
            DispatcherUnhandledException += (s, args) =>
            {
                var ex = args.Exception;
                var errorMessage = $"ERROR: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
    
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nInner Exception:\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                }
    
                Console.WriteLine(errorMessage);
    
                DXMessageBox.Show(
                    errorMessage,
                    "Error Details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                args.Handled = true;
            };

            // ZMIENIONE: Ustaw motyw na VS2019Dark
            ApplicationThemeHelper.ApplicationThemeName = Theme.VS2019DarkName;

            // Wyłącz Trace (bez Event Log)
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new CustomTraceListener());
            
            // Obsługa błędów
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                DXMessageBox.Show(
                    ex?.Message ?? "Unknown error",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            };
            
            DispatcherUnhandledException += (s, args) =>
            {
                DXMessageBox.Show(
                    args.Exception.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                args.Handled = true;
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (SHOW_CONSOLE) FreeConsole();
            base.OnExit(e);
        }
    }

    public class CustomTraceListener : TraceListener
    {
        public override void Write(string message) { }
        public override void WriteLine(string message) { }
        
        public override void Fail(string message, string detailMessage)
        {
            DXMessageBox.Show(
                $"{message}\n\n{detailMessage}",
                "DevExpress Assertion",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }
}