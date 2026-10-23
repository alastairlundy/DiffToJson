using System.IO.Abstractions;
using System.Text.Json;
using ModelsDotDevSharp;
using ModelsDotDevSharp.Contexts;

namespace DiffToJsonLib.Reasoning;

public sealed class CapabilityCache
{
    private readonly IFileSystem _fileSystem;
    private readonly IFetcher _fetcher;
    private static readonly TimeSpan TimeToLive = TimeSpan.FromHours(24);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromMinutes(5);

    private readonly string _cacheDirectory;
    private readonly Lock _memoryLock = new();
    private CapabilityCacheResult? _memoryResult;
    private DateTimeOffset _memoryResultTime;

    public CapabilityCache(IFileSystem fileSystem, string? cacheDirectory = null, IFetcher? fetcher = null)
    {
        _fileSystem = fileSystem;
        _fetcher = fetcher ?? new HttpClientFetcher();
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "difftojson",
            "capability-cache");
    }

    public async Task<CapabilityCacheResult> GetProviderInfosAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

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

            await SaveToCache(bytes).ConfigureAwait(false);
            await SaveFetchTimestamp(DateTimeOffset.UtcNow).ConfigureAwait(false);
            return StoreInMemory(new CapabilityCacheResult(parsed, CacheStatus.Fresh), DateTimeOffset.UtcNow);
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

            return StoreInMemory(new CapabilityCacheResult(Providers: null, CacheStatus.Unavailable), now);
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
            if (!_fileSystem.File.Exists(path))
                return null;

            byte[] bytes = _fileSystem.File.ReadAllBytes(path);
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
            if (!_fileSystem.File.Exists(path))
                return null;

            string text = _fileSystem.File.ReadAllText(path);
            if (DateTimeOffset.TryParse(text, out DateTimeOffset timestamp))
                return timestamp;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveToCache(byte[] bytes)
    {
        _fileSystem.Directory.CreateDirectory(_cacheDirectory);
        await _fileSystem.File.WriteAllBytesAsync(GetCacheFilePath(), bytes).ConfigureAwait(false);
    }

    private async Task SaveFetchTimestamp(DateTimeOffset timestamp)
    {
        _fileSystem.Directory.CreateDirectory(_cacheDirectory);
        await _fileSystem.File.WriteAllTextAsync(GetTimestampFilePath(), timestamp.ToString("O"))
            .ConfigureAwait(false);
    }

    private static AIProviderInfo[]? Deserialize(byte[] bytes) =>
        JsonSerializer.Deserialize<AIProviderInfo[]>(bytes, ModelInfoJsonContext.Default.AIProviderInfoArray);

    private string GetCacheFilePath() => Path.Combine(_cacheDirectory, "api.json");

    private string GetTimestampFilePath() => Path.Combine(_cacheDirectory, "api-fetched.txt");
}