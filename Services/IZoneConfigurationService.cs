namespace TabletMidiPad.Services;

/// <summary>
/// Encapsulates zone-related configuration operations used by the main view.
/// </summary>
internal interface IZoneConfigurationService
{
    /// <summary>
    /// Validates the current application configuration.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    void ValidateConfig(AppConfig config);

    /// <summary>
    /// Applies automatic tiling across configured zones.
    /// </summary>
    /// <param name="config">The configuration containing zones and layout limits.</param>
    void AutoTileZones(AppConfig config);

    /// <summary>
    /// Gets the next available 3-CC block start value for a new zone.
    /// </summary>
    /// <param name="config">The configuration containing existing zone mappings.</param>
    /// <returns>The starting CC value.</returns>
    int GetNextAvailableCcStart(AppConfig config);
}
