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

using Microsoft.Extensions.AI;
using DiffToJsonLib.Redactors;

namespace DiffToJsonLib;

public class LlmAssistantWriter
{
    private readonly Lazy<IChatClient> _clientLazy;
    private readonly RedactionTier _tier;

    public LlmAssistantWriter(IChatClientFactory chatClientFactory, RedactionTier tier)
    {
        _clientLazy = new Lazy<IChatClient>(chatClientFactory.Create);
        _tier = tier;
    }

    public async Task<string?> GenerateAssistantAsync(
        string systemPrompt,
        string userPrompt,
        string repoName,
        string license,
        string repoUrl,
        CancellationToken cancellationToken = default)
    {
        IChatClient client = _clientLazy.Value;

        const int maxRetries = 3;
        int delayMs = 1000;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                ChatMessage system = new(ChatRole.System, systemPrompt);
                ChatMessage user = new(ChatRole.User, userPrompt);

                ChatResponse response = await client.GetResponseAsync(
                    [system, user],
                    cancellationToken: cancellationToken);

                string? message = response.Messages
                    .FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Text;

                if (string.IsNullOrWhiteSpace(message))
                {
                    if (attempt == maxRetries - 1)
                        return null;

                    continue;
                }

                string result = message.Trim();

                if (_tier == RedactionTier.All)
                {
                    var redactor = new RegexPiiRedactor();
                    result = redactor.Redact(result);
                }

                return result;
            }
            catch
            {
                if (attempt == maxRetries - 1)
                    return null;

                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2;
            }
        }

        return null;
    }
}
