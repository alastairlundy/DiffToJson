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
/// Phases reported by an extraction run. Raw runs never report <see cref="BuildingExamples"/>.
/// <see cref="Warning"/> carries non-fatal diagnostics such as dropped safety warnings
/// or degraded capability data; adapters should route it to standard error.
/// </summary>
public enum RunPhase
{
    ResolvingLicense,
    ParsingCommits,
    BuildingExamples,
    WritingOutput,
    Warning,
}