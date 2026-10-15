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