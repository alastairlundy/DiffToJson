/*
    Copyright 2026 Alastair Lundy

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */

using System.Collections.Frozen;
using ModelsDotDevSharp;

namespace DiffToJsonLib.Reasoning;

public sealed class ModelsDevReasoningEffortMatrix : IReasoningEffortMatrix
{
    private static readonly HashSet<ReasoningEffort> AutoOnlySet =
    [
        ReasoningEffort.Auto
    ];

    private static readonly HashSet<ReasoningEffort> ToggleSet =
    [
        ReasoningEffort.Auto, ReasoningEffort.On, ReasoningEffort.Off,
    ];

    // Intentional: BudgetTokens Type carries no values list, so the
    // catalog-driven set is {Auto,On,Off,Low}. BuildBudgetChatOptions retains
    // Medium/High fractions for forward compatibility, but validation currently
    // restricts pure BudgetTokens models to this set.
    private static readonly HashSet<ReasoningEffort> BudgetSet =
    [
        ReasoningEffort.Auto, ReasoningEffort.On, ReasoningEffort.Off,
        ReasoningEffort.Low
    ];

    private static readonly HashSet<string> AutoExceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "minimax-m1",
        "minimax-m2",
        "minimax-m2.5",
        "minimax-m3",
        "deepseek-chat"
    };

    // Allocation-free case-insensitive effort-name lookup; avoids the per-call
    // ToLowerInvariant switch expression in ParseEffortValue.
    private static readonly FrozenDictionary<string, ReasoningEffort> EffortNameMap =
        new Dictionary<string, ReasoningEffort>(StringComparer.OrdinalIgnoreCase)
        {
            ["on"] = ReasoningEffort.On,
            ["off"] = ReasoningEffort.Off,
            ["low"] = ReasoningEffort.Low,
            ["medium"] = ReasoningEffort.Medium,
            ["high"] = ReasoningEffort.High,
            ["xhigh"] = ReasoningEffort.XHigh,
            ["max"] = ReasoningEffort.Max
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> EmptyVariants = new(StringComparer.OrdinalIgnoreCase);

    private readonly CapabilityCache? _cache;
    private readonly AIProviderInfo[]? _providers;

    // Catalog observed by the in-memory constructor or by the most recent async cache
    // probe (GetCacheStatusAsync / WarmUpAsync / GetSupportedReasoningValuesAsync).
    // Synchronous query paths read only this snapshot and never block on I/O; before the
    // first probe they see the empty catalog and fall back to auto-only / null model.
    private volatile CatalogSnapshot? _loaded;

    public ModelsDevReasoningEffortMatrix(CapabilityCache cache)
    {
        _cache = cache;
    }

    internal ModelsDevReasoningEffortMatrix(AIProviderInfo[] providers)
    {
        _providers = providers ?? [];
        _loaded = new CatalogSnapshot(_providers, CacheStatus.Fresh);
    }

    /// <summary>
    /// Last observed cache status, read from memory without I/O. Until an async probe
    /// runs it reports <see cref="CacheStatus.Fresh"/>; prefer
    /// <see cref="GetCacheStatusAsync"/> to probe the backing source (which can hit
    /// the network).
    /// </summary>
    public CacheStatus CacheStatus => _cache is null
        ? CacheStatus.Fresh
        : _loaded?.Status ?? CacheStatus.Fresh;

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
        if (_cache is null)
            return CacheStatus.Unavailable;
        
        CapabilityCacheResult result = await _cache.GetProviderInfosAsync(cancellationToken).ConfigureAwait(false);
        CaptureCatalog(result);
        return result.Status;
    }

    /// <summary>
    /// Stores the probed catalog in memory so synchronous queries read real data
    /// without ever blocking on cache I/O.
    /// </summary>
    private void CaptureCatalog(CapabilityCacheResult result)
    {
        _loaded = new CatalogSnapshot(result.Providers ?? [], result.Status);
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is null || _providers is not null)
        {
            return;
        }

        await GetCacheStatusCoreAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Async variant of <see cref="GetSupportedReasoningValues(string)"/>: loads the
    /// capability catalog without blocking, then answers from memory.
    /// </summary>
    public Task<IReadOnlySet<ReasoningEffort>> GetSupportedReasoningValuesAsync(
        string model, CancellationToken cancellationToken = default)
    {
        return GetSupportedReasoningValuesAsync(model, provider: "", cancellationToken);
    }

    /// <summary>
    /// Async variant of <see cref="GetSupportedReasoningValues(string, string)"/>: loads the
    /// capability catalog without blocking, then answers from memory.
    /// </summary>
    public async Task<IReadOnlySet<ReasoningEffort>> GetSupportedReasoningValuesAsync(
        string model, string provider, CancellationToken cancellationToken = default)
    {
        await EnsureCatalogLoadedAsync(cancellationToken).ConfigureAwait(false);
        return GetSupportedReasoningValues(model, provider);
    }

    private Task EnsureCatalogLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cache is null || _loaded is not null)
        {
            return Task.CompletedTask;
        }

        return GetCacheStatusCoreAsync(cancellationToken);
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

        // In-memory only: never blocks on cache I/O. Async callers warm the catalog via
        // GetCacheStatusAsync / WarmUpAsync / GetSupportedReasoningValuesAsync first; the
        // internal-constructor path always has a snapshot (set in the constructor).
        CatalogSnapshot? loaded = _loaded;
        AIProviderInfo[] providers = _providers ?? loaded?.Providers ?? [];

        // Variant-aware: translate size suffixes (qwen3.5:9b -> qwen3.5-9b)
        // only when the dash variant exists in the catalogue; otherwise fall back
        // gracefully to the stripped id (never fabricated). The variant index is built
        // once per loaded catalog (memoized on the snapshot) and reused across calls.
        HashSet<string> variants = loaded?.GetVariants() ?? EmptyVariants;

        string normalizedModel = ModelIdNormalizer.ResolveModelId(model, variants)
                                 ?? ModelIdNormalizer.ResolveModelId(model, availableVariants: null)
                                 ?? model.Trim();

        if (string.IsNullOrWhiteSpace(provider))
            return FindModelAcrossProviders(providers, normalizedModel);

        if (!ModelsDevProviderMap.TryGetProviderId(provider, out string? modelsDevProviderId))
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

        HashSet<ReasoningEffort> result = [ReasoningEffort.Auto];

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
        return EffortNameMap.TryGetValue(value, out ReasoningEffort effort) ? effort : null;
    }

    /// <summary>
    /// Immutable view of one loaded provider catalog. The model-variant index is built
    /// at most once per snapshot (i.e. per loaded catalog) and reused across queries;
    /// concurrent first callers may build an equivalent set twice, which is benign.
    /// </summary>
    private sealed class CatalogSnapshot(AIProviderInfo[] providers, CacheStatus status)
    {
        private volatile HashSet<string>? _variants;

        public AIProviderInfo[] Providers { get; } = providers;

        public CacheStatus Status { get; } = status;

        public HashSet<string> GetVariants()
        {
            HashSet<string>? variants = _variants;
            if (variants is not null)
                return variants;

            HashSet<string> built = BuildVariants(Providers);
            _variants = built;
            return built;
        }

        private static HashSet<string> BuildVariants(AIProviderInfo[] providers)
        {
            HashSet<string> variants = new(StringComparer.OrdinalIgnoreCase);
            foreach (AIProviderInfo catalogProvider in providers)
            {
                if (catalogProvider.Models.Length == 0)
                    continue;

                foreach (AIModelInfo candidate in catalogProvider.Models)
                {
                    if (!string.IsNullOrWhiteSpace(candidate.Id))
                        variants.Add(candidate.Id);
                }
            }

            return variants;
        }
    }
}
