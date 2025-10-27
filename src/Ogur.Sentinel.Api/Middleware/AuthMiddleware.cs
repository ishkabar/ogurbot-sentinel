using Ogur.Sentinel.Abstractions.Auth;

namespace Ogur.Sentinel.Api.Middleware;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthMiddleware> _logger;

    public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITokenStore tokenStore)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        if (IsPublicPagePath(path))
        {
            await _next(context);
            return;
        }

        // Sprawdź token w cookie dla Razor Pages
        var authHeader = context.Request.Headers["Authorization"].ToString();
        var cookieToken = context.Request.Cookies["auth_token"];
        
        var token = string.Empty;
        
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            token = authHeader.Replace("Bearer ", "");
        }
        else if (!string.IsNullOrEmpty(cookieToken))
        {
            token = cookieToken;
        }

        if (string.IsNullOrEmpty(token))
        {
            if (path.StartsWith("/api") || IsApiEndpoint(path))
            {
                await HandleUnauthorized(context, path);
                return;
            }

            context.Response.Redirect("/login");
            return;
        }

        var (success, tokenData) = await tokenStore.TryGetAsync(token);

        if (!success || tokenData == null || tokenData.ExpiresAt < DateTime.UtcNow)
        {
            if (path.StartsWith("/api") || IsApiEndpoint(path))
            {
                await HandleUnauthorized(context, path, "Invalid or expired token");
                return;
            }

            context.Response.Redirect("/login");
            return;
        }

        context.Items["Username"] = tokenData.Username;
        context.Items["Role"] = tokenData.Role;

        if (!HasPermission(tokenData.Role, context.Request.Method, path))
        {
            await HandleForbidden(context, tokenData.Role, path);
            return;
        }

        await _next(context);
    }

    private static bool IsPublicPagePath(string path)
    {
        return path == "/" ||
               path == "/index" ||
               path == "/privacy" ||
               path == "/download" ||
               path == "/login" ||
               path == "/logout" ||
               path == "/error403" ||
               path.StartsWith("/api/auth/login") ||
               path.StartsWith("/health") ||
               path.StartsWith("/version") ||
               path.StartsWith("/worker/version") ||
               path.StartsWith("/error") ||
               path.StartsWith("/css") ||
               path.StartsWith("/js") ||
               path.StartsWith("/lib") ||
               path.StartsWith("/favicon") ||
               path.StartsWith("/files");
    }


    private static bool IsApiEndpoint(string path)
    {
        return path.StartsWith("/respawn/") ||
               path.StartsWith("/settings") ||
               path.StartsWith("/channels") ||
               (path.StartsWith("/worker") && path != "/worker/version");
    }

    private async Task HandleUnauthorized(HttpContext context, string path, string? message = null)
    {
        if (path.StartsWith("/api"))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                error = message ?? "Unauthorized"
            });
        }
        else
        {
            context.Response.Redirect("/Login");
        }
    }

    private async Task HandleForbidden(HttpContext context, string role, string path)
    {
        _logger.LogWarning("Access denied for role {Role} on {Path}", role, path);

        var message = role switch
        {
            Roles.Timer => "Timer role can only view next respawn time",
            Roles.Operator => "Operator nie może modyfikować ustawień globalnych",
            _ => "Dostęp zabroniony"
        };

        // Jeśli to request do API, zwróć JSON
        if (path.StartsWith("/api") || IsApiEndpoint(path))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = message });
        }
        else
        {
            // Dla Razor Pages przekieruj na ładną stronę błędu
            context.Response.Redirect($"/Error403?message={Uri.EscapeDataString(message)}&role={role}");
        }
    }

    private static bool HasPermission(string role, string method, string path)
    {
        return role switch
        {
            Roles.Admin => true,
            Roles.Operator => IsOperatorAllowed(method, path),
            Roles.Timer => IsTimerAllowed(method, path),
            _ => false
        };
    }

    private static bool IsOperatorAllowed(string method, string path)
    {
        // Operator can view /respawn page but cannot modify settings
        if (path == "/respawn")
            return method == "GET";
        
        // Cannot modify global settings
        if ((method == "POST" || method == "PATCH") && path.StartsWith("/settings"))
            return false;
        
        return true;
    }

    private static bool IsTimerAllowed(string method, string path)
    {
        // Timer cannot access /respawn page
        if (path == "/respawn")
            return false;
        
        // Timer can only access specific API endpoints
        var allowed = method == "GET" && (
            path == "/api/respawn/next" ||
            path == "/respawn/next" ||  // WPF używa tego bez /api
            path.StartsWith("/api/auth/")
        );
        
        return allowed;
    }
}

public static class AuthMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthMiddleware>();
    }
}