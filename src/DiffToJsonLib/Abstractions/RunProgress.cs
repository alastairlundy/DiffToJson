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
/// Progress payload carrying a phase plus a human-readable message.
/// The adapter renders all console text from these values.
/// </summary>
public sealed record RunProgress(RunPhase Phase, string Message);

/// <summary>
/// Success facts returned by an extraction run for adapter rendering.
/// </summary>
public sealed record ExtractionRunFacts(string OutputPath, string License, int RecordCount);
