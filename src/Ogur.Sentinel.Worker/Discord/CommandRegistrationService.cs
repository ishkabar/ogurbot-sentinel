using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Ogur.Sentinel.Worker.Discord.Modules;

namespace Ogur.Sentinel.Worker.Discord;

public sealed class CommandRegistrationService : BackgroundService
{
    private readonly GatewayClient _client;
    private readonly ILogger<CommandRegistrationService> _logger;
    private readonly ApplicationCommandService<SlashCommandContext> _commandService;

    public CommandRegistrationService(
        GatewayClient client,
        ApplicationCommandService<SlashCommandContext> commandService,
        ILogger<CommandRegistrationService> logger)
    {
        _client = client;
        _commandService = commandService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.InteractionCreate += OnInteraction;

        await WaitForReadyAsync(stoppingToken);

        _logger.LogInformation("Command registration service started. Commands will be handled via ApplicationCommandService.");
    }

    private ValueTask OnInteraction(Interaction interaction)
    {
        if (interaction is SlashCommandInteraction slashCommand)
        {
            var context = new SlashCommandContext(slashCommand, _client);
            _ = Task.Run(async () =>
            {
                try
                {
                    await _commandService.ExecuteAsync(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing slash command {CommandName}", slashCommand.Data.Name);
                }
            });
        }

        return ValueTask.CompletedTask;
    }

    private async Task WaitForReadyAsync(CancellationToken ct)
    {
        // Sprawdź czy client już jest gotowy - najprostsze rozwiązanie
        if (_client.Cache?.Guilds != null && _client.Cache.Guilds.Count > 0)
            return;

        var tcs = new TaskCompletionSource();
        
        ValueTask Handler(ReadyEventArgs args)
        {
            tcs.TrySetResult();
            return ValueTask.CompletedTask;
        }

        _client.Ready += Handler;
        
        try
        {
            await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30), ct));
        }
        finally
        {
            _client.Ready -= Handler;
        }
    }
}