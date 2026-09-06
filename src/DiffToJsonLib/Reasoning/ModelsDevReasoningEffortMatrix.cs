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

    // Intentional per D012/T005: BudgetTokens Type carries no values list, so the
    // catalog-driven set is {Auto,On,Off,Low}. BuildBudgetChatOptions retains
    // Medium/High fractions for forward compatibility, but validation currently
    // restricts pure BudgetTokens models to this set.
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

    /// <summary>
    /// Synchronous status probe. Blocks on cache I/O; prefer
    /// <see cref="GetCacheStatusAsync"/> in async paths.
    /// </summary>
    public CacheStatus CacheStatus => _cache is null
        ? CacheStatus.Fresh
        : _cache.GetProviderInfosAsync().GetAwaiter().GetResult().Status;

    public Task<CacheStatus> GetCacheStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is null)
        {
            return Task.FromResult(CacheStatus.Fresh);
        }

        return GetCacheStatusCoreAsync(cancellationToken);
    }

    private async Task<CacheStatus> GetCacheStatusCoreAsync(CancellationToken cancellationToken)
    {
        CapabilityCacheResult result = await _cache!.GetProviderInfosAsync(cancellationToken).ConfigureAwait(false);
        return result.Status;
    }

    public Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is null || _providers is not null)
        {
            return Task.CompletedTask;
        }

        return _cache.GetProviderInfosAsync(cancellationToken);
    }

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
        if (string.IsNullOrWhiteSpace(model))
            return false;

        string normalizedForExceptionCheck = ModelIdNormalizer.ResolveModelId(model, availableVariants: null)?.Trim()
            ?? model.Trim();
        if (AutoExceptions.Contains(normalizedForExceptionCheck))
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

        AIProviderInfo[] providers = _providers ?? _cache!.GetProviderInfosAsync().GetAwaiter().GetResult().Providers ?? [];

        // Variant-aware per D005: translate size suffixes (qwen3.5:9b -> qwen3.5-9b)
        // only when the dash variant exists in the catalog; otherwise fall back
        // gracefully to the stripped id (never fabricated).
        HashSet<string> variants = new(StringComparer.OrdinalIgnoreCase);
        foreach (AIProviderInfo catalogProvider in providers)
        {
            if (catalogProvider.Models is null)
                continue;

            foreach (AIModelInfo candidate in catalogProvider.Models)
            {
                if (!string.IsNullOrWhiteSpace(candidate.Id))
                    variants.Add(candidate.Id);
            }
        }

        string normalizedModel = ModelIdNormalizer.ResolveModelId(model, variants)
            ?? ModelIdNormalizer.ResolveModelId(model, availableVariants: null)
            ?? model.Trim();

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
