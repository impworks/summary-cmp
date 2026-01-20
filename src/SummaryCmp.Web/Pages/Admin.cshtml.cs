using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SummaryCmp.Web.Configuration;
using SummaryCmp.Web.Data.Entities;
using SummaryCmp.Web.Services;

namespace SummaryCmp.Web.Pages;

public class AdminModel : PageModel
{
    private readonly SummarizationService _summarizationService;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly IConfiguration _configuration;
    private const string AdminCookieName = "SummaryCmpAdmin";

    public AdminModel(
        SummarizationService summarizationService,
        IOptions<AuthOptions> authOptions,
        IConfiguration configuration)
    {
        _summarizationService = summarizationService;
        _authOptions = authOptions;
        _configuration = configuration;
    }

    public bool IsAuthenticated { get; set; }
    public bool IsConfigured { get; set; }
    public List<ComparisonSession> Sessions { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        var adminPassword = _authOptions.Value.AdminPassword;
        IsConfigured = !string.IsNullOrEmpty(adminPassword);

        if (!IsConfigured)
        {
            return;
        }

        IsAuthenticated = Request.Cookies.TryGetValue(AdminCookieName, out var cookieValue) &&
                          cookieValue == ComputeAdminToken(adminPassword);

        if (IsAuthenticated)
        {
            Sessions = await _summarizationService.GetAllSessionsAsync(HttpContext.RequestAborted);
        }
    }

    public async Task<IActionResult> OnPostLoginAsync(string password)
    {
        var adminPassword = _authOptions.Value.AdminPassword;

        if (string.IsNullOrEmpty(adminPassword))
        {
            return RedirectToPage();
        }

        if (password == adminPassword)
        {
            Response.Cookies.Append(AdminCookieName, ComputeAdminToken(adminPassword), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(4)
            });
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSessionAsync(Guid sessionId)
    {
        if (!IsAdminAuthenticated())
        {
            return RedirectToPage();
        }

        await _summarizationService.DeleteSessionAsync(sessionId, HttpContext.RequestAborted);
        TempData["SuccessMessage"] = "Session deleted successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleFlagAsync(int resultId)
    {
        if (!IsAdminAuthenticated())
        {
            return RedirectToPage();
        }

        await _summarizationService.ToggleResultFlagAsync(resultId, HttpContext.RequestAborted);
        return RedirectToPage();
    }

    public IActionResult OnGetDownloadDatabase()
    {
        if (!IsAdminAuthenticated())
        {
            return RedirectToPage();
        }

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            return NotFound("Database connection string not configured");
        }

        // Parse the Data Source from connection string
        var dataSource = connectionString
            .Split(';')
            .Select(p => p.Trim())
            .FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase));

        if (dataSource == null)
        {
            return NotFound("Could not determine database file path");
        }

        var dbPath = dataSource.Substring("Data Source=".Length);

        if (!System.IO.File.Exists(dbPath))
        {
            return NotFound($"Database file not found: {dbPath}");
        }

        SqliteConnection.ClearAllPools();

        var fileName = $"summarycmp-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.db";
        return File(new FileStream(dbPath, FileMode.Open, FileAccess.Read), "application/x-sqlite3", fileName);
    }

    private bool IsAdminAuthenticated()
    {
        var adminPassword = _authOptions.Value.AdminPassword;
        if (string.IsNullOrEmpty(adminPassword))
        {
            return false;
        }

        return Request.Cookies.TryGetValue(AdminCookieName, out var cookieValue) &&
               cookieValue == ComputeAdminToken(adminPassword);
    }

    private static string ComputeAdminToken(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "SummaryCmpAdminSalt"));
        return Convert.ToBase64String(hash);
    }
}
