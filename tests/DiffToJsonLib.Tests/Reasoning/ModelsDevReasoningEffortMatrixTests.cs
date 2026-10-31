using System.IO.Abstractions.TestingHelpers;
using DiffToJsonLib.Reasoning;
using ModelsDotDevSharp;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonLib.Tests.Reasoning;

public class ModelsDevReasoningEffortMatrixTests
{
    private static readonly string TestCacheDir = Path.Combine(
        Path.GetTempPath(),
        "difftojson-tests",
        Guid.NewGuid().ToString("N"));

    private static string CacheFilePath => Path.Combine(TestCacheDir, "api.json");

    private static string TimestampFilePath => Path.Combine(TestCacheDir, "api-fetched.txt");

    private static byte[] CreateMinimalProviderJson()
    {
        string json = """
        {
          "test-provider": {
            "id": "test-provider",
            "env": ["TEST_API_KEY"],
            "npm": "@ai-sdk/openai-compatible",
            "api": "https://api.test.com/v1",
            "name": "Test Provider",
            "doc": "https://docs.test.com",
            "models": {}
          }
        }
        """;
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    private static readonly byte[] FixtureBytes = CreateMinimalProviderJson();

    private sealed class ThrowingFetcher : IFetcher
    {
        public Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("Network unavailable");
        }
    }

    private static ModelsDevReasoningEffortMatrix CreateMatrix(AIProviderInfo[] providers)
    {
        return new ModelsDevReasoningEffortMatrix(providers);
    }

    private static AIProviderInfo CreateProvider(string id, AIModelInfo[] models)
    {
        return new AIProviderInfo
        {
            Id = id,
            Models = models
        };
    }

    private static AIModelInfo CreateModel(string id, bool supportsReasoning, AIModelReasoningOption[]? reasoningOptions)
    {
        return new AIModelInfo
        {
            Id = id,
            SupportsReasoning = supportsReasoning,
            ReasoningOptions = reasoningOptions
        };
    }

    private static AIModelReasoningOption CreateReasoningOption(AIModelReasoningOptionType type, string[]? values = null)
    {
        return new AIModelReasoningOption
        {
            Type = type,
            Values = values?.ToList()
        };
    }

    // --- Type Effort ---

    [Test]
    public async Task EffortType_MapsValuesToReasoningEffort()
    {
        AIModelInfo model = CreateModel("gpt-4o", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Effort,
                ["low", "medium", "high", "xhigh", "max"])
        ]);
        AIProviderInfo provider = CreateProvider("openai", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("gpt-4o", "openai");

        await Assert.That(result.Count).IsEqualTo(6);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Low)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Medium)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.High)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.XHigh)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Max)).IsTrue();
    }

    [Test]
    public async Task EffortType_WithLimitedValues()
    {
        AIModelInfo model = CreateModel("claude-sonnet-4-5", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Effort,
                ["low", "medium", "high"])
        ]);
        AIProviderInfo provider = CreateProvider("anthropic", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("claude-sonnet-4-5", "anthropic");

        await Assert.That(result.Count).IsEqualTo(4);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Low)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Medium)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.High)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.XHigh)).IsFalse();
        await Assert.That(result.Contains(ReasoningEffort.Max)).IsFalse();
    }

    // --- Type Toggle ---

    [Test]
    public async Task ToggleType_YieldsOnOffSet()
    {
        AIModelInfo model = CreateModel("claude-sonnet-5", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        ]);
        AIProviderInfo provider = CreateProvider("anthropic", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("claude-sonnet-5", "anthropic");

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.On)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Off)).IsTrue();
    }

    // --- Type BudgetTokens ---

    [Test]
    public async Task BudgetTokensType_YieldsBudgetSet()
    {
        AIModelInfo model = CreateModel("claude-haiku-4-5", true, [
            CreateReasoningOption(AIModelReasoningOptionType.BudgetTokens)
        ]);
        AIProviderInfo provider = CreateProvider("anthropic", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("claude-haiku-4-5", "anthropic");

        await Assert.That(result.Count).IsEqualTo(4);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.On)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Off)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Low)).IsTrue();
    }

    // --- Missing model / missing options ---

    [Test]
    public async Task MissingModel_ReturnsAutoOnly()
    {
        AIProviderInfo provider = CreateProvider("openai", []);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("nonexistent-model", "openai");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
    }

    [Test]
    public async Task ModelWithNoReasoningOptions_ReturnsAutoOnly()
    {
        AIModelInfo model = CreateModel("some-model", false, null);
        AIProviderInfo provider = CreateProvider("openai", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("some-model", "openai");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
    }

    // --- ProducesReasoningOnAuto ---

    [Test]
    public async Task SupportsReasoningTrue_ProducesReasoningOnAuto()
    {
        AIModelInfo model = CreateModel("gpt-4o", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Effort, ["low", "high"])
        ]);
        AIProviderInfo provider = CreateProvider("openai", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        await Assert.That(matrix.ProducesReasoningOnAuto("gpt-4o", "openai")).IsTrue();
    }

    [Test]
    public async Task SupportsReasoningFalse_DoesNotProduceReasoningOnAuto()
    {
        AIModelInfo model = CreateModel("deepseek-chat", false, [
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        ]);
        AIProviderInfo provider = CreateProvider("deepseek", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        await Assert.That(matrix.ProducesReasoningOnAuto("deepseek-chat", "deepseek")).IsFalse();
    }

    // --- Exceptions table ---

    [Test]
    public async Task MiniMaxM1_ExceptionReturnsFalse()
    {
        AIModelInfo model = CreateModel("minimax-m1", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        ]);
        AIProviderInfo provider = CreateProvider("minimax", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        await Assert.That(matrix.ProducesReasoningOnAuto("minimax-m1", "minimax")).IsFalse();
    }

    [Test]
    public async Task MiniMaxM3_ExceptionReturnsFalse()
    {
        AIModelInfo model = CreateModel("minimax-m3", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        ]);
        AIProviderInfo provider = CreateProvider("minimax", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        await Assert.That(matrix.ProducesReasoningOnAuto("minimax-m3", "minimax")).IsFalse();
    }

    [Test]
    public async Task DeepseekChat_ExceptionReturnsFalse()
    {
        AIModelInfo model = CreateModel("deepseek-chat", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        ]);
        AIProviderInfo provider = CreateProvider("deepseek", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        await Assert.That(matrix.ProducesReasoningOnAuto("deepseek-chat", "deepseek")).IsFalse();
    }

    // --- GetReasoningType ---

    [Test]
    public async Task GetReasoningType_ReturnsEffortForEffortModel()
    {
        AIModelInfo model = CreateModel("gpt-4o", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Effort, ["low", "high"])
        ]);
        AIProviderInfo provider = CreateProvider("openai", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        AIModelReasoningOptionType? type = matrix.GetReasoningType("gpt-4o", "openai");

        await Assert.That(type).IsEqualTo(AIModelReasoningOptionType.Effort);
    }

    [Test]
    public async Task GetReasoningType_ReturnsNullForMissingModel()
    {
        AIProviderInfo provider = CreateProvider("openai", []);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        AIModelReasoningOptionType? type = matrix.GetReasoningType("nonexistent", "openai");

        await Assert.That(type).IsNull();
    }

    // --- Provider mapping ---

    [Test]
    public async Task ProviderMapping_OllamaResolvesToOllamaCloud()
    {
        AIModelInfo model = CreateModel("qwen3", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        ]);
        AIProviderInfo provider = CreateProvider("ollama-cloud", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("qwen3", "ollama");

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result.Contains(ReasoningEffort.On)).IsTrue();
        await Assert.That(result.Contains(ReasoningEffort.Off)).IsTrue();
    }

    // --- Fallback ---

    [Test]
    public async Task UnknownProvider_FallsBackToAutoOnly()
    {
        AIProviderInfo provider = CreateProvider("openai", []);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("gpt-4o", "unknown-provider");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
    }

    // --- Empty cache ---

    [Test]
    public async Task EmptyCache_ReturnsAutoOnly()
    {
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([]);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("gpt-4o", "openai");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
    }

    // --- Async query paths (no sync-over-async) ---

    [Test]
    public async Task GetSupportedReasoningValuesAsync_MatchesSyncForInMemoryCatalog()
    {
        AIModelInfo model = CreateModel("gpt-4o", true, [
            CreateReasoningOption(AIModelReasoningOptionType.Effort, ["low", "medium", "high"])
        ]);
        AIProviderInfo provider = CreateProvider("openai", [model]);
        ModelsDevReasoningEffortMatrix matrix = CreateMatrix([provider]);

        IReadOnlySet<ReasoningEffort> asyncResult =
            await matrix.GetSupportedReasoningValuesAsync("gpt-4o", "openai");
        IReadOnlySet<ReasoningEffort> syncResult = matrix.GetSupportedReasoningValues("gpt-4o", "openai");

        await Assert.That(asyncResult.Count).IsEqualTo(4);
        await Assert.That(asyncResult.SetEquals(syncResult)).IsTrue();
    }

    [Test]
    public async Task CacheBackedMatrix_SyncQueriesBeforeProbe_FallBackToAutoOnlyWithoutBlocking()
    {
        // No cache file and a fetcher that always fails: the sync paths must answer
        // from memory (empty catalog) instead of blocking on cache I/O.
        MockFileSystem fileSystem = new();
        CapabilityCache cache = new(fileSystem, TestCacheDir, new ThrowingFetcher());
        ModelsDevReasoningEffortMatrix matrix = new(cache);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("gpt-4o", "openai");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
        await Assert.That(matrix.GetReasoningType("gpt-4o", "openai")).IsNull();
    }

    [Test]
    public async Task CacheBackedMatrix_AsyncProbe_CapturesStatusForSyncCacheStatus()
    {
        MockFileSystem fileSystem = new();
        fileSystem.Directory.CreateDirectory(TestCacheDir);
        fileSystem.File.WriteAllBytes(CacheFilePath, FixtureBytes);
        fileSystem.File.WriteAllText(TimestampFilePath, DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));
        CapabilityCache cache = new(fileSystem, TestCacheDir, new ThrowingFetcher());
        ModelsDevReasoningEffortMatrix matrix = new(cache);

        CacheStatus probed = await matrix.GetCacheStatusAsync();

        // The probe fails to refresh but falls back to the stale on-disk copy; the
        // synchronous property then reports the captured status without re-running I/O.
        await Assert.That(probed).IsEqualTo(CacheStatus.Stale);
        await Assert.That(matrix.CacheStatus).IsEqualTo(CacheStatus.Stale);
    }

    [Test]
    public async Task CacheBackedMatrix_GetSupportedReasoningValuesAsync_LoadsCatalogFromCache()
    {
        MockFileSystem fileSystem = new();
        fileSystem.Directory.CreateDirectory(TestCacheDir);
        fileSystem.File.WriteAllBytes(CacheFilePath, FixtureBytes);
        fileSystem.File.WriteAllText(TimestampFilePath, DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));
        CapabilityCache cache = new(fileSystem, TestCacheDir, new ThrowingFetcher());
        ModelsDevReasoningEffortMatrix matrix = new(cache);

        IReadOnlySet<ReasoningEffort> result =
            await matrix.GetSupportedReasoningValuesAsync("some-model", "test-provider");

        // The fixture catalog loads successfully (stale copy) but its provider has no
        // models, so the auto-only fallback applies after the async load.
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
        await Assert.That(matrix.CacheStatus).IsEqualTo(CacheStatus.Stale);
    }
}
