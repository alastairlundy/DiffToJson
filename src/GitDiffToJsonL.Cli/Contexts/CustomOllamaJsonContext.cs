using System.Text.Json.Serialization;
using OllamaSharp.Models.Chat;

namespace GitDiffToJsonL.Cli.Contexts;

[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ChatResponseStream))]
[JsonSerializable(typeof(ChatDoneResponseStream))]
public partial class CustomOllamaJsonContext : JsonSerializerContext
{
    
}