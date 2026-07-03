using Microsoft.Extensions.AI;

namespace DiffToJsonLib.Reasoning;

public sealed class ChatOptionsBuilder : IChatOptionsBuilder
{
    private readonly IReasoningEffortMatrix _matrix;

    public ChatOptionsBuilder(IReasoningEffortMatrix matrix)
    {
        _matrix = matrix;
    }

    private enum ModelFamily
    {
        OpenAi,
        OpenAiChatAdaptive,
        AnthropicNew,
        AnthropicOld,
        AnthropicCompatible,
        Qwen3,
        MiniMaxM,
        DeepseekV3,
        DeepseekV31,
        DeepseekV4,
        Unknown
    }

    private static ModelFamily ClassifyModel(string model)
    {
        if (string.IsNullOrEmpty(model))
            return ModelFamily.Unknown;

        string lower = model.ToLowerInvariant();

        if (lower == "gpt-4o" || lower == "gpt-4o-mini" ||
            lower == "gpt-4.1" || lower == "gpt-4.1-mini" || lower == "gpt-4.1-nano" ||
            lower == "gpt-4.5" ||
            lower == "gpt-5" || lower == "gpt-5.1" || lower == "gpt-5.2" ||
            lower == "gpt-5.3" || lower == "gpt-5.4" || lower == "gpt-5.5")
            return ModelFamily.OpenAi;

        if (lower.EndsWith("-chat") && (
                lower == "gpt-5-chat" || lower == "gpt-5.1-chat" || lower == "gpt-5.2-chat" ||
                lower == "gpt-5.3-chat" || lower == "gpt-5.4-chat" || lower == "gpt-5.5-chat"))
            return ModelFamily.OpenAiChatAdaptive;

        if (lower == "claude-sonnet-4.6" || lower == "claude-sonnet-5" ||
            lower == "claude-opus-4.6" || lower == "claude-opus-4.7" || lower == "claude-opus-4.8" ||
            lower == "claude-mythos-5" || lower == "claude-fable-5")
            return ModelFamily.AnthropicNew;

        if (lower == "claude-opus-4-5" || lower == "claude-sonnet-4-5" || lower == "claude-haiku-4-5")
            return ModelFamily.AnthropicOld;

        if (lower.StartsWith("claude-"))
            return ModelFamily.AnthropicCompatible;

        if (lower.StartsWith("qwen3"))
            return ModelFamily.Qwen3;

        if (lower.StartsWith("minimax-"))
            return ModelFamily.MiniMaxM;

        if (lower == "deepseek-v3" || lower == "deepseek-chat")
            return ModelFamily.DeepseekV3;

        if (lower == "deepseek-v3.1" || lower == "deepseek-v3-1")
            return ModelFamily.DeepseekV31;

        if (lower.StartsWith("deepseek-v4") || lower.StartsWith("deepseek-v4-"))
            return ModelFamily.DeepseekV4;

        return ModelFamily.Unknown;
    }

    public ChatOptions BuildChatOptions(ReasoningEffort reasoningEffort, string provider, string model)
    {
        ChatOptions options = new();

        ModelFamily family = ClassifyModel(model);

        switch (family)
        {
            case ModelFamily.OpenAi:
                BuildOpenAiChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.OpenAiChatAdaptive:
                BuildOpenAiChatAdaptive(options, reasoningEffort, provider);
                break;
            case ModelFamily.AnthropicNew:
                BuildAnthropicNewChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.AnthropicOld:
                BuildAnthropicOldChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.AnthropicCompatible:
                BuildAnthropicCompatibleChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.Qwen3:
                BuildQwen3ChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.MiniMaxM:
                BuildMiniMaxChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.DeepseekV3:
                BuildDeepseekV3ChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.DeepseekV31:
                BuildDeepseekV31ChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.DeepseekV4:
                BuildDeepseekV4ChatOptions(options, reasoningEffort, provider);
                break;
            case ModelFamily.Unknown:
                ApplyNonCoTFallback(options, reasoningEffort, provider);
                break;
        }

        return options;
    }

    private static void BuildOpenAiChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildOpenAiChatAdaptive(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildAnthropicNewChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildAnthropicOldChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildAnthropicCompatibleChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildQwen3ChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildMiniMaxChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildDeepseekV3ChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildDeepseekV31ChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void BuildDeepseekV4ChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }

    private static void ApplyNonCoTFallback(ChatOptions options, ReasoningEffort effort, string provider)
    {
        throw new NotImplementedException();
    }
}
