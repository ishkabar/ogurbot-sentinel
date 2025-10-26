using Ogur.Sentinel.Abstractions.Auth;

namespace Ogur.Sentinel.Core.Auth;

public class InMemoryTokenStore : ITokenStore
{
    private readonly Dictionary<string, TokenData> _tokens = new();
    private readonly object _lock = new();
    
    public Task AddAsync(string token, TokenData data)
    {
        lock (_lock)
        {
            _tokens[token] = data;
        }
        return Task.CompletedTask;
    }
    
    public Task<(bool success, TokenData? data)> TryGetAsync(string token)
    {
        lock (_lock)
        {
            var success = _tokens.TryGetValue(token, out var data);
            return Task.FromResult((success, data));
        }
    }
    
    public Task RemoveAsync(string token)
    {
        lock (_lock)
        {
            _tokens.Remove(token);
        }
        return Task.CompletedTask;
    }
}