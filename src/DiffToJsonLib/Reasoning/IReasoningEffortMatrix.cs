using ModelsDotDevSharp;

namespace DiffToJsonLib.Reasoning;

public interface IReasoningEffortMatrix
{
    IReadOnlySet<ReasoningEffort> GetSupportedReasoningValues(string model);
    bool ProducesReasoningOnAuto(string model);
    AIModelReasoningOptionType? GetReasoningType(string model, string provider);

    /// <summary>
    /// Probes the backing capability data source. Implementations without a remote
    /// catalog report <see cref="CacheStatus.Fresh"/>.
    /// </summary>
    Task<CacheStatus> GetCacheStatusAsync(CancellationToken cancellationToken = default);
}
