using Microsoft.EntityFrameworkCore;
using SummaryCmp.Web.Data.Entities;

namespace SummaryCmp.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProviderModel> ProviderModels => Set<ProviderModel>();
    public DbSet<ComparisonSession> ComparisonSessions => Set<ComparisonSession>();
    public DbSet<SummaryResult> SummaryResults => Set<SummaryResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProviderModel>(entity =>
        {
            entity.HasIndex(e => new { e.ProviderKey, e.ModelId }).IsUnique();
        });

        modelBuilder.Entity<SummaryResult>(entity =>
        {
            entity.HasOne(e => e.Session)
                .WithMany(s => s.SummaryResults)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProviderModel)
                .WithMany(p => p.SummaryResults)
                .HasForeignKey(e => e.ProviderModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
