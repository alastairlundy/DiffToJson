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

using DiffToJsonLib.Models;
using DiffToJsonLib.Redactors;
using DiffToJsonLib.Training;
using DiffToJsonLib.Training.Abstractions;
using Microsoft.Extensions.AI;
using Polly;
using Polly.Retry;

namespace DiffToJsonLib.Writers;

public class LlmAssistantWriter : IAssistantMessageGenerator
{
    private readonly Lazy<IChatClient> _clientLazy;
    private readonly RedactionPolicy _policy;
    private readonly ResiliencePipeline _pipeline;

    public LlmAssistantWriter(IChatClientFactory chatClientFactory, RedactionPolicy policy)
    {
        _clientLazy = new Lazy<IChatClient>(chatClientFactory.Create);
        _policy = policy;
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

    public async Task<AssistantMessageResult> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CommitRecord redactedCommit,
        ChatOptions options,
        CancellationToken cancellationToken)
    {
        IChatClient client = _clientLazy.Value;

        ChatMessage system = new(ChatRole.System, systemPrompt);
        ChatMessage user = new(ChatRole.User, userPrompt);

        try
        {
            ChatResponse? response = await _pipeline.ExecuteAsync(
                async ct => await client.GetResponseAsync([system, user], options, ct),
                cancellationToken);

            ChatMessage? assistantMessage = response.Messages
                .FirstOrDefault(m => m.Role == ChatRole.Assistant);

            if (assistantMessage is null)
            {
                return new AssistantMessageResult.AssistantMessageAttemptedAndFailed(
                    FallbackContent: null,
                    OriginalAssistantMessage: redactedCommit.CommitMessage);
            }

            List<string> reasoningItems = assistantMessage.Contents
                .OfType<TextReasoningContent>()
                .Select(r => r.Text)
                .ToList();

            string visibleText = assistantMessage.Text ?? "";

            string composed = reasoningItems.Count > 0
                ? $"<think>{string.Join("\n", reasoningItems)}</think>\n\n{visibleText}"
                : visibleText;

            string result = composed.Trim();

            result = _policy.Redact(result, RedactionTier.All);

            return new AssistantMessageResult.AssistantMessageGenerated(
                result,
                redactedCommit.CommitMessage);
        }
        catch
        {
            return new AssistantMessageResult.AssistantMessageAttemptedAndFailed(
                FallbackContent: null,
                OriginalAssistantMessage: redactedCommit.CommitMessage);
        }
    }
}
