using Microsoft.AspNetCore.Mvc.RazorPages;
using SummaryCmp.Web.Services;

namespace SummaryCmp.Web.Pages;

public class LeaderboardModel : PageModel
{
    private readonly LeaderboardService _leaderboardService;

    public LeaderboardModel(LeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    public List<LeaderboardEntry> Entries { get; set; } = new();

    public async Task OnGetAsync()
    {
        Entries = await _leaderboardService.GetLeaderboardAsync(HttpContext.RequestAborted);
    }
}
