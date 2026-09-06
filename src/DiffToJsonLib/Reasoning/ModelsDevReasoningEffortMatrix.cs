using ModelsDotDevSharp;

namespace DiffToJsonLib.Reasoning;

public sealed class ModelsDevReasoningEffortMatrix : IReasoningEffortMatrix
{
    private static readonly HashSet<ReasoningEffort> AutoOnlySet = new()
    {
        ReasoningEffort.Auto
    };

    private static readonly HashSet<ReasoningEffort> ToggleSet = new()
    {
        ReasoningEffort.Auto, ReasoningEffort.On, ReasoningEffort.Off
    };

    private static readonly HashSet<ReasoningEffort> BudgetSet = new()
    {
        ReasoningEffort.Auto, ReasoningEffort.On, ReasoningEffort.Off,
        ReasoningEffort.Low
    };

    private static readonly HashSet<string> AutoExceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "minimax-m1",
        "minimax-m2",
        "minimax-m2.5",
        "minimax-m3",
        "deepseek-chat"
    };

    private readonly CapabilityCache? _cache;
    private readonly AIProviderInfo[]? _providers;

    public ModelsDevReasoningEffortMatrix(CapabilityCache cache)
    {
        _cache = cache;
    }

    internal ModelsDevReasoningEffortMatrix(AIProviderInfo[] providers)
    {
        _providers = providers;
    }

    public CacheStatus CacheStatus => _cache is null
        ? CacheStatus.Fresh
        : _cache.GetProviderInfosAsync().GetAwaiter().GetResult().Status;

    public IReadOnlySet<ReasoningEffort> GetSupportedReasoningValues(string model)
    {
        return GetSupportedReasoningValues(model, provider: "");
    }

    public IReadOnlySet<ReasoningEffort> GetSupportedReasoningValues(string model, string provider)
    {
        AIModelInfo? modelInfo = FindModel(model, provider);
        if (modelInfo is null)
            return AutoOnlySet;

        return DeriveSupportedValues(modelInfo);
    }

    public bool ProducesReasoningOnAuto(string model)
    {
        return ProducesReasoningOnAuto(model, provider: "");
    }

    public bool ProducesReasoningOnAuto(string model, string provider)
    {
        if (AutoExceptions.Contains(model))
            return false;

        AIModelInfo? modelInfo = FindModel(model, provider);
        return modelInfo?.SupportsReasoning == true;
    }

    public AIModelReasoningOptionType? GetReasoningType(string model, string provider)
    {
        AIModelInfo? modelInfo = FindModel(model, provider);
        if (modelInfo is null || modelInfo.ReasoningOptions is null || modelInfo.ReasoningOptions.Length == 0)
            return null;

        return modelInfo.ReasoningOptions[0].Type;
    }

    private AIModelInfo? FindModel(string model, string provider)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        string normalizedModel = ModelIdNormalizer.ResolveModelId(model, availableVariants: null) ?? model;

        AIProviderInfo[] providers = _providers ?? _cache!.GetProviderInfosAsync().GetAwaiter().GetResult().Providers ?? [];

        if (string.IsNullOrWhiteSpace(provider))
            return FindModelAcrossProviders(providers, normalizedModel);

        if (!ModelsDevProviderMap.TryGetProviderId(provider, out string modelsDevProviderId))
            return FindModelAcrossProviders(providers, normalizedModel);

        AIProviderInfo? providerInfo = Array.Find(providers, p =>
            string.Equals(p.Id, modelsDevProviderId, StringComparison.OrdinalIgnoreCase));

        return FindModelInProvider(providerInfo, normalizedModel);
    }

    private static AIModelInfo? FindModelAcrossProviders(AIProviderInfo[] providers, string normalizedModel)
    {
        foreach (AIProviderInfo providerInfo in providers)
        {
            AIModelInfo? found = FindModelInProvider(providerInfo, normalizedModel);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static AIModelInfo? FindModelInProvider(AIProviderInfo? providerInfo, string normalizedModel)
    {
        if (providerInfo?.Models is null)
            return null;

        foreach (AIModelInfo modelInfo in providerInfo.Models)
        {
            if (string.Equals(modelInfo.Id, normalizedModel, StringComparison.OrdinalIgnoreCase))
                return modelInfo;
        }

        return null;
    }

    private static IReadOnlySet<ReasoningEffort> DeriveSupportedValues(AIModelInfo modelInfo)
    {
        if (modelInfo.ReasoningOptions is null || modelInfo.ReasoningOptions.Length == 0)
            return AutoOnlySet;

        AIModelReasoningOption option = modelInfo.ReasoningOptions[0];

        return option.Type switch
        {
            AIModelReasoningOptionType.Effort => MapEffortValues(option.Values),
            AIModelReasoningOptionType.Toggle => ToggleSet,
            AIModelReasoningOptionType.BudgetTokens => BudgetSet,
            _ => AutoOnlySet
        };
    }

    private static IReadOnlySet<ReasoningEffort> MapEffortValues(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
            return AutoOnlySet;

        HashSet<ReasoningEffort> result = new() { ReasoningEffort.Auto };

        foreach (string value in values)
        {
            ReasoningEffort? effort = ParseEffortValue(value);
            if (effort.HasValue)
                result.Add(effort.Value);
        }

        return result;
    }

    private static ReasoningEffort? ParseEffortValue(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "on" => ReasoningEffort.On,
            "off" => ReasoningEffort.Off,
            "low" => ReasoningEffort.Low,
            "medium" => ReasoningEffort.Medium,
            "high" => ReasoningEffort.High,
            "xhigh" => ReasoningEffort.XHigh,
            "max" => ReasoningEffort.Max,
            _ => null
        };
    }
}
