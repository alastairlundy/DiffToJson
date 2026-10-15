namespace DiffToJsonLib.Abstractions;

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