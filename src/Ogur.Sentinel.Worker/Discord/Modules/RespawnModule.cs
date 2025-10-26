using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; 
using Ogur.Sentinel.Abstractions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Ogur.Sentinel.Abstractions.Respawn;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Worker.Services;
using System.Threading;       
using System.Threading.Tasks; 

namespace Ogur.Sentinel.Worker.Discord.Modules;

[SlashCommand("respawn", "Respawn management")]
public sealed class RespawnModule : ApplicationCommandModule<SlashCommandContext>
{
    private readonly RespawnState _state;
    private readonly SettingsStore _store;
    private readonly RespawnSchedulerService _scheduler;
    private readonly VoiceService3 _voice;
    private readonly RespawnOptions _respawn; 
    private readonly ILogger<RespawnModule> _logger;

    public RespawnModule(
        RespawnState state, 
        SettingsStore store, 
        RespawnSchedulerService scheduler, 
        VoiceService3 voice, 
        IOptions<RespawnOptions> opts,
        ILogger<RespawnModule> logger)
    {
        _state = state;
        _store = store;
        _scheduler = scheduler;
        _voice = voice;    
        _respawn = opts.Value;
        _logger = logger;
    }

    [SubSlashCommand("on", "Enable respawn reminders")]
    public async Task OnAsync(SlashCommandContext context)
    {
        _state.Enabled10m = true;
        _state.Enabled2h = true;
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = "Respawn enabled (10m & 2h).",
            Flags = MessageFlags.Ephemeral
        }));
    }

    [SubSlashCommand("off", "Disable respawn reminders")]
    public async Task OffAsync(SlashCommandContext context)
    {
        _state.Enabled10m = false;
        _state.Enabled2h = false;
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = "Respawn disabled.",
            Flags = MessageFlags.Ephemeral
        }));
    }

    [SubSlashCommand("set-base", "Set base time (format HH:MM:SS)")]
    public async Task SetBaseAsync(
        SlashCommandContext context,
        [SlashCommandParameter(Name = "time", Description = "Base time e.g. 01:10:30")] string time)
    {
        SchedulingMath.ParseHhmm(time); // validate
        _state.BaseHhmm = time;
        await _store.SaveAsync(_state.ToPersisted());
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = $"Base set to `{time}`.",
            Flags = MessageFlags.Ephemeral
        }));
    }

    [SubSlashCommand("set-lead", "Set lead seconds")]
    public async Task SetLeadAsync(
        SlashCommandContext context,
        [SlashCommandParameter(Name = "seconds", Description = "Lead seconds")] int seconds)
    {
        _state.LeadSeconds = Math.Max(0, seconds);
        await _store.SaveAsync(_state.ToPersisted());
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = $"Lead set to `{_state.LeadSeconds}s`.",
            Flags = MessageFlags.Ephemeral
        }));
    }

    [SubSlashCommand("add-channel", "Add voice channel id for join")]
    public async Task AddChannelAsync(
        SlashCommandContext context,
        [SlashCommandParameter(Name = "channel_id", Description = "Channel Id (ulong)")] string channelIdStr)
    {
        if (ulong.TryParse(channelIdStr, out var id))
        {
            var newList = _state.Channels.ToList();
            if (!newList.Contains(id)) newList.Add(id);
            _state.SetChannels(newList);
            await _store.SaveAsync(_state.ToPersisted());
            await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = $"Added channel `{id}`.",
                Flags = MessageFlags.Ephemeral
            }));
        }
        else
        {
            await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = "Invalid channel id.",
                Flags = MessageFlags.Ephemeral
            }));
        }
    }

    [SubSlashCommand("remove-channel", "Remove voice channel id")]
    public async Task RemoveChannelAsync(
        SlashCommandContext context,
        [SlashCommandParameter(Name = "channel_id", Description = "Channel Id (ulong)")] string channelIdStr)
    {
        if (ulong.TryParse(channelIdStr, out var id))
        {
            if (_state.RemoveChannel(id))
            {
                await _store.SaveAsync(_state.ToPersisted());
                await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Content = $"Removed `{id}`.",
                    Flags = MessageFlags.Ephemeral
                }));
            }
            else
            {
                await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Content = "Not found.",
                    Flags = MessageFlags.Ephemeral
                }));
            }
        }
        else
        {
            await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = "Invalid channel id.",
                Flags = MessageFlags.Ephemeral
            }));
        }
    }

    [SubSlashCommand("test-voice", "Join channel and play test sound")]
public async Task TestVoiceAsync(
    SlashCommandContext context,
    [SlashCommandParameter(Name = "sound", Description = "Which sound to play (10m or 2h)")]
    string sound,
    [SlashCommandParameter(Name = "channel", Description = "Voice channel ID (leave empty for first configured)")] 
    string? channelIdStr = null)
{
    ulong channelId;
    
    if (!string.IsNullOrEmpty(channelIdStr) && ulong.TryParse(channelIdStr, out var parsed))
    {
        channelId = parsed;
    }
    else
    {
        channelId = _state.Channels.FirstOrDefault();
        if (channelId == 0)
        {
            await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Content = "Brak skonfigurowanych kanałów. Użyj `respawn add-channel` lub podaj ID kanału.",
                Flags = MessageFlags.Ephemeral
            }));
            return;
        }
    }
    
    var path = sound == "2h" ? _respawn.Sound2h : _respawn.Sound10m;
    path ??= sound == "2h" ? "/assets/respawn_2h.wav" : "/assets/respawn_10m.wav";

    if (!File.Exists(path))
    {
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = $"Plik audio nie istnieje: `{path}`",
            Flags = MessageFlags.Ephemeral
        }));
        return;
    }
    
    // ✅ Użyj odpowiednich settings dla danego dźwięku
    var repeatPlays = sound == "2h" ? _state.RepeatPlays2h : _state.RepeatPlays10m;
    var repeatGapMs = sound == "2h" ? _state.RepeatGapMs2h : _state.RepeatGapMs10m;

    await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
    {
        Content = $"OK, próbuję wejść na kanał `{channelId}` i zagrać ({sound}, {repeatPlays}x, gap={repeatGapMs}ms).",
        Flags = MessageFlags.Ephemeral
    }));
    
    _ = _voice.JoinAndPlayAsync(channelId, path, repeatPlays, repeatGapMs, CancellationToken.None);
}
}