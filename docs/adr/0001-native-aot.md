# ADR 001: Native AOT Publishing

## Status
Accepted

## Context
The project is a CLI tool intended for high-performance serialization of Git diffs. To ensure a fast startup time and a lightweight distribution without requiring the .NET runtime to be installed on the target machine, the publishing strategy needs to be decided.

## Decision
We will use **Native AOT (Ahead-Of-Time)** publishing.

## Rationale
1. **Performance:** Native AOT produces a self-contained executable with significantly faster startup times compared to JIT (Just-In-Time) compilation.
2. **Distribution:** Users can run the tool as a single binary without needing to install the .NET SDK or Runtime.
3. **Efficiency:** Reduced memory footprint during execution.

## Consequences
- **Build Time:** Compilation takes longer than standard builds.
- **Trimming:** The code must be compatible with IL trimming. Reflection-heavy libraries must be avoided or configured with `DynamicallyAccessedMembers` and `IsAotCompatible` annotations.
- **Platform Dependence:** Binaries must be published for specific runtime identifiers (e.g., `win-x64`, `linux-x64`).
