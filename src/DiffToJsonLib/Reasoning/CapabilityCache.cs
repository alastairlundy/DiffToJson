using System.Text.Json;
using ModelsDotDevSharp;
using ModelsDotDevSharp.Contexts;

namespace DiffToJsonLib.Reasoning;

public enum CacheStatus
{
    Fresh,
    Stale,
    Unavailable
}

public sealed record CapabilityCacheResult(AIProviderInfo[]? Providers, CacheStatus Status);

public sealed class CapabilityCache
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromHours(24);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromMinutes(5);

    private readonly string _cacheDirectory;
    private readonly IClock _clock;
    private readonly IFileStore _fileStore;
    private readonly IFetcher _fetcher;
    private readonly Lock _memoryLock = new();
    private CapabilityCacheResult? _memoryResult;
    private DateTimeOffset _memoryResultTime;

    public CapabilityCache(string? cacheDirectory = null, IClock? clock = null, IFileStore? fileStore = null, IFetcher? fetcher = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "difftojson",
            "capability-cache");
        _clock = clock ?? new SystemClock();
        _fileStore = fileStore ?? new DiskFileStore();
        _fetcher = fetcher ?? new HttpClientFetcher();
    }

    public async Task<CapabilityCacheResult> GetProviderInfosAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock.UtcNow;

        lock (_memoryLock)
        {
            if (_memoryResult is not null)
            {
                TimeSpan cooldown = _memoryResult.Status == CacheStatus.Fresh ? TimeToLive : NegativeCacheTtl;
                if (now - _memoryResultTime < cooldown)
                {
                    return _memoryResult;
                }
            }
        }

        DateTimeOffset? fetchTime = TryLoadFetchTimestamp();
        bool diskFresh = fetchTime.HasValue && now - fetchTime.Value < TimeToLive;

        if (diskFresh)
        {
            AIProviderInfo[]? cached = TryLoadFromCache();
            if (cached is not null)
            {
                return StoreInMemory(new CapabilityCacheResult(cached, CacheStatus.Fresh), now);
            }
            // Fresh timestamp but missing/corrupt payload: fall through to fetch.
        }

        try
        {
            byte[] bytes = await _fetcher.FetchApiJsonAsync(cancellationToken).ConfigureAwait(false);
            // Validate before evicting the good stale copy (deserialize-first).
            AIProviderInfo[]? parsed = Deserialize(bytes);
            if (parsed is null)
            {
                throw new InvalidOperationException("Fetched capability payload deserialized to null.");
            }

            SaveToCache(bytes);
            SaveFetchTimestamp(_clock.UtcNow);
            return StoreInMemory(new CapabilityCacheResult(parsed, CacheStatus.Fresh), _clock.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            AIProviderInfo[]? stale = TryLoadFromCache();
            if (stale is not null)
            {
                return StoreInMemory(new CapabilityCacheResult(stale, CacheStatus.Stale), now);
            }

            return StoreInMemory(new CapabilityCacheResult(null, CacheStatus.Unavailable), now);
        }
    }

    private CapabilityCacheResult StoreInMemory(CapabilityCacheResult result, DateTimeOffset timestamp)
    {
        lock (_memoryLock)
        {
            _memoryResult = result;
            _memoryResultTime = timestamp;
        }

        return result;
    }

    private AIProviderInfo[]? TryLoadFromCache()
    {
        try
        {
            string path = GetCacheFilePath();
            if (!_fileStore.Exists(path))
                return null;

            byte[] bytes = _fileStore.ReadAllBytes(path);
            if (bytes.Length == 0)
                return null;

            return Deserialize(bytes);
        }
        catch
        {
            return null;
        }
    }

    private DateTimeOffset? TryLoadFetchTimestamp()
    {
        try
        {
            string path = GetTimestampFilePath();
            if (!_fileStore.Exists(path))
                return null;

            string text = _fileStore.ReadAllText(path);
            if (DateTimeOffset.TryParse(text, out DateTimeOffset timestamp))
                return timestamp;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveToCache(byte[] bytes)
    {
        _fileStore.WriteAllBytes(GetCacheFilePath(), bytes);
    }

    private void SaveFetchTimestamp(DateTimeOffset timestamp)
    {
        _fileStore.WriteAllText(GetTimestampFilePath(), timestamp.ToString("O"));
    }

    private static AIProviderInfo[] Deserialize(byte[] bytes)
    {
        return JsonSerializer.Deserialize<AIProviderInfo[]>(bytes, ModelInfoJsonContext.Default.AIProviderInfoArray)!;
    }

    private string GetCacheFilePath() => Path.Combine(_cacheDirectory, "api.json");

    private string GetTimestampFilePath() => Path.Combine(_cacheDirectory, "api-fetched.txt");

    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public interface IFileStore
    {
        bool Exists(string path);
        byte[] ReadAllBytes(string path);
        string ReadAllText(string path);
        void WriteAllBytes(string path, byte[] bytes);
        void WriteAllText(string path, string text);
    }

    public interface IFetcher
    {
        Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default);
    }
}

internal sealed class SystemClock : CapabilityCache.IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

internal sealed class DiskFileStore : CapabilityCache.IFileStore
{
    public bool Exists(string path) => File.Exists(path);

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllBytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public void WriteAllText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}

internal sealed class HttpClientFetcher : CapabilityCache.IFetcher
{
    private const string ApiUrl = "https://models.dev/api.json";

    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SharedHttpClient.GetAsync(ApiUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }
}
