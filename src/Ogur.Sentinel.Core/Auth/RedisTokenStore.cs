using Microsoft.Extensions.Caching.Distributed;
using Ogur.Sentinel.Abstractions.Auth;
using System.Text;
using System.Text.Json;

public class RedisTokenStore : ITokenStore
{
    private readonly IDistributedCache _cache;
    
    public RedisTokenStore(IDistributedCache cache)
    {
        _cache = cache;
    }
    
    public async Task AddAsync(string token, TokenData data)
    {
        var json = JsonSerializer.Serialize(data);
        await _cache.SetStringAsync($"token:{token}", json, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = data.ExpiresAt
        });
    }
    
    public async Task<(bool success, TokenData? data)> TryGetAsync(string token)
    {
        try
        {
            var json = await _cache.GetStringAsync($"token:{token}");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<TokenData>(json);
                return (data != null, data);
            }
        }
        catch
        {
            // Redis error or deserialization error
        }
        
        return (false, null);
    }
    
    public async Task RemoveAsync(string token)
    {
        await _cache.RemoveAsync($"token:{token}");
    }
}