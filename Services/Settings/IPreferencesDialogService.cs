namespace TabletMidiPad.Services.Settings;

/// <summary>
/// Opens the preferences dialog and returns edited values when accepted.
/// </summary>
internal interface IPreferencesDialogService
{
    /// <summary>
    /// Shows the preferences dialog owned by the given window.
    /// </summary>
    /// <param name="owner">The owning window.</param>
    /// <param name="config">The current application configuration.</param>
    /// <param name="expressKeyCount">The detected express-key count.</param>
    /// <returns>
    /// The edited preferences payload when accepted; otherwise <see langword="null" />.
    /// </returns>
    PreferencesDialogResult? ShowDialog(System.Windows.Window owner, AppConfig config, int expressKeyCount);
}
