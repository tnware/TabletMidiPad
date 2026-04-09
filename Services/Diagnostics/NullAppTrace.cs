namespace TabletMidiPad.Services.Diagnostics;

/// <summary>
/// No-op trace sink (designer / tests / default ctor paths).
/// </summary>
internal sealed class NullAppTrace : IAppTrace
{
    public static NullAppTrace Instance { get; } = new();

    private NullAppTrace()
    {
    }

    public void Info(string source, string message)
    {
    }

    public void Warn(string source, string message)
    {
    }
}
