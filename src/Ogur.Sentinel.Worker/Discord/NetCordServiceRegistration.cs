using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Services.ApplicationCommands;
using Ogur.Sentinel.Worker.Discord.Modules;
using Ogur.Sentinel.Worker.Discord.Handlers;
using Ogur.Sentinel.Worker.Services;

namespace Ogur.Sentinel.Worker.Discord;

/// <summary>
/// NetCord service registration with proper Hosting support
/// </summary>
public static class NetCordServiceRegistration
{
    public static IServiceCollection AddDiscordServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var discordToken = configuration["Settings:DiscordToken"]
                           ?? throw new InvalidOperationException("Discord token not configured");

        // 1. Add GatewayClient with Hosting support
        services.AddDiscordGateway(options =>
        {
            options.Intents = GatewayIntents.Guilds
                              | GatewayIntents.GuildVoiceStates
                              | GatewayIntents.GuildMessages;
            
            options.Token = discordToken; // Bezpośrednio string, nie BotToken
        });
        services.AddSingleton<IVoiceServerUpdateGatewayHandler, VoiceServerUpdateHandler>();

        // 2. Add Application Commands service manually
        services.AddSingleton<ApplicationCommandService<SlashCommandContext>>();
        
        // 3. Add command modules as singletons
        services.AddSingleton<RespawnModule>();
        services.AddSingleton<LeaveModule>();
        services.AddSingleton<AdminBreakModule>();

        // 4. Add command registration service
        services.AddHostedService<CommandRegistrationService>();

        // 5. Add Voice service
        services.AddSingleton<VoiceService3>();

        // 6. Add Discord-specific services
        services.AddSingleton<DiscordReadyService>();

        // 7. Add Discord Hosted Service (for event handling)
        services.AddHostedService<DiscordBotHostedService>();

        return services;
    }
}