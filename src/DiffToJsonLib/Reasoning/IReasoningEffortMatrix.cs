using ModelsDotDevSharp;

namespace DiffToJsonLib.Reasoning;

public interface IReasoningEffortMatrix
{
    IReadOnlySet<ReasoningEffort> GetSupportedReasoningValues(string model);
    bool ProducesReasoningOnAuto(string model);
    AIModelReasoningOptionType? GetReasoningType(string model, string provider);
}
