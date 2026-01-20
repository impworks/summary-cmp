using SummaryCmp.Web.Data.Entities;

namespace SummaryCmp.Web.Services.Providers;

public interface ISummaryProvider
{
    string ProviderKey { get; }
    bool IsConfigured { get; }
    Task<SummaryResponse> SummarizeAsync(string text, string modelId, CancellationToken ct);
    decimal? CalculatePrice(SummaryResult result);
}

public class SummaryResponse
{
    public bool IsSuccess { get; set; }
    public string? SummaryText { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }

    // Token usage tracking
    public int? InputTokens { get; set; }
    public int? InternalTokens { get; set; }  // Reasoning/thinking tokens
    public int? OutputTokens { get; set; }
}
