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
using DiffToJsonLib.Parsing;

namespace DiffToJsonLib;

public class GitCommitParser : IGitCommitParser
{
    private readonly IProcessInvoker _processInvoker;

    public GitCommitParser(IProcessInvoker processInvoker)
    {
        _processInvoker = processInvoker;
    }

    private async Task<string> GetDiffsAsync(string workingDir, CancellationToken cancellationToken)
    {
        ProcessConfiguration processConfiguration = new()
        {
            TargetFilePath = OperatingSystem.IsWindows() ? "git.exe" : "git",
            Arguments = "--no-pager log -p",
            WorkingDirectoryPath = workingDir,
            OutputRedirection = true,
        };

        BufferedProcessResult processResult = await _processInvoker.ExecuteBufferedAsync(processConfiguration,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return processResult.StandardOutput ?? string.Empty;
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
        string gitOutput = await GetDiffsAsync(workingDir, cancellationToken).ConfigureAwait(false);

        using StringReader reader = new(gitOutput);

        GitLogParser gitLogParser = new();

        await foreach (RawCommit raw in gitLogParser.ParseAsync(reader, cancellationToken))
        {
            yield return new CommitRecord(raw.Diff, raw.Message, repoName, license, repoUrl);
        }
    }

}
