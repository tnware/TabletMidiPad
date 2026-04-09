namespace TabletMidiPad.Services.Config;

/// <summary>
/// Parses and normalizes user-facing configuration values.
/// </summary>
internal interface IConfigValueParser
{
    /// <summary>
    /// Reads and validates an integer range-bound field.
    /// </summary>
    int ReadInt(string? text, int min, int max, string fieldName);

    /// <summary>
    /// Reads and validates a decimal range-bound field.
    /// </summary>
    double ReadDouble(string? text, double min, double max, string fieldName);

    /// <summary>
    /// Ensures a required text field is non-empty.
    /// </summary>
    string RequireText(string? text, string fieldName);

    /// <summary>
    /// Ensures a required GUID string and returns normalized brace format.
    /// </summary>
    string RequireGuidText(string? text, string fieldName);

    /// <summary>
    /// Ensures pressure curve is one of the supported names.
    /// </summary>
    string RequirePressureCurve(string? text);

    /// <summary>
    /// Normalizes release-behavior aliases to canonical values.
    /// </summary>
    string NormalizeReleaseBehavior(string? text);

    /// <summary>
    /// Normalizes pen-button action values to known actions.
    /// </summary>
    string NormalizePenButtonAction(string? text);

    /// <summary>
    /// Normalizes express-key action values to known actions.
    /// </summary>
    string NormalizeExpressKeyAction(string? text);
}
