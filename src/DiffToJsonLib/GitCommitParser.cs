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
using CliInvoke.Core;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Compliance.Classification;
using DiffToJsonLib.Prompts;

namespace DiffToJsonLib;

public class GitCommitParser : IGitCommitParser
{
    private readonly IRedactorProvider _redactorProvider;
    private readonly IProcessInvoker _processInvoker;

    public GitCommitParser(IRedactorProvider redactorProvider, 
        IProcessInvoker processInvoker)
    {
        _redactorProvider = redactorProvider;
        _processInvoker = processInvoker;
    }

    private async Task<PipedProcessResult> GetDiffsAsync(string workingDir, CancellationToken cancellationToken)
    {
        using ProcessConfiguration processConfiguration = new(OperatingSystem.IsWindows() ? "git.exe" : "git",
            "--no-pager log -p", workingDir);
        
        return await _processInvoker.ExecutePipedAsync(processConfiguration, cancellationToken: cancellationToken);
    }
    
    public async Task<CommitRecord[]> ParseCommitsToArrayAsync(string repoName, string license,
        string workingDir, string repoUrl, CancellationToken cancellationToken)
    {
        return await ParseCommitsStreamAsync(repoName, license, workingDir, repoUrl, cancellationToken)
            .ToArrayAsync(cancellationToken: cancellationToken);
    }

    public async IAsyncEnumerable<CommitRecord> ParseCommitsStreamAsync(string repoName, string license, string workingDir,
        string repoUrl, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using PipedProcessResult processResult = await GetDiffsAsync(workingDir, cancellationToken).ConfigureAwait(false);
        
        processResult.StandardOutput.Position = 0;
        using StreamReader reader = new(processResult.StandardOutput, Encoding.Default);

        string? line;
        bool isCollectingMessage = false;
        bool isCollectingDiff = false;
        
        StringBuilder messageBuilder = new();
        StringBuilder diffBuilder = new();
        
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (line.StartsWith("commit "))
            {
                if (messageBuilder.Length > 0 && !string.IsNullOrWhiteSpace(diffBuilder.ToString()))
                {
                    yield return new CommitRecord(
                        diffBuilder.ToString().TrimStart().TrimEnd(),
                        _redactorProvider.GetRedactor(new DataClassificationSet(PiiRedactionCategory.Value)).Redact(messageBuilder.ToString().TrimStart().TrimEnd()),
                        RepoName: repoName,
                        License: license,
                        RepoUrl: repoUrl
                    );
                }
                
                messageBuilder.Clear();
                diffBuilder.Clear();
                isCollectingMessage = false;
                isCollectingDiff = false;
            }
            else if (line.StartsWith("Author: ") || line.StartsWith("Date: "))
            {
                // Skip
            }
            else if (!isCollectingDiff && !string.IsNullOrWhiteSpace(line) && !isCollectingMessage)
            {
                isCollectingMessage = true;
                messageBuilder.AppendLine(line);
            }
            else if (!isCollectingDiff && (isCollectingMessage || string.IsNullOrWhiteSpace(line)))
            {
                if (line.StartsWith("diff --git"))
                {
                    isCollectingDiff = true;
                    // Start collecting diff but omit the "diff --git " line
                }
                else
                {
                    messageBuilder.AppendLine(line);
                }
            }
            else if (line.StartsWith("diff --git"))
            {
                isCollectingDiff = true;
                // Start collecting diff but omit the "diff --git " line
            }
            else if (isCollectingDiff)
            {
                diffBuilder.AppendLine(line);
            }
        }
        
        if (messageBuilder.Length > 0 && !string.IsNullOrWhiteSpace(diffBuilder.ToString()))
        { 
            yield return new CommitRecord(
                diffBuilder.ToString().TrimStart().TrimEnd(),
                _redactorProvider.GetRedactor(new DataClassificationSet(PiiRedactionCategory.Value)).Redact(messageBuilder.ToString().TrimStart().TrimEnd()),
                RepoName: repoName,
                License: license,
                RepoUrl: repoUrl
            );
        }
    }

    public async IAsyncEnumerable<CommitTrainingRecord> ParseCommitsToTrainingStreamAsync(string repoName, string license,
        string workingDir, string repoUrl, string presetName, RedactionTier redactionTier,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        PromptTemplate template = PromptPresets.Get(presetName);
        RegexPiiRedactor piiRedactor = new();

        await using PipedProcessResult processResult = await GetDiffsAsync(workingDir, cancellationToken)
            .ConfigureAwait(false);

        processResult.StandardOutput.Position = 0;
        using StreamReader reader = new(processResult.StandardOutput, Encoding.Default);

        string? line;
        bool isCollectingMessage = false;
        bool isCollectingDiff = false;

        StringBuilder messageBuilder = new();
        StringBuilder diffBuilder = new();

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (line.StartsWith("commit "))
            {
                if (messageBuilder.Length > 0 && !string.IsNullOrWhiteSpace(diffBuilder.ToString()))
                {
                    yield return BuildTrainingRecord(template, messageBuilder, diffBuilder,
                        repoName, repoUrl, license, redactionTier, piiRedactor);
                }

                messageBuilder.Clear();
                diffBuilder.Clear();
                isCollectingMessage = false;
                isCollectingDiff = false;
            }
            else if (line.StartsWith("Author: ") || line.StartsWith("Date: "))
            {
                // Skip
            }
            else if (!isCollectingDiff && !string.IsNullOrWhiteSpace(line) && !isCollectingMessage)
            {
                isCollectingMessage = true;
                messageBuilder.AppendLine(line);
            }
            else if (!isCollectingDiff && (isCollectingMessage || string.IsNullOrWhiteSpace(line)))
            {
                if (line.StartsWith("diff --git"))
                {
                    isCollectingDiff = true;
                }
                else
                {
                    messageBuilder.AppendLine(line);
                }
            }
            else if (line.StartsWith("diff --git"))
            {
                isCollectingDiff = true;
            }
            else if (isCollectingDiff)
            {
                diffBuilder.AppendLine(line);
            }
        }

        if (messageBuilder.Length > 0 && !string.IsNullOrWhiteSpace(diffBuilder.ToString()))
        {
            yield return BuildTrainingRecord(template, messageBuilder, diffBuilder,
                repoName, repoUrl, license, redactionTier, piiRedactor);
        }
    }

    private static CommitTrainingRecord BuildTrainingRecord(
        PromptTemplate template, StringBuilder messageBuilder, StringBuilder diffBuilder,
        string repoName, string repoUrl, string license,
        RedactionTier redactionTier, RegexPiiRedactor piiRedactor)
    {
        string rawMessage = messageBuilder.ToString().TrimStart().TrimEnd();
        string rawDiff = diffBuilder.ToString().TrimStart().TrimEnd();

        string message = redactionTier >= RedactionTier.Message
            ? piiRedactor.Redact(rawMessage)
            : rawMessage;

        string diff = redactionTier >= RedactionTier.Diff
            ? piiRedactor.Redact(rawDiff)
            : rawDiff;

        string systemContent = SubstitutePlaceholders(template.System, diff, message, repoName, license, repoUrl);
        string userContent = SubstitutePlaceholders(template.User, diff, message, repoName, license, repoUrl);

        return new CommitTrainingRecord(
            Messages:
            [
                new Message("system", systemContent),
                new Message("user", userContent),
                new Message("assistant", message)
            ],
            Provenance: new Provenance(repoName, repoUrl),
            Legal: new Legal(license)
        );
    }

    private static string SubstitutePlaceholders(string text, string diff, string commitMessage,
        string repoName, string license, string repoUrl)
    {
        return text
            .Replace("{diff}", diff)
            .Replace("{commitMessage}", commitMessage)
            .Replace("{repoName}", repoName)
            .Replace("{license}", license)
            .Replace("{repoUrl}", repoUrl);
    }
}
