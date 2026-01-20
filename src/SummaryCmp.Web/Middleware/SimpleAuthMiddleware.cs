using Microsoft.Extensions.Options;
using SummaryCmp.Web.Configuration;

namespace SummaryCmp.Web.Middleware;

public class SimpleAuthMiddleware
{
    private readonly RequestDelegate _next;
    private const string AuthCookieName = "SummaryCmpAuth";

    public SimpleAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<AuthOptions> authOptions)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Allow access to login page and static files
        if (path.StartsWith("/login") ||
            path.StartsWith("/css") ||
            path.StartsWith("/js") ||
            path.StartsWith("/lib") ||
            path.StartsWith("/_framework"))
        {
            await _next(context);
            return;
        }

        // Check if password is configured
        var password = authOptions.Value.Password;
        if (string.IsNullOrEmpty(password))
        {
            // No password configured, allow access
            await _next(context);
            return;
        }

        // Check for valid auth cookie
        if (context.Request.Cookies.TryGetValue(AuthCookieName, out var cookieValue) &&
            cookieValue == ComputeAuthToken(password))
        {
            await _next(context);
            return;
        }

        // Redirect to login
        context.Response.Redirect("/Login");
    }

    public static string ComputeAuthToken(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "SummaryCmpSalt"));
        return Convert.ToBase64String(hash);
    }

    public static void SetAuthCookie(HttpResponse response, string password)
    {
        response.Cookies.Append(AuthCookieName, ComputeAuthToken(password), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }
}
