using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SummaryCmp.Web.Data.Entities;
using SummaryCmp.Web.Services;

namespace SummaryCmp.Web.Pages;

public class CompareModel : PageModel
{
    private readonly SummarizationService _summarizationService;
    private readonly ILogger<CompareModel> _logger;

    public CompareModel(SummarizationService summarizationService, ILogger<CompareModel> logger)
    {
        _summarizationService = summarizationService;
        _logger = logger;
    }

    public ComparisonSession? Session { get; set; }
    public List<SummaryResult> SuccessfulResults { get; set; } = new();
    public List<SummaryResult> FailedResults { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Session = await _summarizationService.GetSessionWithResultsAsync(id, HttpContext.RequestAborted);

        if (Session == null)
        {
            return NotFound();
        }

        if (Session.IsRanked)
        {
            return RedirectToPage("/Results", new { id });
        }

        var allResults = Session.SummaryResults.OrderBy(r => r.DisplayOrder).ToList();
        SuccessfulResults = allResults.Where(r => r.IsSuccess).ToList();
        FailedResults = allResults.Where(r => !r.IsSuccess).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid sessionId, string rankingOrder, string? unacceptableIds)
    {
        Session = await _summarizationService.GetSessionWithResultsAsync(sessionId, HttpContext.RequestAborted);

        if (Session == null)
        {
            return NotFound();
        }

        var allResults = Session.SummaryResults.OrderBy(r => r.DisplayOrder).ToList();
        SuccessfulResults = allResults.Where(r => r.IsSuccess).ToList();
        FailedResults = allResults.Where(r => !r.IsSuccess).ToList();

        if (string.IsNullOrEmpty(rankingOrder))
        {
            ErrorMessage = "Please rank all summaries by dragging them into order.";
            return Page();
        }

        // Parse the ranking order (comma-separated result IDs in order)
        var orderedIds = rankingOrder.Split(',')
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();

        if (orderedIds.Count != SuccessfulResults.Count)
        {
            ErrorMessage = "Please rank all successful summaries.";
            return Page();
        }

        // Create rankings dictionary (resultId -> rank)
        var rankings = new Dictionary<int, int>();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            rankings[orderedIds[i]] = i + 1; // Rank 1 = best (first in order)
        }

        // Assign last ranks to failed results
        int failedRank = SuccessfulResults.Count + 1;
        foreach (var failed in FailedResults)
        {
            rankings[failed.Id] = failedRank++;
        }

        // Parse unacceptable IDs
        var unacceptableSet = new HashSet<int>();
        if (!string.IsNullOrEmpty(unacceptableIds))
        {
            foreach (var idStr in unacceptableIds.Split(','))
            {
                if (int.TryParse(idStr, out var id))
                {
                    unacceptableSet.Add(id);
                }
            }
        }

        try
        {
            await _summarizationService.SaveRankingsAsync(sessionId, rankings, unacceptableSet, HttpContext.RequestAborted);
            return RedirectToPage("/Results", new { id = sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving rankings");
            ErrorMessage = "An error occurred while saving rankings. Please try again.";
            return Page();
        }
    }
}
