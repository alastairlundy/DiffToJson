using DiffToJsonLib.Reasoning;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonLib.Tests.Reasoning;

public class ReasoningEffortMatrixTests
{
    private readonly ReasoningEffortMatrix _matrix = new();

    private static readonly HashSet<ReasoningEffort> FullSet = new()
    {
        ReasoningEffort.Auto, ReasoningEffort.On, ReasoningEffort.Off,
        ReasoningEffort.Low, ReasoningEffort.Medium, ReasoningEffort.High,
        ReasoningEffort.XHigh, ReasoningEffort.Max
    };

    private static readonly HashSet<ReasoningEffort> AnthropicOldSet = new()
    {
        ReasoningEffort.Auto, ReasoningEffort.On, ReasoningEffort.Off,
        ReasoningEffort.Low, ReasoningEffort.Medium, ReasoningEffort.High
    };

    private static readonly HashSet<ReasoningEffort> DeepseekV4Set = new()
    {
        ReasoningEffort.Auto, ReasoningEffort.On, ReasoningEffort.Off,
        ReasoningEffort.Low, ReasoningEffort.Medium, ReasoningEffort.High,
        ReasoningEffort.Max
    };

    private static readonly HashSet<ReasoningEffort> BinaryThinkingSet = new()
    {
        ReasoningEffort.Auto, ReasoningEffort.On, ReasoningEffort.Off
    };

    private static readonly HashSet<ReasoningEffort> OffOnlySet = new()
    {
        ReasoningEffort.Auto, ReasoningEffort.Off
    };

    private static readonly HashSet<ReasoningEffort> AutoOnlySet = new()
    {
        ReasoningEffort.Auto
    };

    private static async Task AssertSetsEqual(IReadOnlySet<ReasoningEffort> actual, HashSet<ReasoningEffort> expected)
    {
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        foreach (var value in expected)
        {
            await Assert.That(actual.Contains(value)).IsTrue();
        }
    }

    // --- Known models return exact sets ---

    [Test]
    public async Task Gpt4o_ReturnsFullSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("gpt-4o");
        AssertSetsEqual(supported, FullSet);
    }

    [Test]
    public async Task Gpt5Chat_ReturnsFullSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("gpt-5-chat");
        AssertSetsEqual(supported, FullSet);
    }

    [Test]
    public async Task ClaudeSonnet45_ReturnsAnthropicOldSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("claude-sonnet-4-5");
        AssertSetsEqual(supported, AnthropicOldSet);
    }

    [Test]
    public async Task DeepseekChat_ReturnsOffOnlySet()
    {
        var supported = _matrix.GetSupportedReasoningValues("deepseek-chat");
        AssertSetsEqual(supported, OffOnlySet);
    }

    [Test]
    public async Task DeepseekV31_ReturnsBinaryThinkingSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("deepseek-v3.1");
        AssertSetsEqual(supported, BinaryThinkingSet);
    }

    [Test]
    public async Task DeepseekV4_ReturnsDeepseekV4Set()
    {
        var supported = _matrix.GetSupportedReasoningValues("deepseek-v4");
        AssertSetsEqual(supported, DeepseekV4Set);
    }

    [Test]
    public async Task MiniMaxM3_ReturnsBinaryThinkingSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("minimax-m3");
        AssertSetsEqual(supported, BinaryThinkingSet);
    }

    // --- Unknown models ---

    [Test]
    public async Task UnknownModel_ReturnsAutoOnly()
    {
        var supported = _matrix.GetSupportedReasoningValues("nonexistent-model");
        AssertSetsEqual(supported, AutoOnlySet);
    }

    // --- ProducesReasoningOnAuto ---

    [Test]
    public async Task Gpt4o_ProducesReasoningOnAuto()
    {
        await Assert.That(_matrix.ProducesReasoningOnAuto("gpt-4o")).IsTrue();
    }

    [Test]
    public async Task DeepseekChat_DoesNotProduceReasoningOnAuto()
    {
        await Assert.That(_matrix.ProducesReasoningOnAuto("deepseek-chat")).IsFalse();
    }

    [Test]
    public async Task MiniMaxM1_DoesNotProduceReasoningOnAuto()
    {
        await Assert.That(_matrix.ProducesReasoningOnAuto("minimax-m1")).IsFalse();
    }

    [Test]
    public async Task MiniMaxM3_DoesNotProduceReasoningOnAuto()
    {
        await Assert.That(_matrix.ProducesReasoningOnAuto("minimax-m3")).IsFalse();
    }

    [Test]
    public async Task DeepseekV31_ProducesReasoningOnAuto()
    {
        await Assert.That(_matrix.ProducesReasoningOnAuto("deepseek-v3.1")).IsTrue();
    }

    [Test]
    public async Task UnknownModel_DoesNotProduceReasoningOnAuto()
    {
        await Assert.That(_matrix.ProducesReasoningOnAuto("unknown-model")).IsFalse();
    }

    // --- Case insensitivity ---

    [Test]
    public async Task CaseInsensitive_Lookup()
    {
        var upper = _matrix.GetSupportedReasoningValues("GPT-4O");
        var lower = _matrix.GetSupportedReasoningValues("gpt-4o");

        await Assert.That(upper.Count).IsEqualTo(lower.Count);
        foreach (var value in lower)
        {
            await Assert.That(upper.Contains(value)).IsTrue();
        }
    }
}
