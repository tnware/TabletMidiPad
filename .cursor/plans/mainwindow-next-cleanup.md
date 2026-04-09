# MainWindow cleanup — next slice (event flow, overlay/cursor, helpers)

## Branch and workflow

- **Work on:** `cursor/mainwindow-coordinator-cleanup-a468` (current feature branch), or merge that PR first and recreate this plan on a new branch off `main`—your choice. This file assumes **continuing on the existing branch**.
- **Rules:** No behavior or UX changes; no config format changes; MVVM/DI per `AGENTS.md`.
- **Commits:** Small, behavior-preserving commits (e.g. one coordinator + wiring, then tests, then next coordinator).

## Goals

1. Thin `Pad_*` and Wintab packet/proximity handlers by moving **orchestration decisions** out of `Views/MainWindow.xaml.cs`.
2. Move **overlay + cursor** computation into a dedicated presentation coordinator; keep actual WPF control updates in the view.
3. Reduce duplication (e.g. input-backend mode normalization) without changing semantics.

## 1) Input event-flow coordinator

**Add**

- `Services/IMainWindowInputEventFlowCoordinator.cs`
- `Services/MainWindowInputEventFlowCoordinator.cs`

**Responsibility:** Given current mode/state and an incoming semantic event (Wintab packet update, proximity leave, stylus/mouse sample), return **what MainWindow should do next** (e.g. call release, process tick with which input, clear active flag). No direct UI; no `Dispatcher` inside coordinator.

**Pull from `MainWindow`:** Branching in `HandleWintabPacket`, proximity handler, `Pad_Stylus*`, `Pad_Mouse*` that mixes `_useWintab`, `AllowsStylusMouseFallback()`, hover vs down, and `_isActive` transitions.

**Wire:** Register in `Composition/AppBootstrapper.cs`; inject into `MainWindow` (parameter + default ctor).

**Suggested commit:** `Extract MainWindow input event flow coordinator`

## 2) Pad presentation coordinator (overlay + cursor)

**Add**

- `Services/IPadPresentationCoordinator.cs`
- `Services/PadPresentationCoordinator.cs`

**Responsibility:**

- Build `ZoneOverlayRenderRequest` (and palette/theme brush resolution if kept view-neutral via passed-in brushes/colors, or keep theme reads in view and pass primitives—prefer not to reference `Application.Current` from deep services unless already the pattern).
- Decide cursor dot visibility, opacity, and pixel position from `InteractionInput` + packet update + pad size.

**Pull from `MainWindow`:** `DrawZoneOverlay` request assembly; the non-trivial parts of `ProcessInteractionTick` / Wintab hover path / `HandleRelease` that only compute presentation.

**Suggested commit:** `Extract pad overlay and cursor presentation coordinator`

## 3) Normalize helpers (optional third commit)

- Single canonical **input backend mode** normalization used by `MainWindowViewStateMapper`, `InputBackendCoordinator`, and any remaining `MainWindow` code—one shared static or small `IInputBackendModeNormalizer` if you want testability.
- **Suggested commit:** `Share input backend mode normalization across services`

## Tests

Add in `TabletMidiPad.Tests/`:

- `MainWindowInputEventFlowCoordinatorTests.cs` — routing decisions (hover → release, fallback allowed/blocked, etc.) with minimal fakes.
- `PadPresentationCoordinatorTests.cs` — overlay request shape, cursor position/opacity rules.

Keep existing service tests as regression net.

**Suggested commit:** `Add unit tests for input flow and pad presentation coordinators`

## Verification (each milestone)

```powershell
dotnet build -v minimal
dotnet test ./TabletMidiPad.Tests/TabletMidiPad.Tests.csproj -v minimal
```

## Non-goals

- New features; MIDI/routing behavior changes; preferences/config schema changes; large unrelated refactors.

## Done when

- `MainWindow.xaml.cs` handlers are thin: subscribe events → call coordinators/services → apply results to controls/VM.
- Tests green; PR updated with short “what moved, behavior preserved, how verified” summary.
