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
