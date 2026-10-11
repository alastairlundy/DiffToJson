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

namespace DiffToJsonCli.Execution;

/// <summary>
/// Single source of truth for the command's exit-code table, consumed by
/// <c>Program.cs</c> and asserted directly by tests so the two cannot drift.
/// </summary>
public static class ExitCode
{
    /// <summary>The run completed and the output file was written.</summary>
    public const int Success = 0;

    /// <summary>The run failed or an unexpected error occurred.</summary>
    public const int RunFailure = 1;

    /// <summary>Usage error: invalid or incompatible option values.</summary>
    public const int UsageError = 2;

    /// <summary>The run was cancelled (Ctrl+C / process termination).</summary>
    public const int Cancelled = 130;

    /// <summary>
    /// Maps a pipeline or adapter exception to its exit code.
    /// Usage errors (<see cref="ArgumentException"/>, <see cref="NotSupportedException"/>,
    /// <see cref="KeyNotFoundException"/> for unknown prompt presets) exit 2;
    /// cancellation exits 130; everything else exits 1.
    /// </summary>
    public static int FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => Cancelled,
            ArgumentException => UsageError,
            KeyNotFoundException => UsageError,
            NotSupportedException => UsageError,
            _ => RunFailure
        };
    }
}
