using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Ogur.Sentinel.Desktop.Models;

namespace Ogur.Sentinel.Desktop.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private string? _token;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient 
        { 
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? CurrentRole { get; private set; }
    public string? CurrentUsername { get; private set; }

    public async Task<(bool success, string? role, string? error)> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login", new
            {
                username,
                password
            });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                _token = result.GetProperty("token").GetString();
                CurrentRole = result.GetProperty("role").GetString();
                CurrentUsername = username;

                _http.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

                return (true, CurrentRole, null);
            }

            return (false, null, "Invalid credentials");
        }
        catch (Exception ex)
        {
            return (false, null, $"Connection error: {ex.Message}");
        }
    }

    public async Task<RespawnTimes?> GetNextRespawnAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<RespawnTimes>("/respawn/next");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Settings?> GetSettingsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<Settings>("/settings");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateSettingsAsync(Settings settings)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/settings", settings);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Logout()
    {
        _token = null;
        CurrentRole = null;
        CurrentUsername = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }
}