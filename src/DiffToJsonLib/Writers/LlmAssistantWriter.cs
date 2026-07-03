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
using Polly;
using Polly.Retry;

namespace DiffToJsonLib.Writers;

public class LlmAssistantWriter
{
    private readonly Lazy<IChatClient> _clientLazy;
    private readonly RedactionTier _tier;
    private readonly ResiliencePipeline _pipeline;

    public LlmAssistantWriter(IChatClientFactory chatClientFactory, RedactionTier tier)
    {
        _clientLazy = new Lazy<IChatClient>(chatClientFactory.Create);
        _tier = tier;
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false
            })
            .Build();
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

        ChatMessage system = new(ChatRole.System, systemPrompt);
        ChatMessage user = new(ChatRole.User, userPrompt);

        ChatResponse? response = await _pipeline.ExecuteAsync(
            async ct => await client.GetResponseAsync([system, user], cancellationToken: ct),
            cancellationToken);

        string? message = response.Messages
            .FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Text;

        if (string.IsNullOrWhiteSpace(message))
            return null;

        string result = message.Trim();

        if (_tier == RedactionTier.All)
        {
            var redactor = new RegexPiiRedactor();
            result = redactor.Redact(result);
        }

        return result;
    }
}
