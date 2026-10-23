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
/// Input for a training extraction run. Flat record with no shared base.
/// Common field names stay identical to <see cref="RunRawInput"/>; any new
/// common field is added to both records. Prompt fields live on this record.
/// All strings use empty string to mean unset. No parsing is performed here.
/// </summary>
public sealed record RunTrainingInput(
    string RepoDirectory,
    string RepoUrl,
    string License,
    string OutputPath,
    string Redaction,
    string ReasoningEffort,
    bool LlmAssistantOutput,
    string LlmOverridePrompt,
    RawValue Provider,
    RawValue Credential,
    RawValue Endpoint,
    RawValue Model,
    string PromptStyle,
    string SystemPromptOverride,
    string UserPromptOverride);