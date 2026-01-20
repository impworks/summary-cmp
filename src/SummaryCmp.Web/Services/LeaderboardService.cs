using Microsoft.EntityFrameworkCore;
using SummaryCmp.Web.Data;
using SummaryCmp.Web.Services.Providers;

namespace SummaryCmp.Web.Services;

public class LeaderboardService
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<ISummaryProvider> _providers;

    public LeaderboardService(AppDbContext db, IEnumerable<ISummaryProvider> providers)
    {
        _db = db;
        _providers = providers;
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(CancellationToken ct)
    {
        // Get all results with their provider models and sessions (for price calculation)
        var allResults = await _db.SummaryResults
            .Include(r => r.ProviderModel)
            .Include(r => r.Session)
            .ToListAsync(ct);

        var providerLookup = _providers.ToDictionary(p => p.ProviderKey);

        // Group by provider model
        var entries = allResults
            .GroupBy(r => r.ProviderModelId)
            .Select(g =>
            {
                var successfulRanked = g.Where(r => r.IsSuccess && r.UserRank.HasValue).ToList();
                var failed = g.Where(r => !r.IsSuccess).ToList();

                // Calculate average price
                decimal? averagePrice = null;
                if (successfulRanked.Count > 0 && providerLookup.TryGetValue(g.First().ProviderModel.ProviderKey, out var provider))
                {
                    var prices = successfulRanked
                        .Select(r => provider.CalculatePrice(r))
                        .Where(p => p.HasValue)
                        .Select(p => p!.Value)
                        .ToList();

                    if (prices.Count > 0)
                    {
                        averagePrice = prices.Average();
                    }
                }

                return new LeaderboardEntry
                {
                    ProviderModelId = g.Key,
                    DisplayName = g.First().ProviderModel.DisplayName,
                    ProviderKey = g.First().ProviderModel.ProviderKey,
                    TotalComparisons = successfulRanked.Count,
                    AverageRank = successfulRanked.Count > 0 ? successfulRanked.Average(r => r.UserRank!.Value) : 0,
                    FirstPlaceWins = successfulRanked.Count(r => r.UserRank == 1),
                    AverageDurationMs = successfulRanked.Count > 0 ? successfulRanked.Average(r => r.DurationMs) : 0,
                    FailedCount = failed.Count,
                    AveragePrice = averagePrice
                };
            })
            .Where(e => e.TotalComparisons > 0 || e.FailedCount > 0)
            .OrderBy(e => e.TotalComparisons > 0 ? e.AverageRank : double.MaxValue)
            .ThenByDescending(e => e.TotalComparisons)
            .ToList();

        return entries;
    }
}

public class LeaderboardEntry
{
    public int ProviderModelId { get; set; }
    public required string DisplayName { get; set; }
    public required string ProviderKey { get; set; }
    public int TotalComparisons { get; set; }
    public double AverageRank { get; set; }
    public int FirstPlaceWins { get; set; }
    public double AverageDurationMs { get; set; }
    public int FailedCount { get; set; }
    public decimal? AveragePrice { get; set; }
}
