using Microsoft.Extensions.AI;

namespace DiffToJsonLib.Training.Abstractions;

public interface IAssistantMessageGenerator
{
    Task<AssistantMessageResult> GenerateAsync(string systemPrompt, string userPrompt, CommitRecord redactedCommit, ChatOptions options, CancellationToken cancellationToken);
}
