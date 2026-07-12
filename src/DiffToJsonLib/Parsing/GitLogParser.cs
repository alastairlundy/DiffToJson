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

namespace DiffToJsonLib.Parsing;

public class GitLogParser
{
    public async IAsyncEnumerable<RawCommit> ParseAsync(TextReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? line;
        bool isCollectingMessage = false;
        bool isCollectingDiff = false;

        StringBuilder messageBuilder = new();
        StringBuilder diffBuilder = new();

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (line.StartsWith("commit "))
            {
                if (messageBuilder.Length > 0 && !string.IsNullOrWhiteSpace(diffBuilder.ToString()))
                {
                    yield return new RawCommit(
                        messageBuilder.ToString().TrimStart().TrimEnd(),
                        diffBuilder.ToString().TrimStart().TrimEnd()
                    );
                }

                messageBuilder.Clear();
                diffBuilder.Clear();
                isCollectingMessage = false;
                isCollectingDiff = false;
            }
            else if (line.StartsWith("Author: ") || line.StartsWith("Date: "))
            {
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
            yield return new RawCommit(
                messageBuilder.ToString().TrimStart().TrimEnd(),
                diffBuilder.ToString().TrimStart().TrimEnd()
            );
        }
    }
}
