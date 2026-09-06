using System.Text.RegularExpressions;

namespace DiffToJsonLib.Reasoning;

public static partial class ModelIdNormalizer
{
    private static readonly HashSet<string> QuantizationSuffixes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "q4_k_m",
            "q4_ks",
            "q4_0",
            "q4_k_s",
            "q5_k_m",
            "q5_k_s",
            "q5_0",
            "q6_k",
            "q6_0",
            "q8_0",
            "q8_k_s",
            "mlx",
            "fp16",
            "bf16",
            "int4",
            "int8",
            "awq",
            "gptq",
            "exl2",
            "gguf",
        };

    public static string? ResolveModelId(string rawModelId, IReadOnlySet<string>? availableVariants)
    {
        if (string.IsNullOrWhiteSpace(rawModelId))
            return null;

        string modelId = rawModelId.Trim();

        string previous;
        do
        {
            previous = modelId;
            modelId = StripCloudSuffix(modelId);
            modelId = StripQuantizationSuffix(modelId);
        } while (modelId != previous);

        modelId = TryTranslateColonToDash(modelId, availableVariants);

        if (availableVariants is null || availableVariants.Count == 0)
            return modelId;

        if (availableVariants.Contains(modelId))
            return modelId;

        return null;
    }

    private static string StripCloudSuffix(string modelId)
    {
        if (modelId.Contains(":cloud:", StringComparison.OrdinalIgnoreCase))
            return modelId.Replace(":cloud:", ":", StringComparison.OrdinalIgnoreCase);

        if (modelId.EndsWith(":cloud", StringComparison.OrdinalIgnoreCase))
            return modelId[..^6];

        return modelId;
    }

    private static string StripQuantizationSuffix(string modelId)
    {
        int colonIndex = modelId.LastIndexOf(':');
        if (colonIndex <= 0)
            return modelId;

        string suffix = modelId[(colonIndex + 1)..];

        if (QuantizationSuffixes.Contains(suffix))
            return modelId[..colonIndex];

        return modelId;
    }

    private static string TryTranslateColonToDash(string modelId, IReadOnlySet<string>? availableVariants)
    {
        int colonIndex = modelId.LastIndexOf(':');
        if (colonIndex <= 0)
            return modelId;

        string sizeSuffix = modelId[(colonIndex + 1)..];
        if (!SizePattern().IsMatch(sizeSuffix))
            return modelId;

        string candidate = $"{modelId[..colonIndex]}-{sizeSuffix}";

        if (availableVariants is not null && availableVariants.Contains(candidate))
            return candidate;

        return modelId;
    }

    [GeneratedRegex(@"^\d+b$", RegexOptions.IgnoreCase)]
    private static partial Regex SizePattern();
}
