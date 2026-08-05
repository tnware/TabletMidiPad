# TabletMidiPad

TabletMidiPad is a Windows WPF utility that turns tablet, pen, stylus, or mouse movement into MIDI CC data for a DAW.

![Main Screen](main.png)
![Settings 1](settings1.png)
![Settings 2](settings2.png)
![TabletMidiPad demo](demo.gif)

The app provides a configurable control surface with rectangular zones. Each zone can send its own MIDI channel and CC assignments for X position, Y position, and pressure.

## Important: tablet control while running

When the Wintab backend is active, TabletMidiPad opens priority Wintab contexts and overrides supported Wacom ExpressKey assignments. While TabletMidiPad is running, normal tablet, pen, and button behavior in other applications may be unavailable or altered.

During an orderly shutdown, TabletMidiPad explicitly disables the ExpressKey overrides it created before closing its Wintab contexts. A crash, forced termination, driver failure, or system shutdown can prevent that cleanup. If normal tablet behavior does not return, close or restart TabletMidiPad and restart **Wacom Professional Service** (`WTabletServicePro`) from Windows Services or reboot Windows.

## Features

- runtime settings UI inside the app
- multiple rectangular zones
- per-zone MIDI channel and CC numbers for X, Y, and pressure
- per-zone inversion and pressure behavior
- configurable lower and upper pen-button actions
- auto-tiling for newly added zones
- optional zone locking until release
- Windows MIDI Services loopback creation at startup
- direct Wacom/Wintab pen input with WPF stylus/mouse fallback
- Wintab overlap recovery and priority context handling

## Requirements

- Windows 11 x64
- **Windows MIDI Services Runtime and Tools**, including `midi.exe`
- **.NET 10 Desktop Runtime** for a framework-dependent build
- a DAW or other MIDI-capable application that can receive CC data

For direct Wacom/Wintab input:

- a compatible Wacom Professional tablet driver
- **Wacom Professional Service** (`WTabletServicePro`) running
- `Wintab32.dll` supplied by the installed driver

TabletMidiPad has been tested with:

- Windows MIDI Services Console `1.0.15-rc.2.18`
- Wacom Tablet Driver `6.4.13-4`

Other compatible releases may work but have not been verified. Obtain the current supported Wacom driver for your hardware from [Wacom Driver Support](https://www.wacom.com/en-us/support/product-support/drivers). The tested 6.4.13-4 installer is also available from [Wacom's CDN](https://cdn.wacom.com/u/productsupport/drivers/win/professional/WacomTablet_6.4.13-4.exe).

Do not run TabletMidiPad as administrator. If its **Start Service** action cannot start `WTabletServicePro`, start the service from `services.msc` instead.

### Windows MIDI Services console discovery

TabletMidiPad does not execute a bare `midi.exe` from `PATH`. It resolves a trusted absolute executable path in this order:

1. `TABLETMIDIPAD_MIDI_EXE_PATH`, if set to an existing absolute path whose file name is `midi.exe`
2. the Windows MIDI Services `MidiConsole` machine-registry value
3. `C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe`

Install the Windows MIDI Services **Tools** component if the console is missing.

## MIDI routing and endpoint lifetime

At startup, TabletMidiPad asks Windows MIDI Services to create a loopback pair:

- `Tablet XY (App)`
- `Tablet XY (DAW)`

TabletMidiPad writes WinMM MIDI messages to `Tablet XY (App)`. Your DAW should listen to `Tablet XY (DAW)`.

By default, `DestroyVirtualMidiOnExit` is `false`, so the loopback pair is preserved when TabletMidiPad exits. If that configuration value is enabled, TabletMidiPad asks `midi.exe` to remove the association during an orderly shutdown. A crash or forced termination can prevent removal; use Windows MIDI Services tooling or restart the service if manual cleanup is needed.

Settings are stored in `%AppData%\TabletMidiPad\config.json`. On the first run after an upgrade, a legacy `config.json` next to the executable is migrated when present.

## Input backends

TabletMidiPad prefers direct Wintab input. If Wintab is unavailable, it falls back to WPF stylus or mouse input. The HUD shows the active backend:

- `Wintab`
- `Stylus`
- `Mouse`

For Wintab to activate:

- the Wacom driver must be installed correctly
- `WTabletServicePro` must be running
- `WTInfoA(0, 0, NULL)` must respond through `Wintab32.dll`

The fallback backend keeps the application usable without Wintab, but it does not provide the same direct tablet access or Wacom ExpressKey handling.

### Wacom pen-button setup

To expose both pen side buttons as normal events, configure them in **Wacom Center**:

- set one side button to `Click`
- set the other side button to `Right Click`
- do not map a button to `Scroll` if TabletMidiPad should handle it

On the tested setup, the lower button reported `mask=0x0002, pressed=2` and the upper button reported `mask=0x0004, pressed=3`. Driver and device behavior can vary.

## Zone behavior

Zone bounds use normalized `Left`, `Top`, `Width`, and `Height` values from `0.0` to `1.0`. X and Y are normalized within the active zone, so each zone has its own local MIDI range from `0` to `127`.

If the pointer or pen is outside all enabled zones, the app sends nothing. When zone switching is disabled, the currently pressed zone remains active until release even if the pointer crosses into another zone.

Release behavior can be configured as:

- `No Send On Release`
- `Hold`
- `Reset Pressure Only`
- `Reset All`

## DAW setup

### Ableton Live

1. Start TabletMidiPad.
2. Start Ableton Live after the loopback endpoints exist.
3. Open `Preferences > Link, Tempo & MIDI`.
4. Under Inputs, find `Tablet XY (DAW)`.
5. Enable `Remote`; normally also enable `Track`.
6. Enter MIDI Map mode and move on the pad.

### Waveform

1. Start TabletMidiPad.
2. Start Waveform after the loopback endpoints exist.
3. Select `Tablet XY (DAW)` as the MIDI input.
4. Ensure the target listens for controller data on the active zone's channel.

## Build and test

Install the .NET 10 SDK, then run:

```powershell
dotnet restore
dotnet build
dotnet test TabletMidiPad.Tests\TabletMidiPad.Tests.csproj
```

If a running executable locks the normal output, use a separate output directory:

```powershell
dotnet build -p:OutDir=bin\Verify\
```

## Packaging

The GNU Make targets run on Windows with PowerShell:

```powershell
make publish
make archive
make installer
make release
```

Packaging additionally requires 7-Zip and NSIS. Release packaging:

- excludes PDB files from the public package
- includes [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)
- generates an explicit uninstall file manifest
- runs `scripts/Test-PublicRelease.ps1` before creating public artifacts
- writes SHA-256 checksums for the archive and installer during `make release`

The uninstaller deletes only files listed in the generated release manifest. It leaves unknown files in the installation directory rather than recursively deleting the directory.

Current community builds are not code-signed. Windows SmartScreen may warn before launch; verify the published SHA-256 checksum before running an artifact.

## Troubleshooting

### Windows MIDI Services console is not found

- install Windows MIDI Services Runtime and Tools
- confirm the registered or default Program Files `midi.exe` exists
- if necessary, set `TABLETMIDIPAD_MIDI_EXE_PATH` to its existing absolute path
- do not copy an unrelated executable named `midi.exe` into the application directory

### The DAW does not see the MIDI device

- confirm Windows MIDI Services Runtime and Tools are installed
- start TabletMidiPad before the DAW
- restart the DAW after creating the endpoints
- make sure the DAW listens to `Tablet XY (DAW)`, not `Tablet XY (App)`

### Wintab does not activate

- confirm the Wacom driver is installed
- confirm `WTabletServicePro` is running in `services.msc`
- confirm `Wintab32.dll` is available
- reboot after installing or updating the driver if necessary

If Wintab remains unavailable, TabletMidiPad falls back to stylus/mouse input.

### Tablet behavior does not return after a crash

Restart **Wacom Professional Service** (`WTabletServicePro`) from Windows Services or reboot Windows. An abrupt process termination does not allow TabletMidiPad to run its normal Wintab override cleanup.

## Project structure

- [Views/MainWindow.xaml](Views/MainWindow.xaml): main window layout
- [Views/MainWindow.xaml.cs](Views/MainWindow.xaml.cs): main window event integration
- [AppConfig.cs](AppConfig.cs): saved configuration model
- [Services/Input/WintabInputBackend.cs](Services/Input/WintabInputBackend.cs): direct Wintab backend and override lifecycle
- [Services/Input/WintabInterop.cs](Services/Input/WintabInterop.cs): Wintab native interop definitions
- [Services/Midi/WinMmMidiEngine.cs](Services/Midi/WinMmMidiEngine.cs): loopback lifecycle and WinMM output
- [TabletMidiPad.csproj](TabletMidiPad.csproj): application project

## Trademark and independence notice

TabletMidiPad is an independent project and is not affiliated with, sponsored by, or endorsed by Wacom, Microsoft, Ableton, or Tracktion. Product and company names are trademarks of their respective owners.

## License

TabletMidiPad is licensed under the MIT License. See [LICENSE](LICENSE). Redistributed dependency notices are in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
