using NetCord.Services.ApplicationCommands;

namespace Ogur.Sentinel.Worker.Discord.Modules;

public class TestModule : ApplicationCommandModule<SlashCommandContext>
{
    [SlashCommand("ping", "Test command")]
    public string Ping()
    {
        return "Pong! 🏓";
    }
    
    [SlashCommand("hello", "Say hello")]
    public string Hello([SlashCommandParameter(Description = "Your name")] string name = "World")
    {
        return $"Hello, {name}! 👋";
    }
}