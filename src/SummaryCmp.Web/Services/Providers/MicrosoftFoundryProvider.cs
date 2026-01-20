using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SummaryCmp.Web.Configuration;
using SummaryCmp.Web.Data.Entities;

namespace SummaryCmp.Web.Services.Providers;

public class MicrosoftFoundryProvider : ISummaryProvider
{
    private const string ApiVersion = "2025-05-15-preview";
    private const int MaxPollAttempts = 60;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly HttpClient _httpClient;
    private readonly MicrosoftFoundryOptions _options;
    private readonly ILogger<MicrosoftFoundryProvider> _logger;

    public string ProviderKey => "MicrosoftFoundry";
    public bool IsConfigured => !string.IsNullOrEmpty(_options.ApiKey) && !string.IsNullOrEmpty(_options.Endpoint);

    public MicrosoftFoundryProvider(IHttpClientFactory httpClientFactory, IOptions<ProvidersOptions> options, ILogger<MicrosoftFoundryProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _options = options.Value.MicrosoftFoundry;
        _logger = logger;
    }

    public async Task<SummaryResponse> SummarizeAsync(string text, string modelId, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrEmpty(_options.Endpoint) || string.IsNullOrEmpty(_options.ApiKey))
        {
            return new SummaryResponse
            {
                IsSuccess = false,
                ErrorMessage = "Microsoft Foundry endpoint or API key not configured",
                DurationMs = sw.ElapsedMilliseconds
            };
        }

        try
        {
            // Submit the analysis job
            var operationLocation = await SubmitJobAsync(text, ct);
            if (string.IsNullOrEmpty(operationLocation))
            {
                sw.Stop();
                return new SummaryResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Failed to submit summarization job - no operation location returned",
                    DurationMs = sw.ElapsedMilliseconds
                };
            }

            // Poll for results
            var summary = await PollForResultsAsync(operationLocation, ct);
            sw.Stop();

            return new SummaryResponse
            {
                IsSuccess = true,
                SummaryText = summary,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error calling Microsoft Foundry Text Analytics API");
            return new SummaryResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task<string?> SubmitJobAsync(string text, CancellationToken ct)
    {
        var endpoint = _options.Endpoint.TrimEnd('/');
        var url = $"{endpoint}/language/analyze-text/jobs?api-version={ApiVersion}";

        var request = new
        {
            analysisInput = new
            {
                documents = new[]
                {
                    new { id = "1", language = "en", text }
                }
            },
            tasks = new[]
            {
                new
                {
                    kind = "AbstractiveSummarization",
                    parameters = new
                    {
                        summaryLength = "oneSentence"
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Content = content;
        requestMessage.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);

        var response = await _httpClient.SendAsync(requestMessage, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Microsoft Foundry submit job error: {StatusCode} - {Body}", response.StatusCode, errorBody);
            throw new Exception($"Failed to submit job: {response.StatusCode} - {errorBody}");
        }

        // Get the operation-location header for polling
        if (response.Headers.TryGetValues("operation-location", out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private async Task<string> PollForResultsAsync(string operationLocation, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxPollAttempts; attempt++)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            requestMessage.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);

            var response = await _httpClient.SendAsync(requestMessage, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Microsoft Foundry poll error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                throw new Exception($"Failed to poll for results: {response.StatusCode}");
            }

            var result = JsonDocument.Parse(responseBody);
            var status = result.RootElement.GetProperty("status").GetString();

            if (status == "succeeded")
                return ExtractSummaryFromResult(result);

            if (status == "failed" || status == "cancelled")
            {
                var errors = result.RootElement.TryGetProperty("errors", out var errorsElement)
                    ? errorsElement.ToString()
                    : "Unknown error";
                throw new Exception($"Job {status}: {errors}");
            }

            // Still running, wait and retry
            await Task.Delay(PollInterval, ct);
        }

        throw new Exception($"Job did not complete within {MaxPollAttempts} polling attempts");
    }

    private string ExtractSummaryFromResult(JsonDocument result)
    {
        var summaryBuilder = new StringBuilder();

        var tasks = result.RootElement.GetProperty("tasks");
        var items = tasks.GetProperty("items");

        foreach (var item in items.EnumerateArray())
        {
            var itemResults = item.GetProperty("results");
            var documents = itemResults.GetProperty("documents");

            foreach (var doc in documents.EnumerateArray())
            {
                var sentences = doc.GetProperty("summaries");

                foreach (var sentence in sentences.EnumerateArray())
                {
                    if (summaryBuilder.Length > 0)
                        summaryBuilder.Append(" ");
                    summaryBuilder.Append(sentence.GetProperty("text").GetString());
                }
            }
        }

        return summaryBuilder.ToString();
    }

    public decimal? CalculatePrice(SummaryResult result)
    {
        // Microsoft Foundry pricing: $2 per 1000 records
        // 1 record = up to 1000 characters
        var inputText = result.Session?.InputText;
        if (string.IsNullOrEmpty(inputText))
            return null;

        var records = (int)Math.Ceiling(inputText.Length / 1000.0);
        return records * 2.00m / 1000m;
    }
}
