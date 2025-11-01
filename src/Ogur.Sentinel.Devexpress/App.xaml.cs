using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using DevExpress.Xpf.Core;

namespace Ogur.Sentinel.Devexpress
{
    public partial class App : Application
    {
        public static bool DebugMode { get; private set; }

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Sprawdź argumenty wiersza poleceń
            var args = e.Args.Select(a => a.ToLower()).ToArray();
            DebugMode = args.Contains("--console") || 
                       args.Contains("-console") || 
                       args.Contains("--debug") || 
                       args.Contains("-debug");

            if (DebugMode)
            {
                AllocConsole();
                Console.WriteLine("🚀 Sentinel Desktop Console (Debug Mode)");
                Console.WriteLine("📋 Command line arguments:");
                foreach (var arg in e.Args)
                {
                    Console.WriteLine($"   - {arg}");
                }
                Console.WriteLine();
            }
            
            DispatcherUnhandledException += (s, args) =>
            {
                var ex = args.Exception;
                var errorMessage = $"ERROR: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
    
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nInner Exception:\n{ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                }
    
                if (DebugMode)
                {
                    Console.WriteLine(errorMessage);
                }
    
                DXMessageBox.Show(
                    errorMessage,
                    "Error Details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                args.Handled = true;
            };

            // Ustaw motyw na VS2019Dark
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
            if (DebugMode) 
            {
                Console.WriteLine("👋 Application exiting...");
                FreeConsole();
            }
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