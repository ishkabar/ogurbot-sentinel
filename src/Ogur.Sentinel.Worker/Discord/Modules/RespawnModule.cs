using System;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Sentinel.Abstractions.Options;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Worker.Services;

namespace Ogur.Sentinel.Worker.Discord.Modules;

[Command("respawn")]
[Description("Respawn management")]
public sealed class RespawnModule
{
    private readonly RespawnState _state;
    private readonly SettingsStore _store;
    private readonly VoiceService _voice;
    private readonly RespawnOptions _respawn;
    private readonly ILogger<RespawnModule> _logger;

    public RespawnModule(
        RespawnState state,
        SettingsStore store,
        VoiceService voice,
        IOptions<RespawnOptions> opts,
        ILogger<RespawnModule> logger)
    {
        _state = state;
        _store = store;
        _voice = voice;
        _respawn = opts.Value;
        _logger = logger;
    }

    [Command("on")]
    [Description("Enable respawn reminders")]
    public async Task OnAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();
        
        _state.Enabled10m = true;
        _state.Enabled2h = true;
        await _store.SaveAsync(_state.ToPersisted());
        
        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent("Respawn enabled (10m & 2h)."));
    }

    [Command("off")]
    [Description("Disable respawn reminders")]
    public async Task OffAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();
        
        _state.Enabled10m = false;
        _state.Enabled2h = false;
        await _store.SaveAsync(_state.ToPersisted());
        
        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent("Respawn disabled."));
    }

    [Command("set-base")]
    [Description("Set base HH:MM[:SS]")]
    public async Task SetBaseAsync(CommandContext ctx, 
        [Description("Base time HH:MM[:SS]")] string time)
    {
        await ctx.DeferResponseAsync();

        try 
        { 
            SchedulingMath.ParseHhmm(time); 
        }
        catch (Exception ex)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Invalid time: `{time}` ({ex.Message})"));
            return;
        }

        _state.BaseHhmm = time;
        await _store.SaveAsync(_state.ToPersisted());
        
        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"Base set to `{time}`."));
    }

    [Command("set-lead")]
    [Description("Set lead seconds")]
    public async Task SetLeadAsync(CommandContext ctx,
        [Description("Lead seconds")] long seconds)
    {
        await ctx.DeferResponseAsync();
        
        _state.LeadSeconds = Math.Max(0, (int)seconds);
        await _store.SaveAsync(_state.ToPersisted());
        
        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"Lead set to `{_state.LeadSeconds}s`."));
    }

    [Command("add-channel")]
    [Description("Add voice channel")]
    public async Task AddChannelAsync(CommandContext ctx,
        [Description("Voice channel Id")] string channelId)
    {
        await ctx.DeferResponseAsync();
        
        if (!ulong.TryParse(channelId, out var id))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Invalid channel id."));
            return;
        }

        var list = _state.Channels.ToList();
        if (!list.Contains(id)) list.Add(id);
        _state.SetChannels(list);
        await _store.SaveAsync(_state.ToPersisted());
        
        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"Added channel `{id}`."));
    }

    [Command("remove-channel")]
    [Description("Remove voice channel")]
    public async Task RemoveChannelAsync(CommandContext ctx,
        [Description("Voice channel Id")] string channelId)
    {
        await ctx.DeferResponseAsync();
        
        if (!ulong.TryParse(channelId, out var id))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Invalid channel id."));
            return;
        }

        if (_state.RemoveChannel(id))
        {
            await _store.SaveAsync(_state.ToPersisted());
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Removed `{id}`."));
        }
        else
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Not found."));
        }
    }

    [Command("test-voice")]
    [Description("Test voice playback")]
    public async Task TestVoiceAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        var first = _state.Channels.FirstOrDefault();
        if (first == 0)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("No channels configured. Use `/respawn add-channel`."));
            return;
        }

        var path = _respawn.Sound10m ?? "assets/respawn_10m.wav";
        if (!File.Exists(path))
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Audio file not found: `{path}`"));
            return;
        }

        await ctx.EditResponseAsync(new DiscordWebhookBuilder()
            .WithContent($"Playing test sound in <#{first}>..."));

        await _voice.PlayOnceAsync(first, path, CancellationToken.None);

        await ctx.FollowupAsync(new DiscordFollowupMessageBuilder()
            .WithContent("Test playback finished."));
    }
}