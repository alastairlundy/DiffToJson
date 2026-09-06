namespace DiffToJsonLib.Reasoning;

public static class ModelsDevProviderMap
{
    private static readonly IReadOnlyDictionary<string, string> ProviderMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = "openai",
            ["openai-compatible"] = "openai",
            ["anthropic"] = "anthropic",
            ["anthropic-compatible"] = "anthropic",
            ["openrouter"] = "openrouter",
            ["ollama"] = "ollama-cloud",
            ["ollama-cloud"] = "ollama-cloud",
            ["minimax"] = "minimax",
            ["deepseek"] = "deepseek",
        };

    public static bool TryGetProviderId(string cliProviderName, out string modelsDevProviderId)
    {
        if (string.IsNullOrWhiteSpace(cliProviderName))
        {
            modelsDevProviderId = string.Empty;
            return false;
        }

        return ProviderMap.TryGetValue(cliProviderName.Trim(), out modelsDevProviderId!);
    }

    /// <summary>
    /// Returns the canonical models.dev provider id for dispatch, falling back to
    /// the trimmed lower-case CLI value when unmapped.
    /// </summary>
    public static string Normalize(string cliProviderName)
    {
        if (string.IsNullOrWhiteSpace(cliProviderName))
        {
            return string.Empty;
        }

        string trimmed = cliProviderName.Trim();
        return ProviderMap.TryGetValue(trimmed, out string? canonical)
            ? canonical
            : trimmed.ToLowerInvariant();
    }
}
