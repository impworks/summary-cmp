using Microsoft.EntityFrameworkCore;
using SummaryCmp.Web.Data;
using SummaryCmp.Web.Data.Entities;
using SummaryCmp.Web.Services.Providers;

namespace SummaryCmp.Web.Services;

public class SummarizationService
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<ISummaryProvider> _providers;
    private readonly ILogger<SummarizationService> _logger;

    public SummarizationService(AppDbContext db, IEnumerable<ISummaryProvider> providers, ILogger<SummarizationService> logger)
    {
        _db = db;
        _providers = providers;
        _logger = logger;
    }

    public async Task<ComparisonSession> CreateAndRunComparisonAsync(string inputText, string? sampleFileName, string? sampleDescription, CancellationToken ct)
    {
        // Get enabled provider models
        var enabledModels = await _db.ProviderModels
            .Where(pm => pm.IsEnabled)
            .OrderBy(pm => pm.DisplayName)
            .ToListAsync(ct);

        if (!enabledModels.Any())
            throw new InvalidOperationException("No provider models are enabled");

        // Create session
        var session = new ComparisonSession
        {
            Id = Guid.NewGuid(),
            InputText = inputText,
            SampleFileName = sampleFileName,
            SampleDescription = sampleDescription,
            CreatedAt = DateTime.UtcNow
        };

        _db.ComparisonSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        // Run summarization in parallel
        var tasks = enabledModels.Select(async model =>
        {
            var provider = _providers.FirstOrDefault(p => p.ProviderKey == model.ProviderKey);
            if (provider == null)
            {
                return new SummaryResult
                {
                    SessionId = session.Id,
                    ProviderModelId = model.Id,
                    IsSuccess = false,
                    ErrorMessage = $"Provider {model.ProviderKey} not found",
                    DurationMs = 0
                };
            }

            _logger.LogInformation("Starting summarization with {Provider}/{Model}", model.ProviderKey, model.ModelId);

            var response = await provider.SummarizeAsync(inputText, model.ModelId, ct);

            return new SummaryResult
            {
                SessionId = session.Id,
                ProviderModelId = model.Id,
                SummaryText = response.SummaryText,
                IsSuccess = response.IsSuccess,
                ErrorMessage = response.ErrorMessage,
                DurationMs = response.DurationMs,
                InputTokens = response.InputTokens,
                InternalTokens = response.InternalTokens,
                OutputTokens = response.OutputTokens
            };
        });

        var results = await Task.WhenAll(tasks);

        // Randomize display order for blind comparison
        var random = new Random();
        var displayOrders = Enumerable.Range(1, results.Length).OrderBy(_ => random.Next()).ToArray();

        for (int i = 0; i < results.Length; i++)
        {
            results[i].DisplayOrder = displayOrders[i];
        }

        _db.SummaryResults.AddRange(results);
        await _db.SaveChangesAsync(ct);

        return session;
    }

    public async Task<ComparisonSession?> GetSessionWithResultsAsync(Guid sessionId, CancellationToken ct)
    {
        return await _db.ComparisonSessions
            .Include(s => s.SummaryResults)
                .ThenInclude(r => r.ProviderModel)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task SaveRankingsAsync(Guid sessionId, Dictionary<int, int> rankings, HashSet<int> unacceptableIds, CancellationToken ct)
    {
        var session = await _db.ComparisonSessions
            .Include(s => s.SummaryResults)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
            throw new InvalidOperationException("Session not found");

        foreach (var result in session.SummaryResults)
        {
            if (rankings.TryGetValue(result.Id, out var rank))
            {
                result.UserRank = rank;
            }
            result.IsUnacceptable = unacceptableIds.Contains(result.Id);
        }

        session.IsRanked = true;
        session.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ComparisonSession>> GetRecentSessionsAsync(int count, CancellationToken ct)
    {
        return await _db.ComparisonSessions
            .Include(s => s.SummaryResults)
                .ThenInclude(r => r.ProviderModel)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    // Admin methods
    public async Task<List<ComparisonSession>> GetAllSessionsAsync(CancellationToken ct)
    {
        return await _db.ComparisonSessions
            .Include(s => s.SummaryResults)
                .ThenInclude(r => r.ProviderModel)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _db.ComparisonSessions
            .Include(s => s.SummaryResults)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session != null)
        {
            _db.SummaryResults.RemoveRange(session.SummaryResults);
            _db.ComparisonSessions.Remove(session);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task ToggleResultFlagAsync(int resultId, CancellationToken ct)
    {
        var result = await _db.SummaryResults.FindAsync(new object[] { resultId }, ct);
        if (result != null)
        {
            result.IsUnacceptable = !result.IsUnacceptable;
            await _db.SaveChangesAsync(ct);
        }
    }
}
