using Microsoft.EntityFrameworkCore;
using SummaryCmp.Web.Data.Entities;

namespace SummaryCmp.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        if (await db.ProviderModels.AnyAsync())
            return;

        var providerModels = new List<ProviderModel>
        {
            // Claude models
            new() { ProviderKey = "Claude", ModelId = "claude-haiku-4-5", DisplayName = "Claude Haiku 4.5" },
            new() { ProviderKey = "Claude", ModelId = "claude-sonnet-4-5", DisplayName = "Claude Sonnet 4.5" },
            new() { ProviderKey = "Claude", ModelId = "claude-opus-4-5", DisplayName = "Claude Opus 4.5" },

            // Azure OpenAI models (deployment names)
            new() { ProviderKey = "AzureOpenAI", ModelId = "gpt-5-nano", DisplayName = "GPT-5 Nano" },
            new() { ProviderKey = "AzureOpenAI", ModelId = "cohere-command-a", DisplayName = "Cohere Command A" },

            // Gemini models
            new() { ProviderKey = "Gemini", ModelId = "gemini-3-flash-preview", DisplayName = "Gemini 3 Flash" },

            // Microsoft Foundry
            new() { ProviderKey = "MicrosoftFoundry", ModelId = "default", DisplayName = "Microsoft Foundry" }
        };

        db.ProviderModels.AddRange(providerModels);
        await db.SaveChangesAsync();
    }
}
