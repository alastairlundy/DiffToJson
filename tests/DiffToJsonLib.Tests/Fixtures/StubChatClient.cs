using Microsoft.Extensions.AI;

namespace DiffToJsonLib.Tests.Fixtures;

public class StubChatClient : IChatClient
{
    private readonly string? _cannedResponse;

    public StubChatClient(string? cannedResponse)
    {
        _cannedResponse = cannedResponse;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ChatMessage message = new(ChatRole.Assistant, _cannedResponse ?? string.Empty);
        ChatResponse response = new([message]);
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }

    object? IChatClient.GetService(Type serviceType, object? serviceKey)
    {
        return null;
    }
}
