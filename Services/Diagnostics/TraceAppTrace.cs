using System.Diagnostics;

namespace TabletMidiPad.Services;

/// <summary>
/// Emits traces via <see cref="Trace"/> so they appear in the debugger and tools listening to default trace output.
/// </summary>
internal sealed class TraceAppTrace : IAppTrace
{
    public void Info(string source, string message)
    {
        Trace.WriteLine(Format(source, message));
    }

    public void Warn(string source, string message)
    {
        Trace.WriteLine(Format(source, "WARN " + message));
    }

    private static string Format(string source, string message)
        => $"[TabletMidiPad:{source}] {message}";
}
