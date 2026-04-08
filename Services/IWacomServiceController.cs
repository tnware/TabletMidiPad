using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Provides operations for querying and controlling the Wacom Professional Service.
/// </summary>
internal interface IWacomServiceController
{
    /// <summary>
    /// Gets the current service status text and whether start is available.
    /// </summary>
    /// <returns>Status details for UI display.</returns>
    WacomServiceStatus GetStatus();

    /// <summary>
    /// Starts the Wacom Professional Service if possible.
    /// </summary>
    void Start();
}

/// <summary>
/// Describes Wacom service status for UI presentation.
/// </summary>
/// <param name="StatusText">Human-readable status text.</param>
/// <param name="CanStart">Whether start action should be enabled.</param>
internal readonly record struct WacomServiceStatus(string StatusText, bool CanStart);
