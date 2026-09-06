using DiffToJsonLib.Reasoning;
using Microsoft.Extensions.AI;
using ModelsDotDevSharp;
using MeaiReasoningEffort = Microsoft.Extensions.AI.ReasoningEffort;
using ReasoningEffort = DiffToJsonLib.Reasoning.ReasoningEffort;

namespace DiffToJsonLib.Tests.Reasoning;

public class ChatOptionsBuilderTests
{
    private static readonly ChatOptionsBuilder Builder;

    static ChatOptionsBuilderTests()
    {
        AIProviderInfo[] providers = CreateTestProviders();
        Builder = new ChatOptionsBuilder(new ModelsDevReasoningEffortMatrix(providers));
    }

    private static AIProviderInfo[] CreateTestProviders()
    {
        return
        [
            CreateProvider("openai",
            [
                CreateModel("gpt-4o", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-4o-mini", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-4.1", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-4.1-mini", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-4.1-nano", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-4.5", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-5", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-5.1", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-5.2", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("gpt-5-chat", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
            ]),
            CreateProvider("anthropic",
            [
                CreateModel("claude-sonnet-4-5", true, AIModelReasoningOptionType.BudgetTokens, null),
                CreateModel("claude-opus-4-5", true, AIModelReasoningOptionType.BudgetTokens, null),
                CreateModel("claude-haiku-4-5", true, AIModelReasoningOptionType.BudgetTokens, null),
                CreateModel("claude-sonnet-4.6", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("claude-opus-4.6", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("claude-sonnet-5", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
                CreateModel("claude-unknown-model", true, AIModelReasoningOptionType.Effort, ["low", "medium", "high", "xhigh", "max"]),
            ]),
            CreateProvider("ollama-cloud",
            [
                CreateModel("qwen3", true, AIModelReasoningOptionType.Toggle, null),
            ]),
            CreateProvider("minimax",
            [
                CreateModel("minimax-m3", true, AIModelReasoningOptionType.Toggle, null),
            ]),
            CreateProvider("deepseek",
            [
                CreateModel("deepseek-chat", false, AIModelReasoningOptionType.Toggle, null),
                CreateModel("deepseek-v3", false, AIModelReasoningOptionType.Toggle, null),
                CreateModel("deepseek-v3.1", true, AIModelReasoningOptionType.Effort, ["low"]),
                CreateModel("deepseek-v4", true, AIModelReasoningOptionType.Effort, ["low", "high", "max"]),
            ]),
        ];
    }

    private static AIProviderInfo CreateProvider(string id, AIModelInfo[] models)
    {
        return new AIProviderInfo { Id = id, Models = models };
    }

    private static AIModelInfo CreateModel(string id, bool supportsReasoning, AIModelReasoningOptionType type, string[]? values)
    {
        return new AIModelInfo
        {
            Id = id,
            SupportsReasoning = supportsReasoning,
            ReasoningOptions = new[]
            {
                new AIModelReasoningOption
                {
                    Type = type,
                    Values = values?.ToList()
                }
            }
        };
    }

    // --- OpenAI Effort dispatch ---

    [Test]
    public async Task OpenAi_SetsReasoningEffort_Low()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Low, "openai", "gpt-4o");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(MeaiReasoningEffort.Low);
    }

    [Test]
    public async Task OpenAi_SetsReasoningEffort_Medium()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Medium, "openai", "gpt-5");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(MeaiReasoningEffort.Medium);
    }

    [Test]
    public async Task OpenAi_SetsReasoningEffort_High()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-4.1");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(MeaiReasoningEffort.High);
    }

    [Test]
    public async Task OpenAi_SetsReasoningEffort_XHigh()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.XHigh, "openai", "gpt-5.2");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(MeaiReasoningEffort.ExtraHigh);
    }

    [Test]
    public async Task OpenAi_Off_ClearsReasoning()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "openai", "gpt-4o");

        await Assert.That(result.Reasoning).IsNull();
    }

    [Test]
    public async Task OpenAi_Auto_SetsLow()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Auto, "openai", "gpt-4o");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(MeaiReasoningEffort.Low);
    }

    // --- OpenAI Chat (Effort type) ---

    [Test]
    public async Task OpenAiChat_EffortType_DelegatesToOpenAiEffort()
    {
        var adaptive = Builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-5-chat");
        var standard = Builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-5");

        await Assert.That(adaptive.Reasoning).IsNotNull();
        await Assert.That(adaptive.Reasoning!.Effort).IsEqualTo(standard.Reasoning!.Effort);
    }

    [Test]
    public async Task OpenAiChat_Off_ClearsReasoning()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "openai", "gpt-5-chat");

        await Assert.That(result.Reasoning).IsNull();
    }

    // --- Anthropic BudgetTokens dispatch (old models) ---

    [Test]
    public async Task AnthropicBudget_Off_OmitsThinkingBlock()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "anthropic", "claude-sonnet-4-5");

        await Assert.That(result.AdditionalProperties).IsNull();
    }

    [Test]
    public async Task AnthropicBudget_Low_SetsThinkingBudget()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Low, "anthropic", "claude-opus-4-5");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!.ContainsKey("thinking")).IsTrue();

        var thinking = result.AdditionalProperties["thinking"] as IDictionary<string, object>;
        await Assert.That(thinking).IsNotNull();
        await Assert.That(thinking!["type"]).IsEqualTo("enabled");

        long budgetTokens = (long)thinking["budget_tokens"];
        await Assert.That(budgetTokens).IsGreaterThanOrEqualTo(1024);
    }

    [Test]
    public async Task AnthropicBudget_High_Uses75PercentBudget()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.High, "anthropic", "claude-haiku-4-5");

        var thinking = result.AdditionalProperties!["thinking"] as IDictionary<string, object>;
        long budgetTokens = (long)thinking!["budget_tokens"];

        await Assert.That(budgetTokens).IsEqualTo(12000);
    }

    [Test]
    public async Task AnthropicBudget_BudgetNeverBelow1024()
    {
        var options = new ChatOptions { MaxOutputTokens = 3000 };
        var result = Builder.BuildChatOptions(options, ReasoningEffort.Low, "anthropic", "claude-opus-4-5");

        var thinking = result.AdditionalProperties!["thinking"] as IDictionary<string, object>;
        long budgetTokens = (long)thinking!["budget_tokens"];
        await Assert.That(budgetTokens).IsEqualTo(1024);
    }

    // --- Anthropic Effort dispatch (new models) ---

    [Test]
    public async Task AnthropicEffort_Low_SetsReasoningEffort()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Low, "anthropic", "claude-sonnet-4.6");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!.ContainsKey("reasoning_effort")).IsTrue();
        await Assert.That(result.AdditionalProperties["reasoning_effort"]).IsEqualTo("low");
    }

    [Test]
    public async Task AnthropicEffort_Off_OmitsReasoningEffort()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "anthropic", "claude-opus-4.6");

        await Assert.That(result.AdditionalProperties).IsNull();
    }

    [Test]
    public async Task AnthropicEffort_XHigh_SetsXhigh()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.XHigh, "anthropic", "claude-sonnet-5");

        await Assert.That(result.AdditionalProperties!["reasoning_effort"]).IsEqualTo("xhigh");
    }

    // --- Anthropic Effort dispatch (compatible models) ---

    [Test]
    public async Task AnthropicEffort_Compat_ForwardsReasoningEffort()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Medium, "anthropic", "claude-unknown-model");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!.ContainsKey("reasoning_effort")).IsTrue();
        await Assert.That(result.AdditionalProperties["reasoning_effort"]).IsEqualTo("medium");
    }

    [Test]
    public async Task AnthropicEffort_Compat_Off_OmitsReasoningEffort()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "anthropic", "claude-unknown-model");

        await Assert.That(result.AdditionalProperties).IsNull();
    }

    // --- Qwen3 Toggle dispatch ---

    [Test]
    public async Task Toggle_Qwen3_EnableThinking()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.On, "ollama", "qwen3");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        var kwargs = result.AdditionalProperties!["chat_template_kwargs"] as IDictionary<string, object>;
        await Assert.That((bool)kwargs!["enable_thinking"]).IsTrue();
    }

    [Test]
    public async Task Toggle_Qwen3_DisableThinking()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "ollama", "qwen3");

        var kwargs = result.AdditionalProperties!["chat_template_kwargs"] as IDictionary<string, object>;
        await Assert.That((bool)kwargs!["enable_thinking"]).IsFalse();
    }

    // --- MiniMax Toggle dispatch ---

    [Test]
    public async Task Toggle_MiniMax_On_SendsEnabled()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.On, "minimax", "minimax-m3");

        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!["thinking"]).IsEqualTo("enabled");
    }

    [Test]
    public async Task Toggle_MiniMax_Off_SendsDisabled()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "minimax", "minimax-m3");

        await Assert.That(result.AdditionalProperties!["thinking"]).IsEqualTo("disabled");
    }

    [Test]
    public async Task Toggle_MiniMax_Auto_SendsAdaptive()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Auto, "minimax", "minimax-m3");

        await Assert.That(result.AdditionalProperties!["thinking"]).IsEqualTo("adaptive");
    }

    // --- Deepseek V3 Toggle dispatch ---

    [Test]
    public async Task DeepseekV3_Off_SendsDisabled()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "deepseek", "deepseek-chat");

        await Assert.That(result.Reasoning).IsNull();
        await Assert.That(result.AdditionalProperties).IsNotNull();
        await Assert.That(result.AdditionalProperties!["thinking"]).IsEqualTo("disabled");
    }

    // --- Deepseek V3.1 Effort dispatch ---

    [Test]
    public async Task DeepseekV31_On_SetsReasoning()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.On, "deepseek", "deepseek-v3.1");

        await Assert.That(result.Reasoning).IsNotNull();
        await Assert.That(result.Reasoning!.Effort).IsEqualTo(MeaiReasoningEffort.Low);
    }

    [Test]
    public async Task DeepseekV31_Off_ClearsReasoning()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "deepseek", "deepseek-v3.1");

        await Assert.That(result.Reasoning).IsNull();
    }

    // --- Deepseek V4 Effort dispatch ---

    [Test]
    public async Task DeepseekV4_DelegatesToOpenAiEffort()
    {
        var v4 = Builder.BuildChatOptions(ReasoningEffort.High, "deepseek", "deepseek-v4");
        var openai = Builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-4o");

        await Assert.That(v4.Reasoning).IsNotNull();
        await Assert.That(v4.Reasoning!.Effort).IsEqualTo(openai.Reasoning!.Effort);
    }

    // --- Unknown fallback (null reasoning type) ---

    [Test]
    public async Task Unknown_NoReasoningOptions()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Auto, "unknown", "unknown-model");

        await Assert.That(result.Reasoning).IsNull();
        await Assert.That(result.AdditionalProperties).IsNull();
    }

    // --- MaxOutputTokens defaults ---

    [Test]
    public async Task ReasoningUsed_SetsHigherMaxTokens()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.High, "openai", "gpt-4o");

        await Assert.That(result.MaxOutputTokens).IsEqualTo(16_000);
    }

    [Test]
    public async Task ReasoningNotUsed_SetsLowerMaxTokens()
    {
        var result = Builder.BuildChatOptions(ReasoningEffort.Off, "openai", "gpt-4o");

        await Assert.That(result.MaxOutputTokens).IsEqualTo(8_000);
    }
}
