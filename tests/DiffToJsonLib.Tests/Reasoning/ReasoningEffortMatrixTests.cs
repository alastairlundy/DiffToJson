using DiffToJsonLib.Reasoning;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonLib.Tests.Reasoning;

public class ReasoningEffortMatrixTests
{
    private readonly ReasoningEffortMatrix _matrix = new();

    // --- Known models return correct sets ---

    [Test]
    public async Task Gpt4o_ReturnsFullSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("gpt-4o");

        await Assert.That(supported).Contains(ReasoningEffort.Auto);
        await Assert.That(supported).Contains(ReasoningEffort.Low);
        await Assert.That(supported).Contains(ReasoningEffort.Medium);
        await Assert.That(supported).Contains(ReasoningEffort.High);
        await Assert.That(supported).Contains(ReasoningEffort.XHigh);
        await Assert.That(supported).Contains(ReasoningEffort.Max);
        await Assert.That(supported).Contains(ReasoningEffort.Off);
    }

    [Test]
    public async Task Gpt5Chat_ReturnsFullSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("gpt-5-chat");

        await Assert.That(supported).Contains(ReasoningEffort.XHigh);
        await Assert.That(supported).Contains(ReasoningEffort.Max);
    }

    [Test]
    public async Task ClaudeSonnet45_ReturnsAnthropicOldSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("claude-sonnet-4-5");

        await Assert.That(supported).Contains(ReasoningEffort.Auto);
        await Assert.That(supported).Contains(ReasoningEffort.Low);
        await Assert.That(supported).Contains(ReasoningEffort.High);
        // AnthropicOldSet does NOT include XHigh or Max
        await Assert.That(supported.Contains(ReasoningEffort.XHigh)).IsFalse();
        await Assert.That(supported.Contains(ReasoningEffort.Max)).IsFalse();
    }

    [Test]
    public async Task DeepseekChat_ReturnsOffOnlySet()
    {
        var supported = _matrix.GetSupportedReasoningValues("deepseek-chat");

        await Assert.That(supported).Contains(ReasoningEffort.Auto);
        await Assert.That(supported).Contains(ReasoningEffort.Off);
        await Assert.That(supported.Contains(ReasoningEffort.Low)).IsFalse();
        await Assert.That(supported.Contains(ReasoningEffort.High)).IsFalse();
    }

    [Test]
    public async Task DeepseekV31_ReturnsBinaryThinkingSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("deepseek-v3.1");

        await Assert.That(supported).Contains(ReasoningEffort.Auto);
        await Assert.That(supported).Contains(ReasoningEffort.On);
        await Assert.That(supported).Contains(ReasoningEffort.Off);
        await Assert.That(supported.Contains(ReasoningEffort.Low)).IsFalse();
    }

    [Test]
    public async Task DeepseekV4_ReturnsDeepseekV4Set()
    {
        var supported = _matrix.GetSupportedReasoningValues("deepseek-v4");

        await Assert.That(supported).Contains(ReasoningEffort.Max);
        await Assert.That(supported.Contains(ReasoningEffort.XHigh)).IsFalse();
    }

    [Test]
    public async Task MiniMaxM3_ReturnsBinaryThinkingSet()
    {
        var supported = _matrix.GetSupportedReasoningValues("minimax-m3");

        await Assert.That(supported).Contains(ReasoningEffort.Auto);
        await Assert.That(supported).Contains(ReasoningEffort.On);
        await Assert.That(supported).Contains(ReasoningEffort.Off);
        await Assert.That(supported.Contains(ReasoningEffort.Low)).IsFalse();
    }

    // --- Unknown models ---

    [Test]
    public async Task UnknownModel_ReturnsAutoOnly()
    {
        var supported = _matrix.GetSupportedReasoningValues("nonexistent-model");

        await Assert.That(supported).Contains(ReasoningEffort.Auto);
        await Assert.That(supported).Count().IsEqualTo(1);
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
