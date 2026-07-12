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

using System.Runtime.CompilerServices;
using DiffToJsonLib.Training.Abstractions;

namespace DiffToJsonLib.Training;

public sealed class TrainingExampleBuilder
{
    private readonly RedactionPolicy _redactor;
    private readonly IAssistantMessageGenerator _assistant;

    public TrainingExampleBuilder(RedactionPolicy redactor, IAssistantMessageGenerator assistant)
    {
        _redactor = redactor;
        _assistant = assistant;
    }

    public async IAsyncEnumerable<CommitTrainingRecord> BuildAsync(
        IAsyncEnumerable<CommitRecord> source,
        TrainingExampleOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var commit in source.WithCancellation(cancellationToken))
        {
            var redactedCommit = _redactor.Redact(commit, options.Tier);

            var systemContent = PromptSubstitutor.Substitute(
                options.Template.System,
                redactedCommit.Diff,
                redactedCommit.CommitMessage,
                redactedCommit.RepoName,
                redactedCommit.License,
                redactedCommit.RepoUrl);

            string userContent;
            if (options.LlmOverridePrompt is not null)
            {
                userContent = PromptSubstitutor.Substitute(
                    options.LlmOverridePrompt,
                    redactedCommit.Diff,
                    redactedCommit.CommitMessage,
                    redactedCommit.RepoName,
                    redactedCommit.License,
                    redactedCommit.RepoUrl);
            }
            else
            {
                userContent = PromptSubstitutor.Substitute(
                    options.Template.User,
                    redactedCommit.Diff,
                    redactedCommit.CommitMessage,
                    redactedCommit.RepoName,
                    redactedCommit.License,
                    redactedCommit.RepoUrl);
            }

            string assistantContent = string.Empty;
            string? originalAssistantMessage = null;

            if (options.LlmAssistantOutput)
            {
                var result = await _assistant.GenerateAsync(
                    systemContent, userContent, redactedCommit, options.Options, cancellationToken);

                switch (result)
                {
                    case AssistantMessageResult.AssistantMessageGenerated(var content, var original):
                        assistantContent = content;
                        originalAssistantMessage = original;
                        break;
                    case AssistantMessageResult.AssistantMessageDisabled(var fallback):
                        assistantContent = fallback;
                        originalAssistantMessage = null;
                        break;
                    case AssistantMessageResult.AssistantMessageAttemptedAndFailed(var fallback, var original):
                        assistantContent = fallback ?? original;
                        originalAssistantMessage = original;
                        break;
                }
            }
            else
            {
                assistantContent = redactedCommit.CommitMessage;
                originalAssistantMessage = null;
            }

            var messages = new Message[]
            {
                new("system", systemContent),
                new("user", userContent),
                new("assistant", assistantContent)
            };

            yield return new CommitTrainingRecord(
                Messages: messages,
                Provenance: new Provenance(redactedCommit.RepoName, redactedCommit.RepoUrl),
                Legal: new Legal(redactedCommit.License),
                OriginalAssistantMessage: originalAssistantMessage
            );
        }
    }
}
