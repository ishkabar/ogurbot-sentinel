using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ogur.Sentinel.Worker.Discord.Modules;

namespace Ogur.Sentinel.Worker.Discord;


public sealed class CommandRegistrationService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<CommandRegistrationService> _logger;
    private readonly RespawnModule _respawn;
    private readonly LeaveModule _leave;
    private readonly AdminBreakModule _admin;

    public CommandRegistrationService(
        DiscordSocketClient client,
        RespawnModule respawn,
        LeaveModule leave,
        AdminBreakModule admin,
        ILogger<CommandRegistrationService> logger)
    {
        _client = client;
        _logger = logger;
        _respawn = respawn;
        _leave = leave;
        _admin = admin;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.SlashCommandExecuted += OnSlash;

        await _client.WaitForReadyAsync(stoppingToken);

        var commands = new List<SlashCommandBuilder>
        {
            _respawn.Build(),
            _leave.Build(),
            _admin.Build()
        };

        foreach (var g in _client.Guilds)
        {
            try
            {
                await g.BulkOverwriteApplicationCommandAsync(commands.Select(c => c.Build()).ToArray());
                _logger.LogInformation("Slash commands registered for guild {Guild}", g.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register slash commands for guild {Guild}", g.Name);
            }
        }
    }

    private Task OnSlash(SocketSlashCommand cmd) =>
        cmd.Data.Name switch
        {
            "respawn" => _respawn.Handle(cmd),
            "leave"   => _leave.Handle(cmd),
            "admin"   => _admin.Handle(cmd),
            _ => cmd.RespondAsync("Unknown command.", ephemeral: true)
        };
}

file static class DiscordClientExtensions
{
    public static async Task WaitForReadyAsync(this DiscordSocketClient client, CancellationToken ct)
    {
        if (client.LoginState == LoginState.LoggedIn && client.ConnectionState == ConnectionState.Connected && client.CurrentUser != null)
            return;

        var tcs = new TaskCompletionSource();
        Task Handler()
        {
            tcs.TrySetResult();
            return Task.CompletedTask;
        }
        client.Ready += Handler;
        try { await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30), ct)); }
        finally { client.Ready -= Handler; }
    }
}