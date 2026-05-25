using System.Text.Json.Serialization;

namespace GitDiffToJsonL.Cli.Contexts;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CommitRecord))]
public partial class CommitJsonContext : JsonSerializerContext
{
}