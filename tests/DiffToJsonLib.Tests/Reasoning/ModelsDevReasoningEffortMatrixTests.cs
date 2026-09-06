using System.Text;
using System.Text.Json;
using DiffToJsonLib.Reasoning;
using ModelsDotDevSharp;
using ModelsDotDevSharp.Contexts;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonLib.Tests.Reasoning;

public class ModelsDevReasoningEffortMatrixTests
{
    private static CapabilityCache CreateCache(AIProviderInfo[] providers)
    {
        string json = JsonSerializer.Serialize(providers, ModelInfoJsonContext.Default.AIProviderInfoArray);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        var clock = new StubClock(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var fileStore = new InMemoryFileStore();
        var fetcher = new StubFetcher(bytes);
        string cacheDir = Path.Combine(Path.GetTempPath(), "difftojson-matrix-tests", Guid.NewGuid().ToString("N"));

        var cache = new CapabilityCache(cacheDir, clock, fileStore, fetcher);
        return cache;
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
        AIModelInfo model = CreateModel("gpt-4o", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Effort,
                ["low", "medium", "high", "xhigh", "max"])
        });
        AIProviderInfo provider = CreateProvider("openai", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

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
        AIModelInfo model = CreateModel("claude-sonnet-4-5", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Effort,
                ["low", "medium", "high"])
        });
        AIProviderInfo provider = CreateProvider("anthropic", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

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
        AIModelInfo model = CreateModel("claude-sonnet-5", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        });
        AIProviderInfo provider = CreateProvider("anthropic", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

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
        AIModelInfo model = CreateModel("claude-haiku-4-5", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.BudgetTokens)
        });
        AIProviderInfo provider = CreateProvider("anthropic", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

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
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("nonexistent-model", "openai");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
    }

    [Test]
    public async Task ModelWithNoReasoningOptions_ReturnsAutoOnly()
    {
        AIModelInfo model = CreateModel("some-model", false, null);
        AIProviderInfo provider = CreateProvider("openai", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("some-model", "openai");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
    }

    // --- ProducesReasoningOnAuto ---

    [Test]
    public async Task SupportsReasoningTrue_ProducesReasoningOnAuto()
    {
        AIModelInfo model = CreateModel("gpt-4o", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Effort, ["low", "high"])
        });
        AIProviderInfo provider = CreateProvider("openai", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        await Assert.That(matrix.ProducesReasoningOnAuto("gpt-4o", "openai")).IsTrue();
    }

    [Test]
    public async Task SupportsReasoningFalse_DoesNotProduceReasoningOnAuto()
    {
        AIModelInfo model = CreateModel("deepseek-chat", false, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        });
        AIProviderInfo provider = CreateProvider("deepseek", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        await Assert.That(matrix.ProducesReasoningOnAuto("deepseek-chat", "deepseek")).IsFalse();
    }

    // --- Exceptions table ---

    [Test]
    public async Task MiniMaxM1_ExceptionReturnsFalse()
    {
        AIModelInfo model = CreateModel("minimax-m1", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        });
        AIProviderInfo provider = CreateProvider("minimax", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        await Assert.That(matrix.ProducesReasoningOnAuto("minimax-m1", "minimax")).IsFalse();
    }

    [Test]
    public async Task MiniMaxM3_ExceptionReturnsFalse()
    {
        AIModelInfo model = CreateModel("minimax-m3", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        });
        AIProviderInfo provider = CreateProvider("minimax", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        await Assert.That(matrix.ProducesReasoningOnAuto("minimax-m3", "minimax")).IsFalse();
    }

    [Test]
    public async Task DeepseekChat_ExceptionReturnsFalse()
    {
        AIModelInfo model = CreateModel("deepseek-chat", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        });
        AIProviderInfo provider = CreateProvider("deepseek", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        await Assert.That(matrix.ProducesReasoningOnAuto("deepseek-chat", "deepseek")).IsFalse();
    }

    // --- GetReasoningType ---

    [Test]
    public async Task GetReasoningType_ReturnsEffortForEffortModel()
    {
        AIModelInfo model = CreateModel("gpt-4o", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Effort, ["low", "high"])
        });
        AIProviderInfo provider = CreateProvider("openai", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        AIModelReasoningOptionType? type = matrix.GetReasoningType("gpt-4o", "openai");

        await Assert.That(type).IsEqualTo(AIModelReasoningOptionType.Effort);
    }

    [Test]
    public async Task GetReasoningType_ReturnsNullForMissingModel()
    {
        AIProviderInfo provider = CreateProvider("openai", []);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        AIModelReasoningOptionType? type = matrix.GetReasoningType("nonexistent", "openai");

        await Assert.That(type).IsNull();
    }

    // --- Provider mapping ---

    [Test]
    public async Task ProviderMapping_OllamaResolvesToOllamaCloud()
    {
        AIModelInfo model = CreateModel("qwen3", true, new[]
        {
            CreateReasoningOption(AIModelReasoningOptionType.Toggle)
        });
        AIProviderInfo provider = CreateProvider("ollama-cloud", [model]);
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

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
        var cache = CreateCache([provider]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("gpt-4o", "unknown-provider");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
    }

    // --- Empty cache ---

    [Test]
    public async Task EmptyCache_ReturnsAutoOnly()
    {
        var cache = CreateCache([]);
        var matrix = new ModelsDevReasoningEffortMatrix(cache);

        IReadOnlySet<ReasoningEffort> result = matrix.GetSupportedReasoningValues("gpt-4o", "openai");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result.Contains(ReasoningEffort.Auto)).IsTrue();
    }

    // --- Stub types ---

    private sealed class StubClock(DateTimeOffset initial) : CapabilityCache.IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private sealed class InMemoryFileStore : CapabilityCache.IFileStore
    {
        private readonly Dictionary<string, byte[]> _bytes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _text = new(StringComparer.OrdinalIgnoreCase);

        public bool Exists(string path) => _bytes.ContainsKey(path) || _text.ContainsKey(path);

        public byte[] ReadAllBytes(string path) =>
            _bytes.TryGetValue(path, out byte[]? bytes) ? bytes : throw new FileNotFoundException(path);

        public string ReadAllText(string path) =>
            _text.TryGetValue(path, out string? text) ? text : throw new FileNotFoundException(path);

        public void WriteAllBytes(string path, byte[] bytes) => _bytes[path] = bytes;

        public void WriteAllText(string path, string text) => _text[path] = text;
    }

    private sealed class StubFetcher(byte[] cannedResponse) : CapabilityCache.IFetcher
    {
        public Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(cannedResponse);
        }
    }
}
