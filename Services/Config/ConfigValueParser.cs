using System;
using System.Globalization;

namespace TabletMidiPad.Services.Config;

/// <summary>
/// Default parser/normalizer for configuration-bound values.
/// </summary>
internal sealed class ConfigValueParser : IConfigValueParser
{
    /// <inheritdoc />
    public int ReadInt(string? text, int min, int max, string fieldName)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new InvalidOperationException($"{fieldName} must be a whole number.");
        }

        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{fieldName} must be between {min} and {max}.");
        }

        return value;
    }

    /// <inheritdoc />
    public double ReadDouble(string? text, double min, double max, string fieldName)
    {
        if (!double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out double value))
        {
            throw new InvalidOperationException($"{fieldName} must be a number.");
        }

        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{fieldName} must be between {min:0.###} and {max:0.###}.");
        }

        return value;
    }

    /// <inheritdoc />
    public string RequireText(string? text, string fieldName)
    {
        string value = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value;
    }

    /// <inheritdoc />
    public string RequireGuidText(string? text, string fieldName)
    {
        string value = RequireText(text, fieldName);
        if (!Guid.TryParse(value, out Guid parsed))
        {
            throw new InvalidOperationException($"{fieldName} must be a valid GUID.");
        }

        return parsed.ToString("B");
    }

    /// <inheritdoc />
    public string RequirePressureCurve(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        if (value is not ("Linear" or "Logarithmic" or "Exponential"))
        {
            throw new InvalidOperationException("Pressure Curve must be Linear, Logarithmic, or Exponential.");
        }

        return value;
    }

    /// <inheritdoc />
    public string NormalizeReleaseBehavior(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            "Hold Last Values" => ZoneReleaseBehaviors.Hold,
            ZoneReleaseBehaviors.Hold => value,
            ZoneReleaseBehaviors.ResetAll => value,
            ZoneReleaseBehaviors.NoSendOnRelease => value,
            _ => ZoneReleaseBehaviors.ResetPressureOnly
        };
    }

    /// <inheritdoc />
    public string NormalizePenButtonAction(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            PenButtonActions.None => value,
            PenButtonActions.LockXY => value,
            PenButtonActions.LockX => value,
            PenButtonActions.LockY => value,
            PenButtonActions.DisablePressure => value,
            PenButtonActions.EnablePressure => value,
            PenButtonActions.CcMomentary => value,
            PenButtonActions.CcToggle => value,
            PenButtonActions.NoteMomentary => value,
            PenButtonActions.NoteToggle => value,
            _ => PenButtonActions.None
        };
    }
}
