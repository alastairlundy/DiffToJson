using System.Text.Json.Serialization;

namespace DiffToJsonLib.Contexts;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CommitTrainingRecord))]
public partial class CommitTrainingJsonContext : JsonSerializerContext
{
}
