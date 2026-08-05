namespace TabletMidiPad.Services.Input;

/// <summary>
/// Identifies a Wintab ExpressKey function overridden by an application context.
/// </summary>
internal readonly record struct WintabExpressKeyOverride(
    uint ContextHandle,
    byte TabletIndex,
    byte ControlIndex,
    byte FunctionIndex);

/// <summary>
/// Tracks successful ExpressKey overrides so each one can be disabled before its Wintab context closes.
/// </summary>
internal sealed class WintabExpressKeyOverrideRegistry
{
    private readonly List<WintabExpressKeyOverride> _entries = [];

    public int Count => _entries.Count;

    public void Add(WintabExpressKeyOverride entry)
    {
        _entries.Add(entry);
    }

    public int ReleaseContext(uint contextHandle, Func<WintabExpressKeyOverride, bool> release)
    {
        ArgumentNullException.ThrowIfNull(release);

        WintabExpressKeyOverride[] owned = [.. _entries.Where(entry => entry.ContextHandle == contextHandle)];
        _entries.RemoveAll(entry => entry.ContextHandle == contextHandle);

        int released = 0;
        foreach (WintabExpressKeyOverride entry in owned)
        {
            try
            {
                if (release(entry))
                {
                    released++;
                }
            }
            catch
            {
                // Cleanup is best-effort; the context must still be closed.
            }
        }

        return released;
    }

    public void Clear()
    {
        _entries.Clear();
    }
}
