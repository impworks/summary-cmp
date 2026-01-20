using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SummaryCmp.Web.Data;
using SummaryCmp.Web.Services;
using SummaryCmp.Web.Services.Providers;

namespace SummaryCmp.Web.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly SummarizationService _summarizationService;
    private readonly IWebHostEnvironment _env;
    private readonly IEnumerable<ISummaryProvider> _providers;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        AppDbContext db,
        SummarizationService summarizationService,
        IWebHostEnvironment env,
        IEnumerable<ISummaryProvider> providers,
        ILogger<IndexModel> logger)
    {
        _db = db;
        _summarizationService = summarizationService;
        _env = env;
        _providers = providers;
        _logger = logger;
    }

    public List<SampleText> SampleTexts { get; set; } = new();
    public List<ProviderStatus> Providers { get; set; } = new();
    public string? InputText { get; set; }
    public string? ErrorMessage { get; set; }

    public bool HasConfiguredProviders => Providers.Any(p => p.IsConfigured);

    public async Task OnGetAsync()
    {
        SampleTexts = LoadSampleTexts();
        Providers = await LoadProviderStatusesAsync();
    }

    public async Task<IActionResult> OnPostAsync(string inputText, string? sampleFileName, string? sampleDescription)
    {
        SampleTexts = LoadSampleTexts();
        Providers = await LoadProviderStatusesAsync();
        InputText = inputText;

        if (string.IsNullOrWhiteSpace(inputText))
        {
            ErrorMessage = "Please enter text to summarize.";
            return Page();
        }

        if (inputText.Length < 50)
        {
            ErrorMessage = "Text should be at least 50 characters for meaningful summaries.";
            return Page();
        }

        if (!HasConfiguredProviders)
        {
            ErrorMessage = "No providers are configured. Please set API keys in the configuration.";
            return Page();
        }

        try
        {
            var session = await _summarizationService.CreateAndRunComparisonAsync(inputText, sampleFileName, sampleDescription, HttpContext.RequestAborted);
            return RedirectToPage("/Compare", new { id = session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comparison");
            ErrorMessage = "An error occurred while creating the comparison. Please try again.";
            return Page();
        }
    }

    private async Task<List<ProviderStatus>> LoadProviderStatusesAsync()
    {
        var providerModels = await _db.ProviderModels
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.DisplayName)
            .ToListAsync();

        var providerLookup = _providers.ToDictionary(p => p.ProviderKey);

        return providerModels.Select(p => new ProviderStatus
        {
            DisplayName = p.DisplayName,
            ProviderKey = p.ProviderKey,
            IsConfigured = providerLookup.TryGetValue(p.ProviderKey, out var provider) && provider.IsConfigured
        }).ToList();
    }

    private List<SampleText> LoadSampleTexts()
    {
        var samples = new List<SampleText>();
        var samplesPath = Path.Combine(_env.ContentRootPath, "samples");

        if (!Directory.Exists(samplesPath))
            return samples;

        foreach (var file in Directory.GetFiles(samplesPath, "*.txt").OrderBy(f => f))
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var lines = System.IO.File.ReadAllLines(file);

                // First line is the description, rest is content
                var description = lines.Length > 0 ? lines[0].Trim() : string.Empty;
                var content = lines.Length > 1 ? string.Join(Environment.NewLine, lines.Skip(1)).TrimStart() : string.Empty;

                samples.Add(new SampleText
                {
                    Title = fileName,
                    Description = description,
                    Content = content
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load sample file: {File}", file);
            }
        }

        return samples;
    }
}

public class SampleText
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Content { get; set; }
}

public class ProviderStatus
{
    public required string DisplayName { get; set; }
    public required string ProviderKey { get; set; }
    public bool IsConfigured { get; set; }
}
