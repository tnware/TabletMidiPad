namespace TabletMidiPad.Services.Midi;

internal interface IMidiEngine
{
    /// <summary>
    /// Gets a value indicating whether a MIDI output target is open.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Initializes loopback routing and opens the app output endpoint.
    /// </summary>
    /// <param name="config">Current runtime config.</param>
    void Initialize(AppConfig config);

    /// <summary>
    /// Initializes loopback routing and opens the app output endpoint asynchronously.
    /// </summary>
    /// <param name="config">Current runtime config.</param>
    /// <param name="onProgress">Progress callback for UI status updates.</param>
    Task InitializeAsync(AppConfig config, Action<string>? onProgress = null);

    /// <summary>
    /// Closes and re-opens routing/output from the current config.
    /// </summary>
    /// <param name="config">Current runtime config.</param>
    void Restart(AppConfig config);

    /// <summary>
    /// Closes and re-opens routing/output from the current config without blocking the caller.
    /// </summary>
    /// <param name="config">Current runtime config.</param>
    /// <param name="onProgress">Optional progress callback for UI status updates.</param>
    Task RestartAsync(AppConfig config, Action<string>? onProgress = null);

    /// <summary>
    /// Shuts down MIDI output and removes loopback endpoints.
    /// </summary>
    /// <param name="config">Current runtime config.</param>
    void Shutdown(AppConfig config);

    /// <summary>
    /// Sends a MIDI CC message when value changed since last send.
    /// </summary>
    void SendCcIfChanged(int channel, int cc, int value, bool force = false);

    /// <summary>
    /// Sends a MIDI note on/off message when state changed since last send.
    /// </summary>
    void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false);

    /// <summary>
    /// Clears cached de-duplication state for sent messages.
    /// </summary>
    void ClearCachedValues();
}
