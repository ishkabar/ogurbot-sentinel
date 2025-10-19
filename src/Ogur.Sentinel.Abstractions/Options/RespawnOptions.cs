namespace Ogur.Sentinel.Abstractions.Options;

public sealed class RespawnOptions
{
    public string SettingsFile { get; set; } = "appsettings/respawn.settings.json";
    public string? Sound10m { get; set; } = "/assets/respawn_10m.wav";
    public string? Sound2h  { get; set; } = "/assets/respawn_2h.wav";
}