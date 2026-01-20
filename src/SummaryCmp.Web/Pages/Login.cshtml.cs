using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SummaryCmp.Web.Configuration;
using SummaryCmp.Web.Middleware;

namespace SummaryCmp.Web.Pages;

public class LoginModel : PageModel
{
    private readonly AuthOptions _authOptions;

    public LoginModel(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions.Value;
    }

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(_authOptions.Password))
        {
            return RedirectToPage("/Index");
        }
        return Page();
    }

    public IActionResult OnPost(string password)
    {
        if (string.IsNullOrEmpty(_authOptions.Password))
        {
            return RedirectToPage("/Index");
        }

        if (password == _authOptions.Password)
        {
            SimpleAuthMiddleware.SetAuthCookie(Response, _authOptions.Password);
            return RedirectToPage("/Index");
        }

        ErrorMessage = "Invalid password";
        return Page();
    }
}
