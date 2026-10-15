namespace DiffToJsonLib.Reasoning;

/// <summary>
/// Fetches the raw models.dev capability payload. Injected into
/// <see cref="CapabilityCache"/> so tests can stub network access.
/// </summary>
public interface IFetcher
{
    Task<byte[]> FetchApiJsonAsync(CancellationToken cancellationToken = default);
}
