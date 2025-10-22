using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; 
using Ogur.Sentinel.Abstractions.Options;
using Discord;
using Discord.WebSocket;
using Ogur.Sentinel.Abstractions.Respawn;
using Ogur.Sentinel.Core.Respawn;
using Ogur.Sentinel.Worker.Services;
using System.Threading;       
using System.Threading.Tasks; 

namespace Ogur.Sentinel.Worker.Discord.Modules;

public sealed class RespawnModule
{
    private readonly RespawnState _state;
    private readonly SettingsStore _store;
    private readonly RespawnSchedulerService _scheduler;
    private readonly VoiceService2 _voice;
    private readonly RespawnOptions _respawn; 
    private readonly ILogger<RespawnModule> _logger;

    public RespawnModule(RespawnState state, SettingsStore store, RespawnSchedulerService scheduler, VoiceService2 voice, IOptions<RespawnOptions> opts,
        ILogger<RespawnModule> logger)
    {
        _state = state;
        _store = store;
        _scheduler = scheduler;
        _voice = voice;    
        _respawn = opts.Value;
        _logger = logger;
    }

    public SlashCommandBuilder Build()
    {
        var b = new SlashCommandBuilder()
            .WithName("respawn")
            .WithDescription("Respawn management");

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("on")
            .WithDescription("Enable respawn reminders")
            .WithType(ApplicationCommandOptionType.SubCommand));

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("off")
            .WithDescription("Disable respawn reminders")
            .WithType(ApplicationCommandOptionType.SubCommand));

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("set-base")
            .WithDescription("Set base time (format HH:MM:SS)")
            .AddOption("time", ApplicationCommandOptionType.String, "Base time e.g. 01:10:30", isRequired: true)  // <-- przykład
            .WithType(ApplicationCommandOptionType.SubCommand));

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("set-lead")
            .WithDescription("Set lead seconds")
            .AddOption("seconds", ApplicationCommandOptionType.Integer, "Lead seconds", isRequired: true)
            .WithType(ApplicationCommandOptionType.SubCommand));

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("add-channel")
            .WithDescription("Add voice channel id for join")
            .AddOption("channel_id", ApplicationCommandOptionType.String, "Channel Id (ulong)", isRequired: true)
            .WithType(ApplicationCommandOptionType.SubCommand));

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("remove-channel")
            .WithDescription("Remove voice channel id")
            .AddOption("channel_id", ApplicationCommandOptionType.String, "Channel Id (ulong)", isRequired: true)
            .WithType(ApplicationCommandOptionType.SubCommand));

        b.AddOption(new SlashCommandOptionBuilder()
            .WithName("test-voice")
            .WithDescription("Join channel and play test sound")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption("sound", ApplicationCommandOptionType.String, "Which sound to play (10m or 2h)", isRequired: true,
                choices: new []
                {
                    new ApplicationCommandOptionChoiceProperties { Name = "10m respawn", Value = "10m" },
                    new ApplicationCommandOptionChoiceProperties { Name = "2h respawn", Value = "2h" }
                })
            .AddOption("channel", ApplicationCommandOptionType.String, "Voice channel ID (leave empty for first configured)", isRequired: false));

        return b;
    }

    public async Task Handle(SocketSlashCommand cmd)
    {
        var sub = cmd.Data.Options.FirstOrDefault()?.Name;
        switch (sub)
        {
            case "on":
                _state.Enabled10m = true;
                _state.Enabled2h = true;
                await cmd.RespondAsync("Respawn enabled (10m & 2h).", ephemeral: true);
                break;

            case "off":
                _state.Enabled10m = false;
                _state.Enabled2h = false;
                await cmd.RespondAsync("Respawn disabled.", ephemeral: true);
                break;

            case "set-base":
                var t = (string)cmd.Data.Options.First().Options.First().Value!;
                SchedulingMath.ParseHhmm(t); // validate
                _state.BaseHhmm = t;
                await _store.SaveAsync(_state.ToPersisted());
                await cmd.RespondAsync($"Base set to `{t}`.", ephemeral: true);
                break;

            case "set-lead":
                var s = Convert.ToInt32(cmd.Data.Options.First().Options.First().Value!);
                _state.LeadSeconds = Math.Max(0, s);
                await _store.SaveAsync(_state.ToPersisted());
                await cmd.RespondAsync($"Lead set to `{_state.LeadSeconds}s`.", ephemeral: true);
                break;

            case "add-channel":
            {
                var str = (string)cmd.Data.Options.First().Options.First().Value!;
                if (ulong.TryParse(str, out var id))
                {
                    var newList = _state.Channels.ToList();
                    if (!newList.Contains(id)) newList.Add(id);
                    _state.SetChannels(newList);
                    await _store.SaveAsync(_state.ToPersisted());
                    await cmd.RespondAsync($"Added channel `{id}`.", ephemeral: true);
                }
                else await cmd.RespondAsync("Invalid channel id.", ephemeral: true);

                break;
            }

            case "remove-channel":
            {
                var str = (string)cmd.Data.Options.First().Options.First().Value!;
                if (ulong.TryParse(str, out var id))
                {
                    if (_state.RemoveChannel(id))
                    {
                        await _store.SaveAsync(_state.ToPersisted());
                        await cmd.RespondAsync($"Removed `{id}`.", ephemeral: true);
                    }
                    else await cmd.RespondAsync("Not found.", ephemeral: true);
                }
                else await cmd.RespondAsync("Invalid channel id.", ephemeral: true);

                break;
            }

            case "test-voice":
            {
                var subCommand = cmd.Data.Options.First();
                var soundType = subCommand.Options.FirstOrDefault(o => o.Name == "sound")?.Value?.ToString() ?? "10m";
                var channelIdStr = subCommand.Options.FirstOrDefault(o => o.Name == "channel")?.Value?.ToString();
    
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
                        await cmd.RespondAsync("Brak skonfigurowanych kanałów. Użyj `respawn add-channel` lub podaj ID kanału.", ephemeral: true);
                        return;
                    }
                }
                
                var path = soundType == "2h" ? _respawn.Sound2h : _respawn.Sound10m;
                path ??= soundType == "2h" ? "/assets/respawn_2h.wav" : "/assets/respawn_10m.wav";
    
                if (!File.Exists(path))
                {
                    await cmd.RespondAsync($"Plik audio nie istnieje: `{path}`", ephemeral: true);
                    return;
                }
                
                var repeatPlays = _state.RepeatPlays > 0 ? _state.RepeatPlays : 1;
                var repeatGapMs = _state.RepeatGapMs;  // ← Bez fallbacku

                await cmd.RespondAsync($"OK, próbuję wejść na kanał `{channelId}` i zagrać ({soundType}, {repeatPlays}x, gap={repeatGapMs}ms).", ephemeral: true);
                _ = _voice.JoinAndPlayAsync(channelId, path, repeatPlays, repeatGapMs, CancellationToken.None);
                break;
            }

            

            default:
                await cmd.RespondAsync("Unknown subcommand.", ephemeral: true);
                break;
        }
    }
}