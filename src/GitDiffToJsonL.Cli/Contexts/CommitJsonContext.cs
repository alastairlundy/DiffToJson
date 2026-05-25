using System.Text.Json.Serialization;

namespace GitDiffToJsonLCli.Contexts;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CommitRecord))]
public partial class CommitJsonContext : JsonSerializerContext
{
}