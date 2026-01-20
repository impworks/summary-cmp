namespace SummaryCmp.Web.Data.Entities;

public class ComparisonSession
{
    public Guid Id { get; set; }
    public required string InputText { get; set; }
    public string? SampleFileName { get; set; }
    public string? SampleDescription { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool IsRanked { get; set; }

    public ICollection<SummaryResult> SummaryResults { get; set; } = new List<SummaryResult>();
}
