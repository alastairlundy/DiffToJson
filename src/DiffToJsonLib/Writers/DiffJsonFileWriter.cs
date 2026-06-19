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

using System.Text.Json;

namespace DiffToJsonLib.Writers;

public class DiffJsonFileWriter : IDiffJsonFileWriter
{
    public async Task WriteToJsonFileAsync(IAsyncEnumerable<CommitRecord> commits, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        await using StreamWriter streamWriter =  new(filePath);
        
        await foreach (CommitRecord record in commits.WithCancellation(cancellationToken))
        {
            string json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);

            await streamWriter.WriteLineAsync(json);
        }
        
        streamWriter.Close();
    }

    public async Task WriteToJsonFileAsync(ICollection<CommitRecord> commits, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        await using StreamWriter streamWriter =  new(filePath);
        
        foreach (CommitRecord record in commits)
        {
            string json = JsonSerializer.Serialize(record, CommitJsonContext.Default.CommitRecord);

            await streamWriter.WriteLineAsync(json);
        }
        
        streamWriter.Close();
    }
}