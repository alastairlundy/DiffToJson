using DiffToJsonLib.Abstractions;
using DiffToJsonLib.Models;
using DiffToJsonLib.Redactors;
using DiffToJsonLib.Tests.Fixtures;
using DiffToJsonLib.Training;
using DiffToJsonLib.Training.Abstractions;
using DiffToJsonLib.Writers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Compliance.Redaction;

namespace DiffToJsonLib.Tests.Training;

public class LlmAssistantWriterTests
{
    private static readonly CommitRecord SampleCommit = new(
        Diff: "some diff",
        CommitMessage: "original message",
        RepoName: "test/repo",
        License: "MIT",
        RepoUrl: "https://example.com");

    private sealed class StubChatClientFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create() => client;
    }

    private sealed class ThrowingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("chat failed");
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

        object? IChatClient.GetService(Type serviceType, object? serviceKey) => null;
    }

    private sealed class EmptyResponseChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse([]));
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

        object? IChatClient.GetService(Type serviceType, object? serviceKey) => null;
    }

    private sealed class OptionsCapturingChatClient : IChatClient
    {
        public ChatOptions? CapturedOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CapturedOptions = options;
            ChatMessage message = new(ChatRole.Assistant, "response");
            return Task.FromResult(new ChatResponse([message]));
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

        object? IChatClient.GetService(Type serviceType, object? serviceKey) => null;
    }

    private sealed class TestRedactor : Redactor
    {
        private readonly string _replacement;

        public TestRedactor(string replacement) => _replacement = replacement;

        public override string Redact(string? source) =>
            source is null ? string.Empty : _replacement;

        public override int GetRedactedLength(ReadOnlySpan<char> source) => _replacement.Length;

        public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
        {
            _replacement.AsSpan().CopyTo(destination);
            return _replacement.Length;
        }
    }

    [Test]
    public async Task GenerateAsync_Success_ReturnsAssistantMessageGenerated()
    {
        var client = new StubChatClient("hello world");
        var factory = new StubChatClientFactory(client);
        var policy = new RedactionPolicy(new Dictionary<RedactionTier, Redactor>());
        var writer = new LlmAssistantWriter(factory, policy);

        AssistantMessageResult result = await writer.GenerateAsync(
            "system", "user", SampleCommit, new ChatOptions(), CancellationToken.None);

        using var _ = Assert.Multiple();
        await Assert.That(result)
            .IsTypeOf<AssistantMessageResult.AssistantMessageGenerated>();
        var generated = (AssistantMessageResult.AssistantMessageGenerated)result;
        await Assert.That(generated.Content).IsEqualTo("hello world");
        await Assert.That(generated.OriginalAssistantMessage).IsEqualTo("original message");
    }

    [Test]
    public async Task GenerateAsync_ExceptionInChatCall_ReturnsAttemptedAndFailed()
    {
        var client = new ThrowingChatClient();
        var factory = new StubChatClientFactory(client);
        var policy = new RedactionPolicy(new Dictionary<RedactionTier, Redactor>());
        var writer = new LlmAssistantWriter(factory, policy);

        AssistantMessageResult result = await writer.GenerateAsync(
            "system", "user", SampleCommit, new ChatOptions(), CancellationToken.None);

        using var _ = Assert.Multiple();
        await Assert.That(result)
            .IsTypeOf<AssistantMessageResult.AssistantMessageAttemptedAndFailed>();
        var failed = (AssistantMessageResult.AssistantMessageAttemptedAndFailed)result;
        await Assert.That(failed.FallbackContent).IsNull();
        await Assert.That(failed.OriginalAssistantMessage).IsEqualTo("original message");
    }

    [Test]
    public async Task GenerateAsync_NoAssistantMessageInResponse_ReturnsAttemptedAndFailed()
    {
        var client = new EmptyResponseChatClient();
        var factory = new StubChatClientFactory(client);
        var policy = new RedactionPolicy(new Dictionary<RedactionTier, Redactor>());
        var writer = new LlmAssistantWriter(factory, policy);

        AssistantMessageResult result = await writer.GenerateAsync(
            "system", "user", SampleCommit, new ChatOptions(), CancellationToken.None);

        using var _ = Assert.Multiple();
        await Assert.That(result)
            .IsTypeOf<AssistantMessageResult.AssistantMessageAttemptedAndFailed>();
        var failed = (AssistantMessageResult.AssistantMessageAttemptedAndFailed)result;
        await Assert.That(failed.FallbackContent).IsNull();
        await Assert.That(failed.OriginalAssistantMessage).IsEqualTo("original message");
    }

    [Test]
    public async Task GenerateAsync_RedactionAllTier_RedactsLlmOutput()
    {
        var client = new StubChatClient("sensitive data");
        var factory = new StubChatClientFactory(client);
        var redactor = new TestRedactor("[REDACTED]");
        var policy = new RedactionPolicy(
            new Dictionary<RedactionTier, Redactor> { [RedactionTier.All] = redactor });
        var writer = new LlmAssistantWriter(factory, policy);

        AssistantMessageResult result = await writer.GenerateAsync(
            "system", "user", SampleCommit, new ChatOptions(), CancellationToken.None);

        var generated = (AssistantMessageResult.AssistantMessageGenerated)result;
        await Assert.That(generated.Content).IsEqualTo("[REDACTED]");
    }

    [Test]
    public async Task GenerateAsync_NoAllTierInPolicy_DoesNotRedactLlmOutput()
    {
        var client = new StubChatClient("sensitive data");
        var factory = new StubChatClientFactory(client);
        var redactor = new TestRedactor("[REDACTED]");
        var policy = new RedactionPolicy(
            new Dictionary<RedactionTier, Redactor> { [RedactionTier.Message] = redactor });
        var writer = new LlmAssistantWriter(factory, policy);

        AssistantMessageResult result = await writer.GenerateAsync(
            "system", "user", SampleCommit, new ChatOptions(), CancellationToken.None);

        var generated = (AssistantMessageResult.AssistantMessageGenerated)result;
        await Assert.That(generated.Content).IsEqualTo("sensitive data");
    }

    [Test]
    public async Task GenerateAsync_ForwardsChatOptionsToClient()
    {
        var client = new OptionsCapturingChatClient();
        var factory = new StubChatClientFactory(client);
        var policy = new RedactionPolicy(new Dictionary<RedactionTier, Redactor>());
        var writer = new LlmAssistantWriter(factory, policy);

        var expectedOptions = new ChatOptions
        {
            ModelId = "test-model",
            Temperature = 0.5f
        };

        await writer.GenerateAsync(
            "system", "user", SampleCommit, expectedOptions, CancellationToken.None);

        await Assert.That(client.CapturedOptions).IsNotNull();
        await Assert.That(client.CapturedOptions!.ModelId).IsEqualTo("test-model");
        await Assert.That(client.CapturedOptions!.Temperature).IsEqualTo(0.5f);
    }
}
