namespace DiffToJsonLib.Reasoning;

/// <summary>
/// Default <see cref="IFetcher"/> that downloads the models.dev capability payload.
/// </summary>
internal sealed class HttpClientFetcher : IFetcher
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
