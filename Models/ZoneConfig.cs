namespace TabletMidiPad.Models;

public static class PenButtonActions
{
    public const string None = "None";
    public const string LockXY = "Lock XY";
    public const string LockX = "Lock X";
    public const string LockY = "Lock Y";
    public const string DisablePressure = "Disable Pressure";
    public const string EnablePressure = "Enable Pressure";
    public const string CcMomentary = "CC Momentary";
    public const string CcToggle = "CC Toggle";
    public const string NoteMomentary = "Note Momentary";
    public const string NoteToggle = "Note Toggle";
}

public static class ZoneReleaseBehaviors
{
    public const string Hold = "Hold";
    public const string ResetPressureOnly = "Reset Pressure Only";
    public const string ResetAll = "Reset All";
    public const string NoSendOnRelease = "No Send On Release";
}

public sealed class ExpressKeyConfig
{
    public string Action { get; set; } = PenButtonActions.None;
    public int MidiChannel { get; set; } = 1;
    public int Cc { get; set; } = 70;
    public int Note { get; set; } = 36;

    public static ExpressKeyConfig CreateDefault(int index)
    {
        return new ExpressKeyConfig
        {
            Action = PenButtonActions.NoteMomentary,
            MidiChannel = 1,
            Cc = Math.Clamp(70 + index, 0, 127),
            Note = Math.Clamp(36 + index, 0, 127)
        };
    }

    public static void EnsureCount(List<ExpressKeyConfig> list, int count)
    {
        while (list.Count < count)
            list.Add(CreateDefault(list.Count));
    }
}

public sealed class ZoneConfig
{
    public string Name { get; set; } = "Zone 1";
    public bool Enabled { get; set; } = true;
    public int MidiChannel { get; set; } = 1;
    public int CcX { get; set; } = 16;
    public int CcY { get; set; } = 17;
    public int CcPressure { get; set; } = 18;
    public bool SendX { get; set; } = true;
    public bool SendY { get; set; } = true;
    public bool InvertX { get; set; }
    public bool InvertY { get; set; } = true;
    public bool SendPressure { get; set; } = true;
    public string ReleaseBehavior { get; set; } = ZoneReleaseBehaviors.NoSendOnRelease;
    public bool ResetPressureOnRelease { get; set; } = true;

    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 1.0;
    public double Height { get; set; } = 1.0;

    public static ZoneConfig CreateDefault()
    {
        return new ZoneConfig();
    }
}
