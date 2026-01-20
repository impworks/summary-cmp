using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SummaryCmp.Web.Configuration;
using SummaryCmp.Web.Data.Entities;

namespace SummaryCmp.Web.Services.Providers;

public class ClaudeProvider : ISummaryProvider
{
    private readonly HttpClient _httpClient;
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeProvider> _logger;

    public string ProviderKey => "Claude";
    public bool IsConfigured => !string.IsNullOrEmpty(_options.ApiKey);

    public ClaudeProvider(IHttpClientFactory httpClientFactory, IOptions<ProvidersOptions> options, ILogger<ClaudeProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _options = options.Value.Claude;
        _logger = logger;
    }

    public async Task<SummaryResponse> SummarizeAsync(string text, string modelId, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            return new SummaryResponse
            {
                IsSuccess = false,
                ErrorMessage = "Claude API key not configured",
                DurationMs = sw.ElapsedMilliseconds
            };
        }

        try
        {
            var request = new
            {
                model = modelId,
                max_tokens = 1024,
                messages = new[]
                {
                    new { role = "user", content = $"Summarize the following transcribed voice mail in a single sentence, in the same language:\n\n{text}" }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Claude API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new SummaryResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"API error: {response.StatusCode}",
                    DurationMs = sw.ElapsedMilliseconds
                };
            }

            var result = JsonDocument.Parse(responseBody);
            var summaryText = result.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            // Parse token usage
            int? inputTokens = null;
            int? outputTokens = null;

            if (result.RootElement.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("input_tokens", out var inputTokensElement))
                    inputTokens = inputTokensElement.GetInt32();

                if (usage.TryGetProperty("output_tokens", out var outputTokensElement))
                    outputTokens = outputTokensElement.GetInt32();
            }

            return new SummaryResponse
            {
                IsSuccess = true,
                SummaryText = summaryText,
                DurationMs = sw.ElapsedMilliseconds,
                InputTokens = inputTokens,
                OutputTokens = outputTokens
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error calling Claude API");
            return new SummaryResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    public decimal? CalculatePrice(SummaryResult result)
    {
        if (!result.InputTokens.HasValue || !result.OutputTokens.HasValue)
            return null;

        // Get pricing based on model
        var (inputPricePerMillion, outputPricePerMillion) = GetModelPricing(result.ProviderModel?.ModelId);

        var inputCost = result.InputTokens.Value * inputPricePerMillion / 1_000_000m;
        var outputCost = result.OutputTokens.Value * outputPricePerMillion / 1_000_000m;

        return inputCost + outputCost;
    }

    private static (decimal input, decimal output) GetModelPricing(string? modelId)
    {
        // Claude pricing per MTok (https://platform.claude.com/docs/en/about-claude/pricing)
        return modelId switch
        {
            "claude-opus-4-5" => (5.00m, 25.00m),
            "claude-sonnet-4-5" => (3.00m, 15.00m),
            "claude-haiku-4-5" => (1.00m, 5.00m),
            _ => (3.00m, 15.00m) // Default to Sonnet pricing
        };
    }
}
