using System;
using System.Windows;
using TabletMidiPad.Views;

namespace TabletMidiPad.Services;

/// <summary>
/// WPF-backed implementation that opens the preferences dialog and
/// returns the resulting settings when accepted by the user.
/// </summary>
internal sealed class PreferencesDialogService : IPreferencesDialogService
{
    private readonly IConfigValueParser _configValueParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreferencesDialogService"/> class.
    /// </summary>
    public PreferencesDialogService(IConfigValueParser configValueParser)
    {
        _configValueParser = configValueParser ?? throw new ArgumentNullException(nameof(configValueParser));
    }

    /// <inheritdoc />
    public PreferencesDialogResult? ShowDialog(Window owner, AppConfig currentConfig, int expressKeyCount)
    {
        ExpressKeyConfig.EnsureCount(currentConfig.ExpressKeys, expressKeyCount);

        var preferencesWindow = new PreferencesWindow(expressKeyCount, _configValueParser)
        {
            Owner = owner
        };
        preferencesWindow.LoadFrom(currentConfig);
        preferencesWindow.ShowDialog();

        if (!preferencesWindow.Accepted)
        {
            return null;
        }

        return new PreferencesDialogResult
        {
            DawPortName = preferencesWindow.DawPortName,
            AppPortName = preferencesWindow.AppPortName,
            AssociationId = preferencesWindow.AssociationId,
            MousePressureValue = preferencesWindow.MousePressureValue,
            PositionDeadZone = preferencesWindow.PositionDeadZone,
            PositionSmoothing = preferencesWindow.PositionSmoothing,
            PressureCurve = preferencesWindow.PressureCurve,
            InputBackendMode = preferencesWindow.InputBackendMode,
            LowerPenButtonAction = preferencesWindow.LowerPenButtonAction,
            LowerPenButtonMidiChannel = preferencesWindow.LowerPenButtonMidiChannel,
            LowerPenButtonCc = preferencesWindow.LowerPenButtonCc,
            LowerPenButtonNote = preferencesWindow.LowerPenButtonNote,
            UpperPenButtonAction = preferencesWindow.UpperPenButtonAction,
            UpperPenButtonMidiChannel = preferencesWindow.UpperPenButtonMidiChannel,
            UpperPenButtonCc = preferencesWindow.UpperPenButtonCc,
            UpperPenButtonNote = preferencesWindow.UpperPenButtonNote,
            SendPressureGlobally = preferencesWindow.SendPressureGlobally,
            AllowZoneSwitchWhilePressed = preferencesWindow.AllowZoneSwitchWhilePressed,
            ExpressKeys = preferencesWindow.GetExpressKeyConfigs()
        };
    }
}
