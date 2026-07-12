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

        GitLogParser gitLogParser = new();

        await foreach (RawCommit raw in gitLogParser.ParseAsync(reader, cancellationToken))
        {
            yield return new CommitRecord(raw.Diff, raw.Message, repoName, license, repoUrl);
        }
    }

}
