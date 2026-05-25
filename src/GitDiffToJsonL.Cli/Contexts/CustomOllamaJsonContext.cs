using System.Text.Json.Serialization;
using OllamaSharp.Models.Chat;

namespace GitDiffToJsonLCli.Contexts;

[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ChatResponseStream))]
[JsonSerializable(typeof(ChatDoneResponseStream))]
public partial class CustomOllamaJsonContext : JsonSerializerContext
{
    
}