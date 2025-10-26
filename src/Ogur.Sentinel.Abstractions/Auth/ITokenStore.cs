namespace Ogur.Sentinel.Abstractions.Auth;

public interface ITokenStore
{
    Task AddAsync(string token, TokenData data);
    Task<(bool success, TokenData? data)> TryGetAsync(string token);
    Task RemoveAsync(string token);
}