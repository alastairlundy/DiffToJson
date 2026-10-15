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

using System.IO.Abstractions;
using System.Text.Json;

namespace DiffToJsonLib.Writers;

public class DiffTrainingJsonFileWriter : IDiffTrainingJsonFileWriter
{
    private readonly IFileSystem _fileSystem;

    public DiffTrainingJsonFileWriter(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public async Task WriteToJsonFileAsync(IAsyncEnumerable<CommitTrainingRecord> records, string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        StreamWriter streamWriter = _fileSystem.File.CreateText(filePath);
        await using (streamWriter.ConfigureAwait(false))
        {
            await foreach (CommitTrainingRecord record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                string json = JsonSerializer.Serialize(record, CommitTrainingJsonContext.Default.CommitTrainingRecord);

                await streamWriter.WriteLineAsync(json).ConfigureAwait(false);
            }

            streamWriter.Close();
        }
    }
}
