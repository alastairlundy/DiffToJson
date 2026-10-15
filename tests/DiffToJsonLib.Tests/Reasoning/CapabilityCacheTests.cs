using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text;
using DiffToJsonLib.Reasoning;

namespace DiffToJsonLib.Tests.Reasoning;

public class CapabilityCacheTests
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
        return Encoding.UTF8.GetBytes(json);
    }

    private static readonly byte[] FixtureBytes = CreateMinimalProviderJson();

    /// <summary>
    /// Each test gets its own in-memory file system, so the shared cache path
    /// never races between tests and nothing touches the real disk.
    /// </summary>
    private static MockFileSystem CreateFileSystem()
    {
        MockFileSystem fileSystem = new();
        fileSystem.Directory.CreateDirectory(TestCacheDir);
        return fileSystem;
    }

    private static void SeedCache(MockFileSystem fileSystem, byte[] payload, DateTimeOffset fetchedAt)
    {
        fileSystem.File.WriteAllBytes(CacheFilePath, payload);
        fileSystem.File.WriteAllText(TimestampFilePath, fetchedAt.ToString("O"));
    }

    [Test]
    public async Task FreshCache_WithinTtl_ReturnsFresh()
    {
        MockFileSystem fileSystem = CreateFileSystem();
        CapabilityCache cache = new(fileSystem, TestCacheDir, new ThrowingFetcher());
        SeedCache(fileSystem, FixtureBytes, DateTimeOffset.UtcNow);

        CapabilityCacheResult result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(result.Providers).IsNotNull();
        await Assert.That(result.Providers!.Length).IsEqualTo(1);
        await Assert.That(result.Providers![0].Id).IsEqualTo("test-provider");
    }

    [Test]
    public async Task ExpiredCache_ReFetches_ReturnsFresh()
    {
        MockFileSystem fileSystem = CreateFileSystem();
        StubFetcher fetcher = new(FixtureBytes);
        CapabilityCache cache = new(fileSystem, TestCacheDir, fetcher);
        SeedCache(fileSystem, FixtureBytes, DateTimeOffset.UtcNow.AddHours(-25));

        CapabilityCacheResult result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(result.Providers).IsNotNull();
        await Assert.That(fetcher.WasCalled).IsTrue();
    }

    [Test]
    public async Task NoCache_FetchSucceeds_ReturnsFresh()
    {
        MockFileSystem fileSystem = CreateFileSystem();
        StubFetcher fetcher = new(FixtureBytes);
        CapabilityCache cache = new(fileSystem, TestCacheDir, fetcher);

        CapabilityCacheResult result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(result.Providers).IsNotNull();
        await Assert.That(result.Providers!.Length).IsEqualTo(1);
        await Assert.That(fetcher.WasCalled).IsTrue();
    }

    [Test]
    public async Task FirstRun_CacheDirectoryMissing_CreatedAndCacheWritten()
    {
        // No directory is pre-created: this mirrors a first run on a clean machine.
        MockFileSystem fileSystem = new();
        StubFetcher fetcher = new(FixtureBytes);
        CapabilityCache cache = new(fileSystem, TestCacheDir, fetcher);

        CapabilityCacheResult result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(fileSystem.Directory.Exists(TestCacheDir)).IsTrue();
        await Assert.That(fileSystem.File.Exists(CacheFilePath)).IsTrue();
        await Assert.That(fileSystem.File.Exists(TimestampFilePath)).IsTrue();
    }

    [Test]
    public async Task StaleCache_FetchFails_ReturnsStale()
    {
        MockFileSystem fileSystem = CreateFileSystem();
        CapabilityCache cache = new(fileSystem, TestCacheDir, new ThrowingFetcher());
        SeedCache(fileSystem, FixtureBytes, DateTimeOffset.UtcNow.AddHours(-48));

        CapabilityCacheResult result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Stale);
        await Assert.That(result.Providers).IsNotNull();
        await Assert.That(result.Providers!.Length).IsEqualTo(1);
    }

    [Test]
    public async Task EmptyCache_FetchFails_ReturnsUnavailable()
    {
        MockFileSystem fileSystem = CreateFileSystem();
        CapabilityCache cache = new(fileSystem, TestCacheDir, new ThrowingFetcher());

        CapabilityCacheResult result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Unavailable);
        await Assert.That(result.Providers).IsNull();
    }

    [Test]
    public async Task CorruptCache_FetchFails_ReturnsUnavailable()
    {
        MockFileSystem fileSystem = CreateFileSystem();
        CapabilityCache cache = new(fileSystem, TestCacheDir, new ThrowingFetcher());
        SeedCache(
            fileSystem,
            Encoding.UTF8.GetBytes("not valid json"),
            DateTimeOffset.UtcNow.AddHours(-48));

        CapabilityCacheResult result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Unavailable);
        await Assert.That(result.Providers).IsNull();
    }

    [Test]
    public async Task StaleCache_FetchSucceeds_UpdatesCache()
    {
        MockFileSystem fileSystem = CreateFileSystem();
        StubFetcher fetcher = new(FixtureBytes);
        CapabilityCache cache = new(fileSystem, TestCacheDir, fetcher);
        SeedCache(fileSystem, FixtureBytes, DateTimeOffset.UtcNow.AddHours(-25));

        CapabilityCacheResult result = await cache.GetProviderInfosAsync();

        await Assert.That(result.Status).IsEqualTo(CacheStatus.Fresh);
        await Assert.That(fetcher.WasCalled).IsTrue();

        string timestampContent = fileSystem.File.ReadAllText(TimestampFilePath);
        await Assert.That(DateTimeOffset.TryParse(timestampContent, out _)).IsTrue();
    }

    private sealed class StubFetcher(byte[] cannedResponse) : IFetcher
    {
        public bool WasCalled { get; private set; }

        public Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(cannedResponse);
        }
    }

    private sealed class ThrowingFetcher : IFetcher
    {
        public Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("Network unavailable");
        }
    }
}
