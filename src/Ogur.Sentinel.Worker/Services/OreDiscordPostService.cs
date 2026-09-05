using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Ogur.Sentinel.Core.Ore;
using Microsoft.Extensions.Logging;

namespace Ogur.Sentinel.Worker.Services;

public sealed class OreDiscordPostService
{
    private const ulong ChannelId = 1545629318322061322;
    private const string ChunjoUrl = "https://respy.ogur.dev/baerim/ore/chunjo";

    private readonly GatewayClient _client;
    private readonly OreState _state;
    private readonly OreStore _store;
    private readonly OreMapImageService _mapImage;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly ILogger<OreDiscordPostService> _logger;

    public OreDiscordPostService(
        GatewayClient client,
        OreState state,
        OreStore store,
        OreMapImageService mapImage,
        ILogger<OreDiscordPostService> logger)
    {
        _client = client;
        _state = state;
        _store = store;
        _mapImage = mapImage;
        _logger = logger;
    }

    public async Task EnsureStaticPostAsync(CancellationToken ct = default)
    {
        if (_state.StaticMessageId != 0)
        {
            var exists = await TryGetMessageAsync(_state.StaticMessageId, ct);
            if (exists)
            {
                _logger.LogInformation("[ORE-POST] Static post already exists (id={Id})", _state.StaticMessageId);
                return;
            }
            _logger.LogWarning("[ORE-POST] Stored static message id={Id} no longer exists, recreating", _state.StaticMessageId);
        }

        var button = new LinkButtonProperties(ChunjoUrl, "Oznacz rudę");

        var message = new MessageProperties
        {
            Content = "**Ruda Chunjo (Baerim)**\nKliknij przycisk, aby otworzyć mapę i oznaczyć lokalizację rudy.",
            Components = [new ActionRowProperties([button])]
        };

        var sent = await _client.Rest.SendMessageAsync(ChannelId, message, cancellationToken: ct);
        _state.SetStaticMessageId(sent.Id);
        await _store.SaveAsync(_state.ToPersisted());

        _logger.LogInformation("[ORE-POST] Static post created (id={Id})", sent.Id);
    }

    public async Task PublishMarkAsync(double x, double y, string username, CancellationToken ct = default)
    {
        await _publishLock.WaitAsync(ct);
        try
        {
            await DeleteDynamicPostInternalAsync(ct);

            var imageBytes = await _mapImage.RenderMarkedMapAsync(x, y, ct);
            if (imageBytes is null)
            {
                _logger.LogWarning("[ORE-POST] Could not render map image, skipping dynamic post");
                return;
            }

            using var stream = new MemoryStream(imageBytes);

            var message = new MessageProperties
            {
                Content = $"📍 Ruda oznaczona przez **{username}**\nSektor: _—_",
                Attachments = [new AttachmentProperties("chunjo_mark.png", stream)]
            };

            var sent = await _client.Rest.SendMessageAsync(ChannelId, message, cancellationToken: ct);
            _state.SetDynamicMessageId(sent.Id);
            await _store.SaveAsync(_state.ToPersisted());

            _logger.LogInformation("[ORE-POST] Dynamic post created (id={Id})", sent.Id);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public async Task DeleteDynamicPostAsync(CancellationToken ct = default)
    {
        await _publishLock.WaitAsync(ct);
        try
        {
            await DeleteDynamicPostInternalAsync(ct);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task DeleteDynamicPostInternalAsync(CancellationToken ct)
    {
        if (_state.DynamicMessageId == 0) return;

        try
        {
            await _client.Rest.DeleteMessageAsync(ChannelId, _state.DynamicMessageId, cancellationToken: ct);
            _logger.LogInformation("[ORE-POST] Dynamic post deleted (id={Id})", _state.DynamicMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ORE-POST] Failed to delete dynamic post (id={Id}), may already be gone", _state.DynamicMessageId);
        }

        _state.SetDynamicMessageId(0);
        await _store.SaveAsync(_state.ToPersisted());
    }

    private async Task<bool> TryGetMessageAsync(ulong messageId, CancellationToken ct)
    {
        try
        {
            await _client.Rest.GetMessageAsync(ChannelId, messageId, cancellationToken: ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}