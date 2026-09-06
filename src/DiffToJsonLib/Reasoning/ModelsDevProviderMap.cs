namespace DiffToJsonLib.Reasoning;

public static class ModelsDevProviderMap
{
    private static readonly IReadOnlyDictionary<string, string> ProviderMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = "openai",
            ["anthropic"] = "anthropic",
            ["openrouter"] = "openrouter",
            ["ollama"] = "ollama-cloud",
            ["ollama-cloud"] = "ollama-cloud",
        };

    public static bool TryGetProviderId(string cliProviderName, out string modelsDevProviderId)
    {
        if (string.IsNullOrWhiteSpace(cliProviderName))
        {
            modelsDevProviderId = string.Empty;
            return false;
        }

        return ProviderMap.TryGetValue(cliProviderName, out modelsDevProviderId!);
    }
}
