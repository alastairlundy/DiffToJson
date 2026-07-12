namespace DiffToJsonLib.Training;

public abstract record AssistantMessageResult
{
    private AssistantMessageResult() { }

    public sealed record AssistantMessageGenerated(string Content, string OriginalAssistantMessage) : AssistantMessageResult;

    public sealed record AssistantMessageDisabled(string FallbackContent) : AssistantMessageResult;

    public sealed record AssistantMessageAttemptedAndFailed(string? FallbackContent, string OriginalAssistantMessage) : AssistantMessageResult;
}
