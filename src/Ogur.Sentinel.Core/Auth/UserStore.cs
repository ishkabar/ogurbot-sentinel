using System.Text.Json;
using Ogur.Sentinel.Abstractions.Auth;
using Microsoft.Extensions.Logging;


namespace Ogur.Sentinel.Core.Auth;

public class UserStore
{
    private readonly string _filePath;
    private List<User> _users = new();
    private readonly object _lock = new();
    private readonly ILogger<UserStore> _logger;


    public UserStore(string filePath, ILogger<UserStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
        Load();
    }
    
    private static bool IsValidRole(string role)
    {
        return role == Roles.Admin || 
               role == Roles.Operator || 
               role == Roles.Timer;
    }

    


    private void Load()
    {
        try
        {
            _logger.LogInformation("👥 Loading users from: {Path}", _filePath);
            _logger.LogInformation("👥 File exists: {Exists}", File.Exists(_filePath));
        
            if (!File.Exists(_filePath))
            {
                _logger.LogWarning("👥 users.json not found, creating empty file");
            
                var emptyUsers = new { users = Array.Empty<User>() };
                var json = JsonSerializer.Serialize(emptyUsers, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.WriteAllText(_filePath, json);
            }

            var fileContent = File.ReadAllText(_filePath);
            _logger.LogDebug("👥 File content:\n{Content}", fileContent);
        
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var data = JsonSerializer.Deserialize<UsersFile>(fileContent, options);
        
            _logger.LogInformation("👥 Deserialized users count: {Count}", data?.Users?.Count ?? 0);
        
            lock (_lock)
            {
                _users = data?.Users ?? new List<User>();
                _logger.LogInformation("👥 Successfully loaded {Count} users", _users.Count);
            
                foreach (var user in _users)
                {
                    _logger.LogDebug("   - User: '{Username}', Role: '{Role}'", user.Username, user.Role);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load users from {Path}", _filePath);
            throw new Exception($"Failed to load users from {_filePath}", ex);
        }
    }

    public User? ValidateUser(string username, string password)
    {
        lock (_lock)
        {
            _logger.LogDebug("🔍 ValidateUser: username='{Username}', users count={Count}", username, _users.Count);
            
            var result = _users.FirstOrDefault(u => 
                u.Username == username && 
                u.Password == password);
            
            _logger.LogDebug("🔍 Match found: {Found}", result != null);
            
            return result;
        }
    }

    public List<User> GetAllUsers()
    {
        lock (_lock)
        {
            return _users.ToList();
        }
    }

    public void Reload()
    {
        _logger.LogInformation("🔄 Reloading users...");
        Load();
    }

    private class UsersFile
    {
        public List<User> Users { get; set; } = new();
    }
}