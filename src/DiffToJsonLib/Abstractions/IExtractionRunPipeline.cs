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

/// <summary>
/// Testable extraction run pipeline. One interface carries both record streams
/// so one fake covers them in tests. Implementations live beside their caller;
/// this contract has zero command line references.
/// </summary>
public interface IExtractionRunPipeline
{
    /// <summary>
    /// Runs a raw extraction and writes one JSONL file.
    /// </summary>
    /// <param name="input">Raw input record with plain values; empty string means unset.</param>
    /// <param name="progress">Per-call progress sink; raw runs never report <see cref="RunPhase.BuildingExamples"/>.</param>
    /// <param name="cancellationToken">Cancellation token, last.</param>
    /// <returns>Output path plus resolved license plus record count.</returns>
    /// <exception cref="ArgumentException">Thrown for invalid input, including translated prompt placeholder rejections.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the run cannot complete.</exception>
    /// <exception cref="NotSupportedException">Thrown for model-unsupported options, with the supported list in the message.</exception>
    Task<ExtractionRunFacts> RunRawAsync(
        RunRawInput input,
        IProgress<RunProgress>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a training extraction and writes one JSONL file.
    /// </summary>
    /// <param name="input">Training input record with plain values; empty string means unset.</param>
    /// <param name="progress">Per-call progress sink.</param>
    /// <param name="cancellationToken">Cancellation token, last.</param>
    /// <returns>Output path plus resolved license plus record count.</returns>
    /// <exception cref="ArgumentException">Thrown for invalid input, including translated prompt placeholder rejections.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the run cannot complete.</exception>
    /// <exception cref="NotSupportedException">Thrown for model-unsupported options, with the supported list in the message.</exception>
    Task<ExtractionRunFacts> RunTrainingAsync(
        RunTrainingInput input,
        IProgress<RunProgress>? progress,
        CancellationToken cancellationToken = default);
}
