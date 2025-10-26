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

        _logger.LogWarning("🔍 AUTH MIDDLEWARE: Path={Path}", path);
        _logger.LogWarning("🔍 IsPublicPagePath={IsPublic}", IsPublicPagePath(path));  // ✅ Zmień tutaj

        if (IsPublicPagePath(path))  // ✅ I tutaj
        {
            _logger.LogWarning("✅ PUBLIC - passing through");
            await _next(context);
            return;
        }

        _logger.LogWarning("❌ NOT PUBLIC - checking auth");

        var authHeader = context.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            if (path.StartsWith("/api") || IsApiEndpoint(path))
            {
                await HandleUnauthorized(context, path);
                return;
            }
            
            //await _next(context);
            context.Response.Redirect("/login");
            return;
        }

        var token = authHeader.Replace("Bearer ", "");
        var (success, tokenData) = await tokenStore.TryGetAsync(token);

        if (!success || tokenData == null || tokenData.ExpiresAt < DateTime.UtcNow)
        {
            if (path.StartsWith("/api") || IsApiEndpoint(path))
            {
                await HandleUnauthorized(context, path, "Invalid or expired token");
                return;
            }

            //await _next(context);
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
               path == "/respawn" ||
               path == "/login" ||
               path == "/logout" ||
               path.StartsWith("/api/auth/login") ||
               path.StartsWith("/health") ||
               path.StartsWith("/version") ||
               //path.StartsWith("/worker") ||
               //path.StartsWith("/respawn/") ||
               //path.StartsWith("/settings") ||
               //path.StartsWith("/channels") ||
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
           path.StartsWith("/worker");
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
        
        context.Response.StatusCode = 403;
        
        var message = role switch
        {
            Roles.Timer => "Timer role can only view next respawn time",
            Roles.Operator => "Operator cannot modify global settings",
            _ => "Access forbidden"
        };

        await context.Response.WriteAsJsonAsync(new { error = message });
    }
    
    private static bool HasPermission(string role, string method, string path)
    {
        return role switch
        {
            Roles.Admin => true,
            Roles.Operator => !((method == "POST" || method == "PATCH") && path.StartsWith("/settings")),
            Roles.Timer => method == "GET" && path == "/respawn/next",
            _ => false
        };
    }
}

public static class AuthMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthMiddleware>();
    }
}