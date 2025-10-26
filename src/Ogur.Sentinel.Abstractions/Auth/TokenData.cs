namespace Ogur.Sentinel.Abstractions.Auth;

public record TokenData
{
    public required string Username { get; set; } 
    public required string Role { get; set; }
    public DateTime ExpiresAt { get; set; }
}