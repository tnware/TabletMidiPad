# Services Organization

This folder is organized by domain to keep responsibilities clear and reduce cross-layer drift.

## Folders

- `Config`
  - Configuration persistence, parsing, normalization, and runtime ownership.
  - Examples: `IConfigStore`, `RuntimeConfigService`, `ConfigValueParser`.

- `Diagnostics`
  - Lightweight app tracing abstractions and implementations.
  - Examples: `IAppTrace`, `TraceAppTrace`, `NullAppTrace`.

- `Input`
  - Pen/stylus/mouse/Wintab backend lifecycle, packet adaptation, fallback handling, and input controller orchestration.
  - Examples: `PadInputController`, `InputBackendCoordinator`, `WintabInputBridge`.

- `Pad`
  - Pad-surface presentation and interaction-outcome application.
  - Examples: `IPadPresentationCoordinator`, `PadInteractionOutcomeApplier`.

- `Settings`
  - Preferences/settings workflows, toolbar config mutations, and settings decision logic.
  - Examples: `SettingsWorkflow`, `SettingsWorkflowCoordinator`, `PreferencesDialogService`.

- `Shell`
  - Main shell command orchestration and startup lifecycle sequencing.
  - Examples: `MainShellCommandController`, `StartupLifecycleService`.

- `UiProjection`
  - Projection of runtime/domain state to UI text/selection state DTOs.
  - Examples: `UiStateProjectionService`, `ConnectedState`, `ToolbarState`.

- `Wacom`
  - Wacom service control and workflow coordination.
  - Examples: `IWacomServiceController`, `WacomServiceWorkflowCoordinator`.

- `Zone`
  - Zone editing, layout, resize, overlay, and zone action workflows.
  - Examples: `ZoneEditorService`, `ZoneResizeService`, `ZoneActionWorkflowCoordinator`.

## Boundary Rules

- Keep new code in the folder matching its primary responsibility.
- Prefer depending on focused interfaces from the same domain.
- Avoid introducing cross-domain “god coordinators.”
- Keep UI-only projection logic in `UiProjection`, not in `Views`.
- Keep WPF event plumbing in `Views`; operational logic belongs in services/viewmodels.
