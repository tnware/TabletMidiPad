# TabletMidiPad

`TabletMidiPad` is a small WPF utility that turns tablet or mouse movement into MIDI CC data for use in a DAW.

![TabletMidiPad demo](demo.gif)

The app presents a 2D pad and sends three continuous controller values:

- `CC16` = X position
- `CC17` = Y position
- `CC18` = pressure

It is currently built for Windows and uses **Windows MIDI Services** loopback endpoints rather than `loopMIDI` or `teVirtualMIDI`.

## Why this project uses Windows MIDI Services

On the target machine, third-party virtual MIDI ports were being created successfully but were not consistently visible or usable inside Ableton/Waveform on Windows 11 24H2. This project now creates a temporary loopback pair through Microsoft's built-in MIDI stack and sends MIDI into that path through WinMM.

At startup the app creates:

- `Tablet XY (App)`
- `Tablet XY (DAW)`

The app writes MIDI to `Tablet XY (App)`. Your DAW should listen to `Tablet XY (DAW)`.

These loopback endpoints are temporary and are removed when the app closes.

## Requirements

- Windows 11 with **Windows MIDI Services**
- Windows MIDI Services **Runtime and Tools** installed
- `.NET 10 SDK`
- A DAW or MIDI-capable application that can receive CC data

The app expects the Windows MIDI Services console at:

`C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe`

If your installation lives somewhere else, update `MidiConsolePath` in `MainWindow.xaml.cs`.

## Build

From the project directory:

```powershell
dotnet build
```

If the executable is already running, a normal build may fail because `TabletMidiPad.exe` is locked. In that case, close the app or build to another output folder:

```powershell
dotnet build -p:OutDir=bin\Verify\
```

## Run

```powershell
dotnet run
```

On startup, the app:

1. Removes any stale loopback pair using the app's association id.
2. Creates a fresh Windows MIDI Services loopback pair.
3. Opens `Tablet XY (App)` as a MIDI output.
4. Sends CC messages whenever X, Y, or pressure changes.

## DAW setup

### Ableton Live

1. Start `TabletMidiPad`.
2. Launch Ableton after the app is already open.
3. Open `Preferences > Link, Tempo & MIDI`.
4. Under Inputs, find `Tablet XY (DAW)`.
5. Enable `Remote`.
6. Usually also enable `Track`.
7. Enter MIDI Map mode and move on the pad.

### Waveform

1. Start `TabletMidiPad`.
2. Launch Waveform after the app is already open.
3. Select `Tablet XY (DAW)` as the MIDI input.
4. Make sure the track or mapping target is listening for controller data on channel 1.

## MIDI details

- MIDI channel: `1`
- `CC16`: X axis
- `CC17`: Y axis
- `CC18`: pressure
- Pressure falls back to `127` when using the mouse
- Values are normalized to `0-127`

The app only sends a CC message when the value changes, which keeps the event stream smaller during use.

## Input behavior

- Stylus input is preferred
- Mouse input is supported as a fallback
- Y is inverted so the top of the pad produces a higher value
- The cursor indicator shows the current pointer position

## Project structure

- `MainWindow.xaml`: main pad UI
- `MainWindow.xaml.cs`: MIDI loopback creation, WinMM output, and pad handling
- `TabletMidiPad.csproj`: WPF project file

## Troubleshooting

### The app opens but the DAW does not see the MIDI device

- Confirm Windows MIDI Services Runtime and Tools are installed
- Confirm `midi.exe` exists at the configured path
- Start the app before starting the DAW
- Fully restart the DAW after launching the app

### The MIDI device appears but mapping does nothing

- In Ableton, enable `Remote` for `Tablet XY (DAW)`
- Ensure the DAW is listening to `Tablet XY (DAW)`, not `Tablet XY (App)`
- Verify the target is learning CC input, not note input
- Remember the app sends only CC data, not note messages

### Build fails because the executable is locked

Close the running app and build again, or build to another output folder:

```powershell
dotnet build -p:OutDir=bin\Verify\
```

### Windows MIDI Services is installed but the app still fails on startup

Check that this command works:

```powershell
& 'C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe' --help
```

If it does not, the app will not be able to create the loopback pair.

## Notes

- The loopback pair created by the app is temporary.
- The endpoints are removed when the app closes.
- If the app crashes, it will attempt to clean up the same association id on next launch.

## License

MIT. See `LICENSE`.
