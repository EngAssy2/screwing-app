using System.Text.Json;
using ScrewingHub.Core.Models;
using Serilog;

namespace ScrewingHub.Core.Services;

/// <summary>
/// Loads and saves application settings including model configurations.
/// </summary>
public class ModelConfigService
{
    private static readonly ILogger Logger = Log.ForContext<ModelConfigService>();
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Settings { get; private set; } = new();

    public ModelConfigService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    /// <summary>Load settings from JSON file. Creates default if not found.</summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                Logger.Information("Settings loaded from {Path}", _settingsFilePath);
            }
            else
            {
                Logger.Information("Settings file not found, creating defaults at {Path}", _settingsFilePath);
                Settings = CreateDefaultSettings();
                Save();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load settings, using defaults");
            Settings = CreateDefaultSettings();
        }

        return Settings;
    }

    /// <summary>Save current settings to JSON file.</summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
            Logger.Information("Settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save settings");
            throw;
        }
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            Models = new List<ProductModel>
            {
                new()
                {
                    ModelId = 1,
                    ModelName = "Model A",
                    TotalScrewCount = 4,
                    IsEnabled = true,
                    Channels = new List<ChannelConfig>
                    {
                        new()
                        {
                            ChannelNumber = 1,
                            ScrewName = "Screw A-1",
                            CurrentValueUpperLimit = 2500,
                            CurrentValueLowerLimit = 1500,
                            TimeUpperLimitMs = 1500,
                            TimeLowerLimitMs = 300,
                            TorqueConversionFactor = 0.001098,
                            IsEnabled = true
                        }
                    }
                },
                new()
                {
                    ModelId = 2,
                    ModelName = "Model B",
                    TotalScrewCount = 6,
                    IsEnabled = true,
                    Channels = new List<ChannelConfig>
                    {
                        new()
                        {
                            ChannelNumber = 1,
                            ScrewName = "Screw B-1",
                            CurrentValueUpperLimit = 3200,
                            CurrentValueLowerLimit = 2000,
                            TimeUpperLimitMs = 2000,
                            TimeLowerLimitMs = 500,
                            TorqueConversionFactor = 0.002475,
                            IsEnabled = true
                        }
                    }
                }
            }
        };
    }
}
