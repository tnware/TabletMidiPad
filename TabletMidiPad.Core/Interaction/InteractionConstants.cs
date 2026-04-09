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
    /// Supported express-key action names.
    /// </summary>
    public static class ExpressKeyActions
    {
        public const string None = PenButtonActions.None;
        public const string CcMomentary = PenButtonActions.CcMomentary;
        public const string CcToggle = PenButtonActions.CcToggle;
        public const string NoteMomentary = PenButtonActions.NoteMomentary;
        public const string NoteToggle = PenButtonActions.NoteToggle;
        public const string PrevPage = "Prev Page";
        public const string NextPage = "Next Page";
        public const string GoToPage1 = "Go To Page 1";
        public const string GoToPage2 = "Go To Page 2";
        public const string GoToPage3 = "Go To Page 3";
        public const string GoToPage4 = "Go To Page 4";
        public const string GoToPage5 = "Go To Page 5";
        public const string GoToPage6 = "Go To Page 6";
        public const string GoToPage7 = "Go To Page 7";
        public const string GoToPage8 = "Go To Page 8";
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

