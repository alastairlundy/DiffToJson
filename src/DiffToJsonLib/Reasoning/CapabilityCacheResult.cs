using ModelsDotDevSharp;

namespace DiffToJsonLib.Reasoning;

public sealed record CapabilityCacheResult(AIProviderInfo[]? Providers, CacheStatus Status);