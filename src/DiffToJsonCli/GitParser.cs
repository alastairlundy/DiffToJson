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

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CliInvoke;
using CliInvoke.Core;

namespace DiffToJsonCli;

public partial class GitParser
{
    private static readonly Regex PiiRegex = MyRegex();
    public async Task ParseStreamingAsync(string repoName, string license, string outputPath,
        string workingDir, string repoUrl)
    {
        await using PipedProcessResult processResult = await CliRun
            .RunPipedAsync(OperatingSystem.IsWindows() ? "git.exe" : "git", 
                "--no-pager log -p", workingDir);
        
        processResult.StandardOutput.Position = 0;
        using StreamReader reader = new(processResult.StandardOutput, Encoding.UTF8);
        await using StreamWriter writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        string? line;
        bool isCollectingMessage = false;
        bool isCollectingDiff = false;
        
        StringBuilder messageBuilder = new();
        StringBuilder diffBuilder = new();

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith("commit "))
            {
                if (messageBuilder.Length > 0 && !string.IsNullOrWhiteSpace(diffBuilder.ToString()))
                {
                    await WriteRecord(writer, messageBuilder.ToString(), diffBuilder.ToString(), repoName, license, repoUrl);
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
            await WriteRecord(writer, messageBuilder.ToString(), diffBuilder.ToString(), repoName, license, repoUrl);
        }
    }

    private async Task WriteRecord(StreamWriter writer, string message, string diff, string repoName, string license, string repoUrl)
    {
        CommitRecord record = new CommitRecord(
            diff.TrimStart().TrimEnd(),
            Sanitize(message.TrimStart().TrimEnd()),
            RepoName: repoName,
            License: license,
            RepoUrl: repoUrl
        );

        string json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);
        await writer.WriteLineAsync(json);
    }

    public string Sanitize(string text) 
        => PiiRegex.Replace(text, "REDACTED");

    [GeneratedRegex(@"<([^>\s]+@[^>\s]+\.[^>\s]+)>|\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}
