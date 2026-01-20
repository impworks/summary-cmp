using Microsoft.AspNetCore.Mvc.RazorPages;
using SummaryCmp.Web.Data.Entities;
using SummaryCmp.Web.Services;

namespace SummaryCmp.Web.Pages.History;

public class IndexModel : PageModel
{
    private readonly SummarizationService _summarizationService;

    public IndexModel(SummarizationService summarizationService)
    {
        _summarizationService = summarizationService;
    }

    public List<ComparisonSession> Sessions { get; set; } = new();

    public async Task OnGetAsync()
    {
        Sessions = await _summarizationService.GetRecentSessionsAsync(50, HttpContext.RequestAborted);
    }
}
