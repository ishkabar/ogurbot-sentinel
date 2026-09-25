using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ogur.Sentinel.Api.Services;

public record UpgradeGroup(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("items")] List<string> Items,
    [property: JsonPropertyName("chances")] List<decimal> Chances,
    [property: JsonPropertyName("name_en")] string? NameEn = null,
    [property: JsonPropertyName("items_en")] List<string>? ItemsEn = null,
    [property: JsonPropertyName("type")] string? Type = null,
    [property: JsonPropertyName("level")] int? Level = null
);

public record UpgradeData(
    [property: JsonPropertyName("levels")] List<int> Levels,
    [property: JsonPropertyName("groups")] List<UpgradeGroup> Groups
);

public class UpgradeChanceService
{
    private readonly string _filePath;
    private readonly ILogger<UpgradeChanceService> _logger;
    private readonly object _lock = new();

    private UpgradeData? _cached;
    private DateTime _cachedAt;

    public UpgradeChanceService(IWebHostEnvironment env, ILogger<UpgradeChanceService> logger)
    {
        _filePath = env.IsDevelopment()
            ? Path.Combine(env.ContentRootPath, "appsettings", "baerim-upgrade.json")
            : "/app/appsettings/baerim-upgrade.json";
        _logger = logger;
    }

    public UpgradeData GetData()
    {
        lock (_lock)
        {
            var lastWrite = File.Exists(_filePath) ? File.GetLastWriteTimeUtc(_filePath) : DateTime.MinValue;
            if (_cached is not null && lastWrite <= _cachedAt)
            {
                return _cached;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<UpgradeData>(json) ?? new UpgradeData(new List<int>(), new List<UpgradeGroup>());
                _cached = data;
                _cachedAt = lastWrite;
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load upgrade chance data from {Path}", _filePath);
                return _cached ?? new UpgradeData(new List<int>(), new List<UpgradeGroup>());
            }
        }
    }
}