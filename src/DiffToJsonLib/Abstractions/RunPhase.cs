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