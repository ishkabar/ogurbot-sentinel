using System;
using System.IO;
using System.Text.Json;

namespace Ogur.Sentinel.Devexpress.Config
{
    // === Sub-classes dla hierarchii ===
    
    public class GeneralSettings
    {
        public double OrientationThreshold { get; set; } = 1.0;
        public int HeaderHeightThreshold { get; set; } = 200;
        public int LabelHeightThreshold { get; set; } = 100;
        public int NextTimeHeightThreshold { get; set; } = 60;
        public int CompactModeThreshold { get; set; } = 150;
    }

    public class MarginsSettings
    {
        public int MainGridMarginSmall { get; set; } = 5;
        public int MainGridMarginLarge { get; set; } = 20;
        public double InnerMarginMin { get; set; } = 2;
        public double InnerMarginScale { get; set; } = 0.3;
        public double CountdownMarginScale { get; set; } = 0.2;
    }

    public class AnimationSettings
    {
        public int HeaderAnimationDurationMs { get; set; } = 300;
        public int OrientationAnimationDurationMs { get; set; } = 200;
        public int ElementVisibilityAnimationDurationMs { get; set; } = 250;
    }

    public class OverflowSettings
    {
        public double LabelHeightMultiplier { get; set; } = 1.5;
        public double CountdownHeightMultiplier { get; set; } = 1.2;
        public double NextTimeHeightMultiplier { get; set; } = 1.8;
        public double Threshold { get; set; } = 0.95;
    }

    public class StatusTextSettings
    {
        public double FontMin { get; set; } = 8;
        public double FontMax { get; set; } = 14;
        public double FontScale { get; set; } = 0.035;
        
        public double MarginTopMin { get; set; } = 5;
        public double MarginTopMax { get; set; } = 20;
        public double MarginTopScale { get; set; } = 0.05;
        
        public int HeightThreshold { get; set; } = 100;
    }

    public class FontSettings
    {
        public double CountdownMin { get; set; }
        public double CountdownMax { get; set; }
        public double CountdownScale { get; set; }
        
        public double NextTimeMin { get; set; }
        public double NextTimeMax { get; set; }
        public double NextTimeScale { get; set; }
        
        public double LabelMin { get; set; }
        public double LabelMax { get; set; }
        public double LabelScale { get; set; }
    }

    public class SpacingSettings
    {
        public double BorderMarginMin { get; set; }
        public double BorderMarginMax { get; set; }
        public double BorderMarginScale { get; set; }
        
        public double BorderPaddingMin { get; set; }
        public double BorderPaddingMax { get; set; }
        public double BorderPaddingScale { get; set; }
    }

    public class HorizontalLayoutSettings
    {
        public FontSettings Fonts { get; set; } = new FontSettings
        {
            CountdownMin = 16,
            CountdownMax = 72,
            CountdownScale = 0.20,
            NextTimeMin = 8,
            NextTimeMax = 16,
            NextTimeScale = 0.04,
            LabelMin = 10,
            LabelMax = 18,
            LabelScale = 0.045
        };

        public SpacingSettings Spacing { get; set; } = new SpacingSettings
        {
            BorderMarginMin = 2,
            BorderMarginMax = 8,
            BorderMarginScale = 0.015,
            BorderPaddingMin = 3,
            BorderPaddingMax = 15,
            BorderPaddingScale = 0.03
        };
    }

    public class VerticalLayoutSettings
    {
        public FontSettings Fonts { get; set; } = new FontSettings
        {
            CountdownMin = 16,
            CountdownMax = 60,
            CountdownScale = 0.25,
            NextTimeMin = 8,
            NextTimeMax = 14,
            NextTimeScale = 0.04,
            LabelMin = 9,
            LabelMax = 16,
            LabelScale = 0.045
        };

        public SpacingSettings Spacing { get; set; } = new SpacingSettings
        {
            BorderMarginMin = 2,
            BorderMarginMax = 5,
            BorderMarginScale = 0.012,
            BorderPaddingMin = 3,
            BorderPaddingMax = 12,
            BorderPaddingScale = 0.025
        };

        public class CompactModeSettings
        {
            public double CountdownMin { get; set; } = 20;
            public double CountdownMax { get; set; } = 80;
            public double CountdownScale { get; set; } = 0.40;
            
            public double BorderMarginMin { get; set; } = 1;
            public double BorderMarginMax { get; set; } = 3;
            public double BorderMarginScale { get; set; } = 0.008;
            
            public double BorderPaddingMin { get; set; } = 2;
            public double BorderPaddingMax { get; set; } = 8;
            public double BorderPaddingScale { get; set; } = 0.015;
        }

        public CompactModeSettings CompactMode { get; set; } = new CompactModeSettings();
    }

    // === Główna klasa konfiguracji ===
    
    public class ScalingConfig
    {
        private static string AppDataFolder => 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OgurSentinel");
        
        private static string ConfigFilePath =>  
            Path.Combine(AppDataFolder, "scaling-config.json");

        public GeneralSettings General { get; set; } = new GeneralSettings();
        public MarginsSettings Margins { get; set; } = new MarginsSettings();
        public AnimationSettings Animations { get; set; } = new AnimationSettings();
        public OverflowSettings Overflow { get; set; } = new OverflowSettings();
        public StatusTextSettings StatusText { get; set; } = new StatusTextSettings();
        public HorizontalLayoutSettings Horizontal { get; set; } = new HorizontalLayoutSettings();
        public VerticalLayoutSettings Vertical { get; set; } = new VerticalLayoutSettings();

        public static ScalingConfig Load()
        {
            if (!App.DebugMode)
            {
                // Tryb produkcyjny - ładuj bez logowania
                try
                {
                    if (File.Exists(ConfigFilePath))
                    {
                        var json = File.ReadAllText(ConfigFilePath);
                        var config = JsonSerializer.Deserialize<ScalingConfig>(json);
                        if (config != null) return config;
                    }
                }
                catch
                {
                    // Cicho zwróć domyślny config
                }

                return new ScalingConfig();
            }

            // Tryb debug - pełne logowanie
            try
            {
                Console.WriteLine($"📂 [ScalingConfig] AppDataFolder: {AppDataFolder}");
                Console.WriteLine($"📄 [ScalingConfig] ConfigFilePath: {ConfigFilePath}");

                if (!Directory.Exists(AppDataFolder))
                {
                    Console.WriteLine($"📁 [ScalingConfig] Folder doesn't exist, creating...");
                    Directory.CreateDirectory(AppDataFolder);
                }

                if (File.Exists(ConfigFilePath))
                {
                    Console.WriteLine($"✅ [ScalingConfig] File exists, reading...");
                    var json = File.ReadAllText(ConfigFilePath);
                    
                    var config = JsonSerializer.Deserialize<ScalingConfig>(json);
                    
                    if (config != null)
                    {
                        Console.WriteLine($"✅ [ScalingConfig] Loaded successfully");
                        return config;
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ [ScalingConfig] Deserialization returned null");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ [ScalingConfig] File doesn't exist at: {ConfigFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ScalingConfig] Load failed: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }

            Console.WriteLine($"🆕 [ScalingConfig] Creating default config");
            var defaultConfig = new ScalingConfig();
            defaultConfig.Save();
            return defaultConfig;
        }

        public void Save()
        {
            try
            {
                if (App.DebugMode)
                {
                    Console.WriteLine($"💾 [ScalingConfig] Saving to: {ConfigFilePath}");
                }
                
                if (!Directory.Exists(AppDataFolder))
                {
                    if (App.DebugMode)
                    {
                        Console.WriteLine($"📁 [ScalingConfig] Creating folder: {AppDataFolder}");
                    }
                    Directory.CreateDirectory(AppDataFolder);
                }

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };
                
                var json = JsonSerializer.Serialize(this, options);
                
                File.WriteAllText(ConfigFilePath, json);
                
                if (App.DebugMode)
                {
                    Console.WriteLine("✅ [ScalingConfig] Saved successfully!");
                }
            }
            catch (Exception ex)
            {
                if (App.DebugMode)
                {
                    Console.WriteLine($"❌ [ScalingConfig] Failed to save: {ex.Message}");
                    Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                }
            }
        }
    }
}