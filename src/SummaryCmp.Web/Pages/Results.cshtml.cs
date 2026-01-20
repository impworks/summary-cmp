using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SummaryCmp.Web.Data.Entities;
using SummaryCmp.Web.Services;
using SummaryCmp.Web.Services.Providers;

namespace SummaryCmp.Web.Pages;

public class ResultsModel : PageModel
{
    private readonly SummarizationService _summarizationService;
    private readonly IEnumerable<ISummaryProvider> _providers;

    public ResultsModel(SummarizationService summarizationService, IEnumerable<ISummaryProvider> providers)
    {
        _summarizationService = summarizationService;
        _providers = providers;
    }

    public ComparisonSession? Session { get; set; }
    public List<SummaryResult> Results { get; set; } = new();
    public Dictionary<int, decimal?> Prices { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Session = await _summarizationService.GetSessionWithResultsAsync(id, HttpContext.RequestAborted);

        if (Session == null)
        {
            return NotFound();
        }

        if (!Session.IsRanked)
        {
            return RedirectToPage("/Compare", new { id });
        }

        Results = Session.SummaryResults.OrderBy(r => r.UserRank).ToList();

        // Calculate prices for each result
        var providerLookup = _providers.ToDictionary(p => p.ProviderKey);
        foreach (var result in Results)
        {
            if (providerLookup.TryGetValue(result.ProviderModel.ProviderKey, out var provider))
            {
                Prices[result.Id] = provider.CalculatePrice(result);
            }
        }

        return Page();
    }
}
