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
using System.Text;
using System.Text.Json;

namespace DiffToJsonLib.Writers;

public sealed class DiffJsonFileWriter : IDiffJsonFileWriter
{
    private static readonly ReadOnlyMemory<byte> NewLineUtf8 = Encoding.UTF8.GetBytes(Environment.NewLine);

    private readonly IFileSystem _fileSystem;

    public DiffJsonFileWriter(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public async Task WriteToJsonFileAsync(IAsyncEnumerable<CommitRecord> commits, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        StreamWriter streamWriter = _fileSystem.File.CreateText(filePath);
        await using (streamWriter.ConfigureAwait(false))
        {
            await foreach (CommitRecord record in commits.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(streamWriter.BaseStream, record, CommitJsonContext.Default.CommitRecord, cancellationToken).ConfigureAwait(false);
                await streamWriter.BaseStream.WriteAsync(NewLineUtf8, cancellationToken).ConfigureAwait(false);
            }
        
            streamWriter.Close();
        }
    }

    public async Task WriteToJsonFileAsync(ICollection<CommitRecord> commits, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        StreamWriter streamWriter = _fileSystem.File.CreateText(filePath);
        await using (streamWriter.ConfigureAwait(false))
        {
            foreach (CommitRecord record in commits)
            {
                await JsonSerializer.SerializeAsync(streamWriter.BaseStream, record, CommitJsonContext.Default.CommitRecord, cancellationToken).ConfigureAwait(false);
                await streamWriter.BaseStream.WriteAsync(NewLineUtf8, cancellationToken).ConfigureAwait(false);
            }
        
            streamWriter.Close();
        }
    }
}