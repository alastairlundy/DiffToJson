/*
    Copyright 2026 Alastair Lundy

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */

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
