namespace DiffToJsonCli;

/// <summary>
/// Synchronous progress sink: writes each <see cref="RunProgress"/> message to the
/// console as it is reported. Warnings are routed to standard error; all other
/// phases go to standard output.
/// </summary>
internal sealed class ConsoleProgress : IProgress<RunProgress>
{
    public void Report(RunProgress value)
    {
        TextWriter writer = value.Phase == RunPhase.Warning ? Console.Error : Console.Out;
        writer.WriteLine(value.Message);
    }
}