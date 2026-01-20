namespace SummaryCmp.Web.Data.Entities;

public class SummaryResult
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public int ProviderModelId { get; set; }
    public string? SummaryText { get; set; }
    public long DurationMs { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int? UserRank { get; set; }
    public int DisplayOrder { get; set; }

    // Token usage tracking
    public int? InputTokens { get; set; }
    public int? InternalTokens { get; set; }  // Reasoning/thinking tokens
    public int? OutputTokens { get; set; }

    public ComparisonSession Session { get; set; } = null!;
    public ProviderModel ProviderModel { get; set; } = null!;
}
