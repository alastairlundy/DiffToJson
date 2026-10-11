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
/// Carries a raw option value plus its environment fallback.
/// Empty string means unset on both halves.
/// The effective value resolves with option winning over environment.
/// </summary>
public sealed record RawValue(string OptionValue, string EnvironmentValue)
{
    /// <summary>
    /// Gets the resolved effective value with option winning over environment.
    /// Empty string means unset.
    /// </summary>
    public string EffectiveValue =>
        !string.IsNullOrEmpty(OptionValue) ? OptionValue : (EnvironmentValue ?? string.Empty);

    /// <summary>
    /// Resolves the effective value with option winning over environment.
    /// Empty string means unset on both halves.
    /// </summary>
    public static string Resolve(string optionValue, string environmentValue) =>
        !string.IsNullOrEmpty(optionValue) ? optionValue : (environmentValue ?? string.Empty);

    /// <summary>
    /// Creates a <see cref="RawValue"/> from raw option and environment halves.
    /// </summary>
    public static RawValue From(string optionValue, string environmentValue) =>
        new(optionValue ?? string.Empty, environmentValue ?? string.Empty);
}

/// <summary>
/// Input for a raw extraction run. Flat record with no shared base.
/// All strings use empty string to mean unset. No parsing is performed here.
/// </summary>
public sealed record RunRawInput(
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
    RawValue Model);

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
