using Microsoft.Extensions.AI;
using MeAiReasoningEffort = Microsoft.Extensions.AI.ReasoningEffort;
using ModelsDotDevSharp;

namespace DiffToJsonLib.Reasoning;

public sealed class ChatOptionsBuilder : IChatOptionsBuilder
{
    private readonly IReasoningEffortMatrix _matrix;

    public ChatOptionsBuilder(IReasoningEffortMatrix matrix)
    {
        _matrix = matrix;
    }

    public ChatOptions BuildChatOptions(ReasoningEffort reasoningEffort, string provider, string model)
    {
        ChatOptions options = new();
        return BuildChatOptions(options, reasoningEffort, provider, model);
    }

    internal ChatOptions BuildChatOptions(ChatOptions options, ReasoningEffort reasoningEffort, string provider, string model)
    {
        bool reasoningUsed = reasoningEffort switch
        {
            ReasoningEffort.Auto => _matrix.ProducesReasoningOnAuto(model),
            ReasoningEffort.Off => false,
            _ => true
        };
        int defaultMaxTokens = reasoningUsed ? 16_000 : 8_000;
        options.MaxOutputTokens ??= defaultMaxTokens;

        AIModelReasoningOptionType? reasoningType = _matrix.GetReasoningType(model, provider);

        switch (reasoningType)
        {
            case AIModelReasoningOptionType.Toggle:
                BuildToggleChatOptions(options, reasoningEffort, provider);
                break;
            case AIModelReasoningOptionType.BudgetTokens:
                BuildBudgetChatOptions(options, reasoningEffort);
                break;
            case AIModelReasoningOptionType.Effort:
                BuildEffortChatOptions(options, reasoningEffort, provider);
                break;
            // ReSharper disable once RedundantEmptySwitchSection
            default:
                break;
        }

        return options;
    }

    private static void BuildEffortChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        string canonical = ModelsDevProviderMap.Normalize(provider);
        if (string.Equals(canonical, "anthropic", StringComparison.OrdinalIgnoreCase))
        {
            BuildAnthropicEffortChatOptions(options, effort);
        }
        else
        {
            BuildOpenAiStyleEffortChatOptions(options, effort);
        }
    }

    private static void BuildOpenAiStyleEffortChatOptions(ChatOptions options, ReasoningEffort effort)
    {
        MeAiReasoningEffort meaiEffort = effort switch
        {
            ReasoningEffort.Auto or ReasoningEffort.On=> MeAiReasoningEffort.Medium,
            ReasoningEffort.Off => MeAiReasoningEffort.None,
            ReasoningEffort.Low => MeAiReasoningEffort.Low,
            ReasoningEffort.Medium => MeAiReasoningEffort.Medium,
            ReasoningEffort.High => MeAiReasoningEffort.High,
            ReasoningEffort.XHigh or ReasoningEffort.Max => MeAiReasoningEffort.ExtraHigh,
            _ => throw new ArgumentException("Invalid effort type", nameof(effort)),
        };

        options.Reasoning = new ReasoningOptions { Effort = meaiEffort };
    }

    private static void BuildAnthropicEffortChatOptions(ChatOptions options, ReasoningEffort effort)
    {
        string? value = effort switch
        {
            ReasoningEffort.Auto => null,
            ReasoningEffort.On => null,
            ReasoningEffort.Off => "off",
            ReasoningEffort.Low => "low",
            ReasoningEffort.Medium => "medium",
            ReasoningEffort.High => "high",
            ReasoningEffort.XHigh => "xhigh",
            ReasoningEffort.Max => "max",
            _ => null
        };

        if (value is not null)
        {
            options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            options.AdditionalProperties["reasoning_effort"] = value;
        }
    }

    private static void BuildBudgetChatOptions(ChatOptions options, ReasoningEffort effort)
    {
        if (effort == ReasoningEffort.Off)
        {
            return;
        }

        long maxTokens = options.MaxOutputTokens ?? 8192;

        double fraction = effort switch
        {
            ReasoningEffort.Auto => 0.50,
            ReasoningEffort.On => 0.50,
            ReasoningEffort.Low => 0.25,
            ReasoningEffort.Medium => 0.50,
            ReasoningEffort.High => 0.75,
            _ => 0.50
        };

        long budgetTokens = Math.Max((long)Math.Floor(maxTokens * fraction), 1024);

        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties["thinking"] = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = "enabled",
            ["budget_tokens"] = budgetTokens,
        };
    }

    private static void BuildToggleChatOptions(ChatOptions options, ReasoningEffort effort, string provider)
    {
        // Type-driven: Toggle maps to the nearest on/off builder.
        // Ollama/Qwen3 uses chat_template_kwargs; MiniMax, DeepSeek and other
        // Toggle providers use the MiniMax-style thinking string (adaptive/enabled/disabled).
        string canonical = ModelsDevProviderMap.Normalize(provider);
        if (string.Equals(canonical, "ollama-cloud", StringComparison.OrdinalIgnoreCase))
        {
            BuildQwen3ToggleChatOptions(options, effort);
        }
        else if (string.Equals(canonical, "minimax", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(canonical, "deepseek", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(canonical, "anthropic", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(canonical, "openai", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(canonical, "openrouter", StringComparison.OrdinalIgnoreCase))
        {
            BuildMiniMaxToggleChatOptions(options, effort);
        }
        else
        {
            string thinkingType = effort switch
            {
                ReasoningEffort.Off => "false",
                // ReSharper disable once RedundantSwitchExpressionArms
                ReasoningEffort.Auto => "true",
                _ => "true",
            };

            options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            options.AdditionalProperties["thinking"] = thinkingType;
        }
    }

    private static void BuildQwen3ToggleChatOptions(ChatOptions options, ReasoningEffort effort)
    {
        bool enableThinking = effort switch
        {
            ReasoningEffort.Off => false,
            _ => true,
        };

        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties["chat_template_kwargs"] = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["enable_thinking"] = enableThinking,
        };
    }

    private static void BuildMiniMaxToggleChatOptions(ChatOptions options, ReasoningEffort effort)
    {
        string thinkingType = effort switch
        {
            ReasoningEffort.Off => "disabled",
            ReasoningEffort.Auto => "adaptive",
            _ => "enabled",
        };

        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties["thinking"] = thinkingType;
    }
}
