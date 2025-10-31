namespace Ogur.Sentinel.Desktop.Models;

public record Settings(
    List<string> Channels,
    string BaseHhmm,
    int LeadSeconds,
    bool Enabled10m,
    bool Enabled2h
);