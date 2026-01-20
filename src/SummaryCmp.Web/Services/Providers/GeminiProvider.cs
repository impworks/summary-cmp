using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SummaryCmp.Web.Configuration;
using SummaryCmp.Web.Data.Entities;

namespace SummaryCmp.Web.Services.Providers;

public class GeminiProvider : ISummaryProvider
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiProvider> _logger;

    public string ProviderKey => "Gemini";
    public bool IsConfigured => !string.IsNullOrEmpty(_options.ApiKey);

    public GeminiProvider(IHttpClientFactory httpClientFactory, IOptions<ProvidersOptions> options, ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _options = options.Value.Gemini;
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
                ErrorMessage = "Gemini API key not configured",
                DurationMs = sw.ElapsedMilliseconds
            };
        }

        Exception? lastException = null;
        string? lastErrorMessage = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogWarning("Gemini API retry attempt {Attempt} of {MaxRetries}", attempt, MaxRetries);
                await Task.Delay(RetryDelay, ct);
            }

            try
            {
                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = $"Summarize the following transcribed voice mail concisely in a single sentence, in the same language:\n\n{text}" }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={_options.ApiKey}";
                var response = await _httpClient.PostAsync(url, content, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API error (attempt {Attempt}): {StatusCode} - {Body}", attempt + 1, response.StatusCode, responseBody);
                    lastErrorMessage = $"API error: {response.StatusCode}";
                    continue;
                }

                var result = JsonDocument.Parse(responseBody);
                var summaryText = result.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                // Parse token usage from usageMetadata
                int? inputTokens = null;
                int? internalTokens = null;
                int? outputTokens = null;

                if (result.RootElement.TryGetProperty("usageMetadata", out var usageMetadata))
                {
                    if (usageMetadata.TryGetProperty("promptTokenCount", out var promptTokens))
                        inputTokens = promptTokens.GetInt32();

                    if (usageMetadata.TryGetProperty("candidatesTokenCount", out var candidatesTokens))
                        outputTokens = candidatesTokens.GetInt32();

                    if (usageMetadata.TryGetProperty("thoughtsTokenCount", out var thoughtsTokens))
                        internalTokens = thoughtsTokens.GetInt32();
                }

                sw.Stop();
                return new SummaryResponse
                {
                    IsSuccess = true,
                    SummaryText = summaryText,
                    DurationMs = sw.ElapsedMilliseconds,
                    InputTokens = inputTokens,
                    InternalTokens = internalTokens,
                    OutputTokens = outputTokens
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API (attempt {Attempt})", attempt + 1);
                lastException = ex;
                lastErrorMessage = ex.Message;
            }
        }

        sw.Stop();
        return new SummaryResponse
        {
            IsSuccess = false,
            ErrorMessage = lastErrorMessage ?? lastException?.Message ?? "Unknown error after retries",
            DurationMs = sw.ElapsedMilliseconds
        };
    }

    public decimal? CalculatePrice(SummaryResult result)
    {
        if (!result.InputTokens.HasValue)
            return null;

        // Gemini 3 Flash pricing:
        // Input: $0.50 per 1M tokens
        // Output (including thinking): $3.00 per 1M tokens
        const decimal inputPricePerMillion = 0.50m;
        const decimal outputPricePerMillion = 3.00m;

        var inputCost = result.InputTokens.Value * inputPricePerMillion / 1_000_000m;
        var outputTokens = (result.OutputTokens ?? 0) + (result.InternalTokens ?? 0);
        var outputCost = outputTokens * outputPricePerMillion / 1_000_000m;

        return inputCost + outputCost;
    }
}
