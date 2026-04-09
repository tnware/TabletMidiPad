using System;

namespace TabletMidiPad.Services.Input;

/// <summary>
/// Canonical normalization for persisted/config input backend mode strings.
/// </summary>
internal static class InputBackendModeNormalizer
{
    /// <summary>
    /// Normalizes user-facing and legacy mode labels to the three canonical stored values.
    /// </summary>
    public static string Normalize(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            "Wintab" => "Wintab Only",
            "Mouse" => "Stylus/Mouse Only",
            "Wintab Only" => value,
            "Stylus/Mouse Only" => value,
            _ => "Auto"
        };
    }

    /// <summary>
    /// Returns true when stylus/mouse fallback input should not be used (Wintab-only semantics).
    /// </summary>
    public static bool IsWintabOnlyMode(string? inputBackendMode)
        => string.Equals(Normalize(inputBackendMode), "Wintab Only", StringComparison.Ordinal);
}
