using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;

namespace GitDiffToJsonL.Cli;

public partial class GitParser
{
    private static readonly Regex PiiRegex = MyRegex();

    public async Task ParseStreamingAsync(string repoName, string license, string outputPath,
        string workingDir, string repoUrl)
    {
        using MemoryStream memoryStream = new();

        await CliWrap.Cli.Wrap("git")
            .WithArguments("--no-pager log -p")
            .WithWorkingDirectory(workingDir)
            .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
            .ExecuteBufferedAsync();

        memoryStream.Position = 0;
        using StreamReader reader = new(memoryStream, Encoding.UTF8);
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
    {
        return PiiRegex.Replace(text, "REDACTED");
    }

    [GeneratedRegex(@"<([^>\s]+@[^>\s]+\.[^>\s]+)>|\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}
