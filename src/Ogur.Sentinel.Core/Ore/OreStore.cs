using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ogur.Sentinel.Core.Ore;

public sealed class OreStore
{
    private readonly string _path;
    private readonly ILogger<OreStore> _logger;

    public OreStore(ILogger<OreStore> logger)
    {
        _logger = logger;
        _path = Path.Combine(AppContext.BaseDirectory, "appsettings", "ore.state.json");
    }

    public async Task<OrePersisted> LoadAsync()
    {
        try
        {
            if (!File.Exists(_path))
                return new OrePersisted();

            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<OrePersisted>(json) ?? new OrePersisted();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ORE-STORE] Failed to load {Path}, using empty state", _path);
            return new OrePersisted();
        }
    }

    public async Task SaveAsync(OrePersisted persisted)
    {
        try
        {
            var json = JsonSerializer.Serialize(persisted, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_path, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ORE-STORE] Failed to save {Path}", _path);
        }
    }
}