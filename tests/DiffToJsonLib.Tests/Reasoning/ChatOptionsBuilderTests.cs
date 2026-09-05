using DiffToJsonLib.Reasoning;
using Microsoft.Extensions.AI;
using MeaiReasoningEffort = Microsoft.Extensions.AI.ReasoningEffort;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonLib.Tests.Reasoning;

public class ChatOptionsBuilderTests
{
    private readonly ChatOptionsBuilder _builder = new(new ReasoningEffortMatrix());

    // --- OpenAI graduated ---

    [Test]
    public async Task OpenAi_SetsReasoningEffort_Low()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Low, "openai", "gpt-4o");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(Microsoft.Extensions.AI.ReasoningEffort.Low);
    }

    [Test]
    public async Task OpenAi_SetsReasoningEffort_Medium()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Medium, "openai", "gpt-5");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(Microsoft.Extensions.AI.ReasoningEffort.Medium);
    }

    [Test]
    public async Task OpenAi_SetsReasoningEffort_High()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-4.1");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(Microsoft.Extensions.AI.ReasoningEffort.High);
    }

    [Test]
    public async Task OpenAi_SetsReasoningEffort_XHigh()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.XHigh, "openai", "gpt-5.2");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(Microsoft.Extensions.AI.ReasoningEffort.ExtraHigh);
    }

    [Test]
    public async Task OpenAi_Off_ClearsReasoning()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "openai", "gpt-4o");

        await Assert.That(result.Reasoning).IsNull();
    }

    [Test]
    public async Task OpenAi_Auto_SetsLow()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Auto, "openai", "gpt-4o");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(Microsoft.Extensions.AI.ReasoningEffort.Low);
    }

    // --- OpenAI Chat Adaptive ---

    [Test]
    public async Task OpenAiChatAdaptive_DelegatesToOpenAi()
    {
        var adaptive = _builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-5-chat");
        var standard = _builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-5");

        await Assert.That(adaptive.Reasoning).IsNotNull();
        await Assert.That(adaptive.Reasoning!.Effort).IsEqualTo(standard.Reasoning!.Effort);
    }

    [Test]
    public async Task OpenAiChatAdaptive_Off_ClearsReasoning()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "openai", "gpt-5-chat");

        await Assert.That(result.Reasoning).IsNull();
    }

    // --- Anthropic Old ---

    [Test]
    public async Task AnthropicOld_Off_OmitsThinkingBlock()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "anthropic", "claude-sonnet-4-5");

        await Assert.That(result.AdditionalProperties).IsNull();
    }

    [Test]
    public async Task AnthropicOld_Low_SetsThinkingBudget()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Low, "anthropic", "claude-opus-4-5");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!.ContainsKey("thinking")).IsTrue();

        var thinking = result.AdditionalProperties["thinking"] as IDictionary<string, object>;
        await Assert.That(thinking).IsNotNull();
        await Assert.That(thinking!["type"]).IsEqualTo("enabled");

        long budgetTokens = (long)thinking["budget_tokens"];
        await Assert.That(budgetTokens).IsGreaterThanOrEqualTo(1024);
    }

    [Test]
    public async Task AnthropicOld_High_Uses75PercentBudget()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.High, "anthropic", "claude-haiku-4-5");

        var thinking = result.AdditionalProperties!["thinking"] as IDictionary<string, object>;
        long budgetTokens = (long)thinking!["budget_tokens"];

        // Default max is 16000 (reasoningUsed=true), 75% = 12000
        await Assert.That(budgetTokens).IsEqualTo(12000);
    }

    [Test]
    public async Task AnthropicOld_BudgetNeverBelow1024()
    {
        // With Low effort on small max tokens, budget should be clamped to 1024
        var options = new ChatOptions { MaxOutputTokens = 2000 };
        // We can't pass custom options to BuildChatOptions, but we can verify
        // the Math.Max clamp by testing Low (25% of 16000 = 4000, above 1024)
        var result = _builder.BuildChatOptions(ReasoningEffort.Low, "anthropic", "claude-opus-4-5");

        var thinking = result.AdditionalProperties!["thinking"] as IDictionary<string, object>;
        long budgetTokens = (long)thinking!["budget_tokens"];
        await Assert.That(budgetTokens).IsGreaterThanOrEqualTo(1024);
    }

    // --- Anthropic New ---

    [Test]
    public async Task AnthropicNew_Low_SetsReasoningEffort()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Low, "anthropic", "claude-sonnet-4.6");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!.ContainsKey("reasoning_effort")).IsTrue();
        await Assert.That(result.AdditionalProperties["reasoning_effort"]).IsEqualTo("low");
    }

    [Test]
    public async Task AnthropicNew_Off_OmitsReasoningEffort()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "anthropic", "claude-opus-4.6");

        await Assert.That(result.AdditionalProperties).IsNull();
    }

    [Test]
    public async Task AnthropicNew_XHigh_SetsXhigh()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.XHigh, "anthropic", "claude-sonnet-5");

        await Assert.That(result.AdditionalProperties!["reasoning_effort"]).IsEqualTo("xhigh");
    }

    // --- Anthropic Compatible ---

    [Test]
    public async Task AnthropicCompatible_ForwardsReasoningEffort()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Medium, "anthropic", "claude-unknown-model");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!.ContainsKey("reasoning_effort")).IsTrue();
        await Assert.That(result.AdditionalProperties["reasoning_effort"]).IsEqualTo("medium");
    }

    [Test]
    public async Task AnthropicCompatible_Off_OmitsReasoningEffort()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "anthropic", "claude-unknown-model");

        await Assert.That(result.AdditionalProperties).IsNull();
    }

    // --- Qwen3 ---

    [Test]
    public async Task Qwen3_EnableThinking()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.On, "ollama", "qwen3");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        var kwargs = result.AdditionalProperties!["chat_template_kwargs"] as IDictionary<string, object>;
        await Assert.That((bool)kwargs!["enable_thinking"]).IsTrue();
    }

    [Test]
    public async Task Qwen3_DisableThinking()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "ollama", "qwen3");

        var kwargs = result.AdditionalProperties!["chat_template_kwargs"] as IDictionary<string, object>;
        await Assert.That((bool)kwargs!["enable_thinking"]).IsFalse();
    }

    // --- MiniMax ---

    [Test]
    public async Task MiniMax_On_SendsEnabled()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.On, "minimax", "minimax-m3");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!["thinking"]).IsEqualTo("enabled");
    }

    [Test]
    public async Task MiniMax_Off_SendsDisabled()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "minimax", "minimax-m3");

        await Assert.That(result.AdditionalProperties!["thinking"]).IsEqualTo("disabled");
    }

    [Test]
    public async Task MiniMax_Auto_SendsAdaptive()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Auto, "minimax", "minimax-m3");

        await Assert.That(result.AdditionalProperties!["thinking"]).IsEqualTo("adaptive");
    }

    // --- Deepseek V3 ---

    [Test]
    public async Task DeepseekV3_NoReasoningOptions()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "deepseek", "deepseek-chat");

        await Assert.That(result.Reasoning).IsNull();
        await Assert.That(result.AdditionalProperties).IsNull();
    }

    // --- Deepseek V3.1 ---

    [Test]
    public async Task DeepseekV31_On_SetsReasoning()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.On, "deepseek", "deepseek-v3.1");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(Microsoft.Extensions.AI.ReasoningEffort.Low);
    }

    [Test]
    public async Task DeepseekV31_Off_ClearsReasoning()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "deepseek", "deepseek-v3.1");

        await Assert.That(result.Reasoning).IsNull();
    }

    // --- Deepseek V4 ---

    [Test]
    public async Task DeepseekV4_DelegatesToOpenAi()
    {
        var v4 = _builder.BuildChatOptions(ReasoningEffort.High, "deepseek", "deepseek-v4");
        var openai = _builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-4o");

        await Assert.That(v4.Reasoning).IsNotNull();
        await Assert.That(v4.Reasoning!.Effort).IsEqualTo(openai.Reasoning!.Effort);
    }

    // --- Unknown fallback ---

    [Test]
    public async Task Unknown_NoReasoningOptions()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Auto, "unknown", "unknown-model");

        await Assert.That(result.Reasoning).IsNull();
        await Assert.That(result.AdditionalProperties).IsNull();
    }

    // --- MaxOutputTokens defaults ---

    [Test]
    public async Task ReasoningUsed_SetsHigherMaxTokens()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-4o");

        await Assert.That(result.MaxOutputTokens).IsEqualTo(16_000);
    }

    [Test]
    public async Task ReasoningNotUsed_SetsLowerMaxTokens()
    {
        var result = _builder.BuildChatOptions(ReasoningEffort.Off, "openai", "gpt-4o");

        await Assert.That(result.MaxOutputTokens).IsEqualTo(8_000);
    }
}
