using Microsoft.AspNetCore.Http;

namespace Ogur.Sentinel.Api.Services;

public static class BaerimLang
{
    private const string CookieName = "baerim_lang";

    public static string Resolve(HttpRequest request, HttpResponse response, string? queryLang)
    {
        if (queryLang is "pl" or "en")
        {
            response.Cookies.Append(CookieName, queryLang, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true
            });
            return queryLang;
        }

        return request.Cookies[CookieName] == "en" ? "en" : "pl";
    }
}