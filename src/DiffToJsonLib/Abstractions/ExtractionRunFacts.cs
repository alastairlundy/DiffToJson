namespace DiffToJsonLib.Abstractions;

/// <summary>
/// Success facts returned by an extraction run for adapter rendering.
/// </summary>
public sealed record ExtractionRunFacts(string OutputPath, string License, int RecordCount);