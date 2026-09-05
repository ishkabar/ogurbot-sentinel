using System.Text.Json;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ogur.Sentinel.Worker.Services;

public sealed class OreSectorResolver
{
    private const double MapWidth = 171;
    private const double MapHeight = 214;
    private const int SectorImageWidth = 1024;
    private const int SectorImageHeight = 1280;
    private const string UnknownSectorName = "Nieznany sektor";

    private readonly string _sectorsImagePath;
    private readonly string _sectorNamesPath;
    private readonly ILogger<OreSectorResolver> _logger;

    private Image<Rgb24>? _sectorsImage;
    private Dictionary<string, string>? _colorToName;
    private readonly object _lock = new();

    public OreSectorResolver(ILogger<OreSectorResolver> logger)
    {
        _logger = logger;
        var assetsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings", "assets");
        _sectorsImagePath = System.IO.Path.Combine(assetsDir, "Joan_sectors_raw.png");
        _sectorNamesPath = System.IO.Path.Combine(assetsDir, "Joan_sectors_names.json");
    }

    public string ResolveSector(double x, double y)
{
    EnsureImageLoaded();
    var colorToName = LoadSectorNames();

    if (_sectorsImage is null)
        return UnknownSectorName;

    var scaleX = SectorImageWidth / MapWidth;
    var scaleY = SectorImageHeight / MapHeight;

    var px = (int)Math.Round(x * scaleX);
    var py = (int)Math.Round(y * scaleY);

    px = Math.Clamp(px, 0, SectorImageWidth - 1);
    py = Math.Clamp(py, 0, SectorImageHeight - 1);

    Rgb24 pixel;
    lock (_lock)
    {
        pixel = _sectorsImage[px, py];
    }

    if (pixel.R == 0 && pixel.G == 0 && pixel.B == 0)
        return UnknownSectorName;

    var hex = $"#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2}";

    return colorToName.TryGetValue(hex, out var name) ? name : UnknownSectorName;
}

private void EnsureImageLoaded()
{
    if (_sectorsImage is not null) return;

    lock (_lock)
    {
        if (_sectorsImage is not null) return;

        try
        {
            if (!File.Exists(_sectorsImagePath))
            {
                _logger.LogWarning("[ORE-SECTOR] sectors image not found at {Path}", _sectorsImagePath);
                return;
            }

            _sectorsImage = Image.Load<Rgb24>(_sectorsImagePath);
            _logger.LogInformation("[ORE-SECTOR] Loaded sectors image {Size}", _sectorsImage.Size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ORE-SECTOR] Failed to load sectors image");
        }
    }
}

private Dictionary<string, string> LoadSectorNames()
{
    try
    {
        if (!File.Exists(_sectorNamesPath))
        {
            _logger.LogWarning("[ORE-SECTOR] sector names file not found at {Path}", _sectorNamesPath);
            return new Dictionary<string, string>();
        }

        var json = File.ReadAllText(_sectorNamesPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
               ?? new Dictionary<string, string>();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "[ORE-SECTOR] Failed to load sector names, using empty map");
        return new Dictionary<string, string>();
    }
}

    private void EnsureLoaded()
    {
        if (_sectorsImage is not null && _colorToName is not null) return;

        lock (_lock)
        {
            if (_sectorsImage is not null && _colorToName is not null) return;

            try
            {
                if (!File.Exists(_sectorsImagePath))
                {
                    _logger.LogWarning("[ORE-SECTOR] sectors.png not found at {Path}", _sectorsImagePath);
                    return;
                }

                _sectorsImage = Image.Load<Rgb24>(_sectorsImagePath);
                _logger.LogInformation("[ORE-SECTOR] Loaded sectors image {Size}", _sectorsImage.Size);

                if (!File.Exists(_sectorNamesPath))
                {
                    _logger.LogWarning("[ORE-SECTOR] sector_names.json not found at {Path}", _sectorNamesPath);
                    _colorToName = new Dictionary<string, string>();
                    return;
                }

                var json = File.ReadAllText(_sectorNamesPath);
                _colorToName = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                                ?? new Dictionary<string, string>();

                _logger.LogInformation("[ORE-SECTOR] Loaded {Count} sector names", _colorToName.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ORE-SECTOR] Failed to load sector data");
            }
        }
    }
}