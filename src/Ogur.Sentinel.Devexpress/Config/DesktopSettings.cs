using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ogur.Sentinel.Devexpress.Config;

public class DesktopSettings
{
    // ✅ Ścieżka do AppData\Local\OgurSentinel
    private static string AppDataFolder => 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OgurSentinel");
    
    private static string SettingsFilePath => 
        Path.Combine(AppDataFolder, "appsettings.json");

    // ✅ Ustawienia API i logowania
    public string ApiUrl { get; set; } = "http://localhost:5205";
    public string Username { get; set; } = "";
    public string HashedPassword { get; set; } = ""; // SHA256 hash

    // ✅ Ustawienia UI i synchronizacji
    public bool AlwaysOnTop { get; set; } = true;
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public int SyncIntervalSeconds { get; set; } = 30;
    public int TimeOffsetSeconds { get; set; } = 0;  // ✅ Offset czasu (może być ujemny)
    public int WarningMinutesRed { get; set; } = 5;
    public int WarningMinutesOrange { get; set; } = 10;

    // ✅ Helper do kodowania hasła (base64 - można odkodować)
    public static string EncodePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return "";
            
        var bytes = Encoding.UTF8.GetBytes(password);
        return Convert.ToBase64String(bytes);
    }

    // ✅ Helper do odkodowania hasła
    public static string DecodePassword(string encodedPassword)
    {
        if (string.IsNullOrEmpty(encodedPassword))
            return "";
            
        try
        {
            var bytes = Convert.FromBase64String(encodedPassword);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }

    // ✅ Ustaw hasło (automatycznie koduje)
    public void SetPassword(string password)
    {
        HashedPassword = EncodePassword(password);
    }
    
    // ✅ Pobierz plain-text hasło
    public string GetPassword()
    {
        return DecodePassword(HashedPassword);
    }

    public static DesktopSettings Load()
    {
        try
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }

            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<DesktopSettings>(json);
                return settings ?? new DesktopSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load settings: {ex.Message}");
        }

        var defaultSettings = new DesktopSettings();
        defaultSettings.Save();
        return defaultSettings;
    }

    public void Save()
    {
        try
        {
            Console.WriteLine($"💾 Saving settings to: {SettingsFilePath}");
            
            if (!Directory.Exists(AppDataFolder))
            {
                Console.WriteLine($"📁 Creating folder: {AppDataFolder}");
                Directory.CreateDirectory(AppDataFolder);
            }

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            
            var json = JsonSerializer.Serialize(this, options);
            Console.WriteLine($"📝 JSON: {json}");
            
            File.WriteAllText(SettingsFilePath, json);
            Console.WriteLine("✅ Settings saved successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to save settings: {ex.Message}");
            Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
        }
    }
}