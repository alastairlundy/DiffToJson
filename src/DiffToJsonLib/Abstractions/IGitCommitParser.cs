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

namespace DiffToJsonLib.Abstractions;

public interface IGitCommitParser
{
    Task<CommitRecord[]> ParseCommitsToArrayAsync(string repoName, string license,
        string workingDir, string repoUrl, CancellationToken cancellationToken);
    
    IAsyncEnumerable<CommitRecord> ParseCommitsStreamAsync(string repoName, string license,
        string workingDir, string repoUrl, CancellationToken cancellationToken);
    
    IAsyncEnumerable<CommitTrainingRecord> ParseCommitsToTrainingStreamAsync(string repoName, string license,
        string workingDir, string repoUrl, string presetName, RedactionTier redactionTier,
        CancellationToken cancellationToken);
}