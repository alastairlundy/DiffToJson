using System.Text;
using System.Text.Json;
using DiffToJsonLib.Reasoning;
using ModelsDotDevSharp;
using ModelsDotDevSharp.Contexts;

namespace DiffToJsonLib.Tests.Reasoning;

public class CapabilityCacheTests
{
    private static readonly string TestCacheDir = Path.Combine(
        Path.GetTempPath(),
        "difftojson-tests",
        Guid.NewGuid().ToString("N"));

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
        return Encoding.UTF8.GetBytes(json);
    }

    private static readonly byte[] FixtureBytes = CreateMinimalProviderJson();

    public CapabilityCacheTests()
    {
        if (Directory.Exists(TestCacheDir))
            Directory.Delete(TestCacheDir, recursive: true);
    }

    [Test]
    public async Task FreshCache_WithinTtl_ReturnsFresh()
    {
        var clock = new StubClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var fileStore = new InMemoryFileStore();
        var cache = new CapabilityCache(
            TestCacheDir,
            clock,
            fileStore,
            new ThrowingFetcher());

        fileStore.WriteAllBytes(
            Path.Combine(TestCacheDir, "api.json"),
            FixtureBytes);
        fileStore.WriteAllText(
            Path.Combine(TestCacheDir, "api-fetched.txt"),
            clock.UtcNow.ToString("O"));

        var result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(result.Providers).IsNotNull();
        await Assert.That(result.Providers!.Length).IsEqualTo(1);
        await Assert.That(result.Providers![0].Id).IsEqualTo("test-provider");
    }

    [Test]
    public async Task ExpiredCache_ReFetches_ReturnsFresh()
    {
        var fetchTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new StubClock(fetchTime.AddHours(25));
        var fileStore = new InMemoryFileStore();
        var fetcher = new StubFetcher(FixtureBytes);
        var cache = new CapabilityCache(TestCacheDir, clock, fileStore, fetcher);

        fileStore.WriteAllBytes(
            Path.Combine(TestCacheDir, "api.json"),
            FixtureBytes);
        fileStore.WriteAllText(
            Path.Combine(TestCacheDir, "api-fetched.txt"),
            fetchTime.ToString("O"));

        var result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(result.Providers).IsNotNull();
        await Assert.That(fetcher.WasCalled).IsTrue();
    }

    [Test]
    public async Task NoCache_FetchSucceeds_ReturnsFresh()
    {
        var clock = new StubClock(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var fileStore = new InMemoryFileStore();
        var fetcher = new StubFetcher(FixtureBytes);
        var cache = new CapabilityCache(TestCacheDir, clock, fileStore, fetcher);

        var result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(result.Providers).IsNotNull();
        await Assert.That(result.Providers!.Length).IsEqualTo(1);
        await Assert.That(fetcher.WasCalled).IsTrue();
    }

    [Test]
    public async Task StaleCache_FetchFails_ReturnsStale()
    {
        var clock = new StubClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var fileStore = new InMemoryFileStore();
        var cache = new CapabilityCache(
            TestCacheDir,
            clock,
            fileStore,
            new ThrowingFetcher());

        fileStore.WriteAllBytes(
            Path.Combine(TestCacheDir, "api.json"),
            FixtureBytes);
        fileStore.WriteAllText(
            Path.Combine(TestCacheDir, "api-fetched.txt"),
            new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero).ToString("O"));

        var result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Stale);
        await Assert.That(result.Providers).IsNotNull();
        await Assert.That(result.Providers!.Length).IsEqualTo(1);
    }

    [Test]
    public async Task EmptyCache_FetchFails_ReturnsUnavailable()
    {
        var clock = new StubClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var fileStore = new InMemoryFileStore();
        var cache = new CapabilityCache(
            TestCacheDir,
            clock,
            fileStore,
            new ThrowingFetcher());

        var result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Unavailable);
        await Assert.That(result.Providers).IsNull();
    }

    [Test]
    public async Task CorruptCache_FetchFails_ReturnsUnavailable()
    {
        var clock = new StubClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var fileStore = new InMemoryFileStore();
        var cache = new CapabilityCache(
            TestCacheDir,
            clock,
            fileStore,
            new ThrowingFetcher());

        fileStore.WriteAllBytes(
            Path.Combine(TestCacheDir, "api.json"),
            Encoding.UTF8.GetBytes("not valid json"));
        fileStore.WriteAllText(
            Path.Combine(TestCacheDir, "api-fetched.txt"),
            new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero).ToString("O"));

        var result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Unavailable);
        await Assert.That(result.Providers).IsNull();
    }

    [Test]
    public async Task StaleCache_FetchSucceeds_UpdatesCache()
    {
        var originalTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new StubClock(originalTime.AddHours(25));
        var fileStore = new InMemoryFileStore();
        var fetcher = new StubFetcher(FixtureBytes);
        var cache = new CapabilityCache(TestCacheDir, clock, fileStore, fetcher);

        fileStore.WriteAllBytes(
            Path.Combine(TestCacheDir, "api.json"),
            FixtureBytes);
        fileStore.WriteAllText(
            Path.Combine(TestCacheDir, "api-fetched.txt"),
            originalTime.ToString("O"));

        var result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(fetcher.WasCalled).IsTrue();

        string timestampContent = fileStore.ReadAllText(
            Path.Combine(TestCacheDir, "api-fetched.txt"));
        await Assert.That(DateTimeOffset.TryParse(timestampContent, out _)).IsTrue();
    }

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
        public bool WasCalled { get; private set; }

        public Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(cannedResponse);
        }
    }

    private sealed class ThrowingFetcher : CapabilityCache.IFetcher
    {
        public Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("Network unavailable");
        }
    }
}
