# Core Project Guide

`TabletMidiPad.Core` holds portable domain logic that should remain independent from app shell and infrastructure details.

## What Belongs In Core

- Interaction-domain models and engine behavior
  - Example: interaction input/state/result and `PadInteractionEngine`.
- Overlay/layout calculation logic
  - Example: `ZoneOverlayLayoutCalculator`.
- Pure transformations and deterministic rules.

## What Does Not Belong In Core

- WPF/UI framework types and event plumbing.
- Device/service orchestration (Wintab, MIDI engine lifecycle, dialogs).
- File/process/environment access.

## Design Rules

- Keep APIs deterministic and side-effect free where possible.
- Favor immutable DTOs/records for inputs and outputs.
- Do not introduce service-location or UI coupling.

## Compatibility

- Treat core DTO contracts and behavioral semantics as stability-sensitive.
- If behavior changes are intentional, update/add tests in `TabletMidiPad.Tests` that document the change.
