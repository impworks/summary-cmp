using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SummaryCmp.Web.Data.Entities;
using SummaryCmp.Web.Services;

namespace SummaryCmp.Web.Pages.History;

public class DetailsModel : PageModel
{
    private readonly SummarizationService _summarizationService;

    public DetailsModel(SummarizationService summarizationService)
    {
        _summarizationService = summarizationService;
    }

    public ComparisonSession? Session { get; set; }
    public List<SummaryResult> Results { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Session = await _summarizationService.GetSessionWithResultsAsync(id, HttpContext.RequestAborted);

        if (Session == null)
        {
            return NotFound();
        }

        Results = Session.SummaryResults
            .OrderBy(r => r.UserRank ?? int.MaxValue)
            .ThenBy(r => r.ProviderModel.DisplayName)
            .ToList();

        return Page();
    }
}
