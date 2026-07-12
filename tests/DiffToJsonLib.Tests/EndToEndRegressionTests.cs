using System.Runtime.CompilerServices;
using System.Text.Json;
using DiffToJsonLib.Abstractions;
using DiffToJsonLib.Contexts;
using DiffToJsonLib.Models;
using DiffToJsonLib.Parsing;
using DiffToJsonLib.Prompts;
using DiffToJsonLib.Redactors;
using DiffToJsonLib.Tests.Fixtures;
using DiffToJsonLib.Training;
using DiffToJsonLib.Training.Abstractions;
using DiffToJsonLib.Writers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Compliance.Redaction;

namespace DiffToJsonLib.Tests;

public class EndToEndRegressionTests
{
    private static string FixturePath =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "tests", "DiffToJsonLib.Tests", "Fixtures", "git-log.txt"
        ));

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
            throw new InvalidOperationException("simulated LLM failure");
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

    private static async IAsyncEnumerable<CommitRecord> LoadCommitsFromFixture()
    {
        GitLogParser parser = new();
        using StreamReader reader = new(FixturePath);
        await foreach (RawCommit raw in parser.ParseAsync(reader, default))
        {
            yield return new CommitRecord(
                raw.Diff,
                raw.Message,
                "test/repo",
                "MIT",
                "https://github.com/test/repo");
        }
    }

    private static RedactionPolicy CreateEmptyPolicy()
    {
        return new RedactionPolicy(new Dictionary<RedactionTier, Redactor>());
    }

    [Test]
    public async Task TrainingPath()
    {
        var policy = CreateEmptyPolicy();
        var stubClient = new StubChatClient("This is an AI-generated summary of the changes.");
        var factory = new StubChatClientFactory(stubClient);
        var assistant = new LlmAssistantWriter(factory, policy);
        var builder = new TrainingExampleBuilder(policy, assistant);
        var template = PromptPresets.Get("default");
        var options = new TrainingExampleOptions(
            template, null, true, RedactionTier.None, new ChatOptions());

        var commits = LoadCommitsFromFixture();
        var records = await builder
            .BuildAsync(commits, options, CancellationToken.None)
            .ToListAsync(cancellationToken: CancellationToken.None);

        var jsonLines = records.Select(r =>
            JsonSerializer.Serialize(r, CommitTrainingJsonContext.Default.CommitTrainingRecord));
        var actualJsonl = string.Join(Environment.NewLine, jsonLines);

        await Verify(actualJsonl);
    }

    [Test]
    public async Task LlmFailurePath()
    {
        var policy = CreateEmptyPolicy();
        var failingClient = new ThrowingChatClient();
        var factory = new StubChatClientFactory(failingClient);
        var assistant = new LlmAssistantWriter(factory, policy);
        var builder = new TrainingExampleBuilder(policy, assistant);
        var template = PromptPresets.Get("default");
        var options = new TrainingExampleOptions(
            template, null, true, RedactionTier.None, new ChatOptions());

        var commits = LoadCommitsFromFixture();
        var records = await builder
            .BuildAsync(commits, options, CancellationToken.None)
            .ToListAsync(cancellationToken: CancellationToken.None);

        var jsonLines = records.Select(r =>
            JsonSerializer.Serialize(r, CommitTrainingJsonContext.Default.CommitTrainingRecord));
        var actualJsonl = string.Join(Environment.NewLine, jsonLines);

        await Verify(actualJsonl);
    }
}
