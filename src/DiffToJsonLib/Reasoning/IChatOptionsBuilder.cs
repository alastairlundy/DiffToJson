using Microsoft.Extensions.AI;

namespace DiffToJsonLib.Reasoning;

public interface IChatOptionsBuilder
{
    ChatOptions BuildChatOptions(ReasoningEffort reasoningEffort, string provider, string model);
}
