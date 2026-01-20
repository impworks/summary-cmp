using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SummaryCmp.Web.Configuration;
using SummaryCmp.Web.Data.Entities;

namespace SummaryCmp.Web.Services.Providers;

public class AzureOpenAIProvider : ISummaryProvider
{
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AzureOpenAIProvider> _logger;

    public string ProviderKey => "AzureOpenAI";
    public bool IsConfigured => !string.IsNullOrEmpty(_options.ApiKey) && !string.IsNullOrEmpty(_options.Endpoint);

    public AzureOpenAIProvider(IHttpClientFactory httpClientFactory, IOptions<ProvidersOptions> options, ILogger<AzureOpenAIProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _options = options.Value.AzureOpenAI;
        _logger = logger;
    }

    public async Task<SummaryResponse> SummarizeAsync(string text, string modelId, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrEmpty(_options.ApiKey) || string.IsNullOrEmpty(_options.Endpoint))
        {
            return new SummaryResponse
            {
                IsSuccess = false,
                ErrorMessage = "Azure OpenAI API key or endpoint not configured",
                DurationMs = sw.ElapsedMilliseconds
            };
        }

        try
        {
            var request = new
            {
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant that summarizes transcribed voice mails." },
                    new { role = "user", content = $"Please summarize the following voicemail contents in a single sentence, in the same language:\n```\n{text}\n```" }
                },
                max_completion_tokens = 1024
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", _options.ApiKey);

            // Azure OpenAI endpoint format: https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions?api-version=2024-02-15-preview
            var endpoint = _options.Endpoint.TrimEnd('/');
            var url = $"{endpoint}/openai/deployments/{modelId}/chat/completions?api-version=2025-01-01-preview";

            var response = await _httpClient.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Azure OpenAI API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                var errorMessage = ParseErrorMessage(responseBody, response.StatusCode);
                return new SummaryResponse
                {
                    IsSuccess = false,
                    ErrorMessage = errorMessage,
                    DurationMs = sw.ElapsedMilliseconds
                };
            }

            var result = JsonDocument.Parse(responseBody);
            var summaryText = result.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            // Parse token usage
            int? inputTokens = null;
            int? internalTokens = null;
            int? outputTokens = null;

            if (result.RootElement.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var promptTokens))
                    inputTokens = promptTokens.GetInt32();

                if (usage.TryGetProperty("completion_tokens", out var completionTokens))
                {
                    var totalCompletion = completionTokens.GetInt32();

                    // Check for reasoning tokens in completion_tokens_details
                    if (usage.TryGetProperty("completion_tokens_details", out var details) &&
                        details.TryGetProperty("reasoning_tokens", out var reasoningTokens))
                    {
                        internalTokens = reasoningTokens.GetInt32();
                        outputTokens = totalCompletion - internalTokens.Value;
                    }
                    else
                    {
                        outputTokens = totalCompletion;
                    }
                }
            }

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
            sw.Stop();
            _logger.LogError(ex, "Error calling Azure OpenAI API");
            return new SummaryResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    private string ParseErrorMessage(string responseBody, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            var errorDoc = JsonDocument.Parse(responseBody);
            if (errorDoc.RootElement.TryGetProperty("error", out var errorElement))
            {
                // Check for content filter error
                if (errorElement.TryGetProperty("code", out var codeElement) &&
                    codeElement.GetString() == "content_filter")
                {
                    // Try to find which filter was triggered
                    if (errorElement.TryGetProperty("innererror", out var innerError) &&
                        innerError.TryGetProperty("content_filter_result", out var filterResult))
                    {
                        var triggeredFilters = new List<string>();

                        foreach (var filter in filterResult.EnumerateObject())
                        {
                            if (filter.Value.TryGetProperty("filtered", out var filtered) &&
                                filtered.GetBoolean())
                            {
                                triggeredFilters.Add(filter.Name);
                            }
                        }

                        if (triggeredFilters.Count > 0)
                        {
                            return $"Content filtered: {string.Join(", ", triggeredFilters)}";
                        }
                    }

                    return "Content filtered by policy";
                }

                // Return generic error message if available
                if (errorElement.TryGetProperty("message", out var messageElement))
                {
                    var message = messageElement.GetString();
                    if (!string.IsNullOrEmpty(message) && message.Length > 100)
                    {
                        message = message.Substring(0, 100) + "...";
                    }
                    return message ?? $"API error: {statusCode}";
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return $"API error: {statusCode}";
    }

    public decimal? CalculatePrice(SummaryResult result)
    {
        if (!result.InputTokens.HasValue)
            return null;

        var (inputPricePerMillion, outputPricePerMillion) = GetModelPricing(result.ProviderModel?.ModelId);

        var inputCost = result.InputTokens.Value * inputPricePerMillion / 1_000_000m;
        var outputTokens = (result.OutputTokens ?? 0) + (result.InternalTokens ?? 0);
        var outputCost = outputTokens * outputPricePerMillion / 1_000_000m;

        return inputCost + outputCost;
    }

    private static (decimal input, decimal output) GetModelPricing(string? modelId)
    {
        return modelId switch
        {
            "gpt-5-nano" => (0.05m, 0.40m),           // $0.05/1M input, $0.40/1M output
            "cohere-command-a" => (2.50m, 10.00m),    // $2.50/1M input, $10/1M output
            _ => (0.05m, 0.40m)                        // Default to GPT-5 Nano pricing
        };
    }
}
