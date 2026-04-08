namespace TabletMidiPad.Core.Interaction;

/// <summary>
/// Shared string constants used by the interaction engine to match persisted configuration values.
/// </summary>
public static class InteractionConstants
{
    /// <summary>
    /// Supported pen button action names.
    /// </summary>
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

    /// <summary>
    /// Supported zone release behavior names.
    /// </summary>
    public static class ZoneReleaseBehaviors
    {
        public const string Hold = "Hold";
        public const string ResetPressureOnly = "Reset Pressure Only";
        public const string ResetAll = "Reset All";
        public const string NoSendOnRelease = "No Send On Release";
    }
}

