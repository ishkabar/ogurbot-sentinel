using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ogur.Sentinel.Worker.Services;

public sealed class OreMapImageService
{
    private const string MapFileName = "Joan.png";
    private const double OriginalWidth = 171;
    private const double OriginalHeight = 214;
    private const int OutputScale = 4; // 171x214 -> 684x856, czytelniejsze na Discordzie

    private readonly string _mapPath;
    private readonly ILogger<OreMapImageService> _logger;

    public OreMapImageService(ILogger<OreMapImageService> logger)
    {
        _logger = logger;
        _mapPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings", "assets", MapFileName);
    }

    public async Task<byte[]?> RenderMarkedMapAsync(double markX, double markY, CancellationToken ct = default)
    {
        if (!File.Exists(_mapPath))
        {
            _logger.LogWarning("[ORE-MAP-IMG] Map file not found at {Path}", _mapPath);
            return null;
        }

        try
        {
            using var image = await Image.LoadAsync<Rgba32>(_mapPath, ct);

            image.Mutate<Rgba32>(ctx => ctx.Resize(
                (int)(OriginalWidth * OutputScale),
                (int)(OriginalHeight * OutputScale),
                KnownResamplers.NearestNeighbor));

            var scaledX = (float)(markX * OutputScale);
            var scaledY = (float)(markY * OutputScale);
            var radius = 8f;

            image.Mutate<Rgba32>(ctx =>
            {
                ctx.Fill(Color.White, new EllipsePolygon(scaledX, scaledY, radius + 2));
                ctx.Fill(Color.Red, new EllipsePolygon(scaledX, scaledY, radius));
            });

            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, ct);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ORE-MAP-IMG] Failed to render marked map");
            return null;
        }
    }
}