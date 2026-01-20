namespace SummaryCmp.Web.Configuration;

public class ProvidersOptions
{
    public ClaudeOptions Claude { get; set; } = new();
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();
    public GeminiOptions Gemini { get; set; } = new();
    public MicrosoftFoundryOptions MicrosoftFoundry { get; set; } = new();
}

public class ClaudeOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
}

public class MicrosoftFoundryOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class AuthOptions
{
    public string Password { get; set; } = string.Empty;
}
