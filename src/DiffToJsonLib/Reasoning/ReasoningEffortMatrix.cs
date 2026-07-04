namespace DiffToJsonLib.Reasoning;

public sealed class ReasoningEffortMatrix : IReasoningEffortMatrix
{
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

    private static readonly HashSet<ReasoningEffort> UnknownFallbackSet = new()
    {
        ReasoningEffort.Auto
    };

    private sealed record ModelEntry(IReadOnlySet<ReasoningEffort> ValidSet, bool ProducesReasoningOnAuto);

    private static readonly IReadOnlyDictionary<string, ModelEntry> _perModel =
        new Dictionary<string, ModelEntry>(StringComparer.OrdinalIgnoreCase)
        {
            // OpenAI graduated
            ["gpt-4o"] = new(FullSet, true),
            ["gpt-4o-mini"] = new(FullSet, true),
            ["gpt-4.1"] = new(FullSet, true),
            ["gpt-4.1-mini"] = new(FullSet, true),
            ["gpt-4.1-nano"] = new(FullSet, true),
            ["gpt-4.5"] = new(FullSet, true),
            ["gpt-5"] = new(FullSet, true),
            ["gpt-5.1"] = new(FullSet, true),
            ["gpt-5.2"] = new(FullSet, true),
            ["gpt-5.3"] = new(FullSet, true),
            ["gpt-5.4"] = new(FullSet, true),
            ["gpt-5.5"] = new(FullSet, true),

            // OpenAI Chat adaptive (T032)
            ["gpt-5-chat"] = new(FullSet, true),
            ["gpt-5.1-chat"] = new(FullSet, true),
            ["gpt-5.2-chat"] = new(FullSet, true),
            ["gpt-5.3-chat"] = new(FullSet, true),
            ["gpt-5.4-chat"] = new(FullSet, true),
            ["gpt-5.5-chat"] = new(FullSet, true),

            // Anthropic new (Mythos 5+, Fable 5+, Opus 4.6+, Sonnet 4.6+/5)
            ["claude-sonnet-4.6"] = new(FullSet, true),
            ["claude-sonnet-5"] = new(FullSet, true),
            ["claude-opus-4.6"] = new(FullSet, true),
            ["claude-opus-4.7"] = new(FullSet, true),
            ["claude-opus-4.8"] = new(FullSet, true),
            ["claude-mythos-5"] = new(FullSet, true),
            ["claude-fable-5"] = new(FullSet, true),

            // Anthropic old (budget-based, no XHigh/Max per D010)
            ["claude-opus-4-5"] = new(AnthropicOldSet, true),
            ["claude-sonnet-4-5"] = new(AnthropicOldSet, true),
            ["claude-haiku-4-5"] = new(AnthropicOldSet, true),

            // Deepseek V3 (off only)
            ["deepseek-chat"] = new(OffOnlySet, false),
            ["deepseek-v3"] = new(OffOnlySet, false),

            // Deepseek V3.1 (binary thinking)
            ["deepseek-v3.1"] = new(BinaryThinkingSet, true),
            ["deepseek-v3-1"] = new(BinaryThinkingSet, true),

            // Deepseek V4 (graduated without XHigh per D011)
            ["deepseek-v4"] = new(DeepseekV4Set, true),
            ["deepseek-v4-flash"] = new(DeepseekV4Set, true),

            // Qwen 3 thinking
            ["qwen3"] = new(BinaryThinkingSet, true),
            ["qwen3-30b-a3b"] = new(BinaryThinkingSet, true),
            ["qwen3-235b-a22b"] = new(BinaryThinkingSet, true),

            // Qwen 3.5+ (graduated)
            ["qwen3.5"] = new(FullSet, true),
            ["qwen3.5-30b-a3b"] = new(FullSet, true),
            ["qwen3.5-235b-a22b"] = new(FullSet, true),

            // MiniMax M series
            ["minimax-m1"] = new(BinaryThinkingSet, false),
            ["minimax-m2"] = new(BinaryThinkingSet, false),
            ["minimax-m2.5"] = new(BinaryThinkingSet, false),
            ["minimax-m3"] = new(BinaryThinkingSet, false),
        };

    public IReadOnlySet<ReasoningEffort> GetSupportedReasoningValues(string model)
    {
        if (_perModel.TryGetValue(model, out var entry))
            return entry.ValidSet;

        return UnknownFallbackSet;
    }

    public bool ProducesReasoningOnAuto(string model)
    {
        if (_perModel.TryGetValue(model, out var entry))
            return entry.ProducesReasoningOnAuto;

        return false;
    }
}
