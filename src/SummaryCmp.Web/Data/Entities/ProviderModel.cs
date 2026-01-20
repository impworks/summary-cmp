namespace SummaryCmp.Web.Data.Entities;

public class ProviderModel
{
    public int Id { get; set; }
    public required string ProviderKey { get; set; }
    public required string ModelId { get; set; }
    public required string DisplayName { get; set; }
    public bool IsEnabled { get; set; } = true;

    public ICollection<SummaryResult> SummaryResults { get; set; } = new List<SummaryResult>();
}
