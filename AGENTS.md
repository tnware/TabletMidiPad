# AGENTS.md

## Purpose

This file defines the development standards for autonomous and human contributors working on TabletMidiPad.

Primary goal:

- Keep behavior stable for users while continuously improving architecture quality.

Secondary goals:

- Prefer maintainability, clear boundaries, and testability over short-term convenience.
- Avoid monolithic windows/code-behind classes by enforcing MVVM + DI patterns.

---

## Core Philosophy

1. **Behavior first, structure second**
  - Refactors must preserve user-visible behavior unless a change is explicitly requested.
  - Organizational cleanup is valid and encouraged, but should not silently alter runtime behavior.
2. **Incremental migration over big-bang rewrites**
  - Make small, verifiable steps.
  - Keep the app shippable after each commit.
  - Use compatibility bridges during migration (e.g., keep existing event handlers while binding new VM state).
3. **Single responsibility by layer**
  - Views: presentation concerns only.
  - ViewModels: UI state and presentation logic.
  - Services: infrastructure/system integrations and domain operations.
  - Composition root: dependency wiring only.

---

## Required Architecture Standards

### 1) MVVM

- All user-facing mutable UI state should live in ViewModels.
- ViewModels should derive from a common observable base (`INotifyPropertyChanged` pattern).
- XAML should bind to ViewModel properties for display and editor fields wherever practical.
- Code-behind may remain for:
  - WPF-specific event plumbing,
  - low-level input event bridging,
  - view-only concerns that do not belong in ViewModels.

Do **not** place new business/infrastructure logic in code-behind.

### 2) Dependency Injection

- All non-trivial dependencies must be injected via constructor parameters.
- Concrete wiring must happen in the composition root (`Composition/AppBootstrapper.cs`).
- Avoid service location patterns in feature code.

### 3) Services and Interfaces

- Infrastructure integrations must be abstracted behind interfaces.
- Current examples include:
  - `IConfigStore`
  - `IMidiEngine`
  - `IWacomServiceController`
- New external/system concerns should follow the same pattern.

### 4) Configuration and Compatibility

- Preserve backward compatibility for persisted config formats.
- Use explicit normalization/migration paths (e.g., `AppConfig.NormalizeLoaded`).
- Never break existing user configs during refactor-only work.

---

## Development Rules

### Refactor Safety Rules

- Prefer move/extract/adapter changes over logic rewrites.
- If behavior changes are necessary, isolate them and document clearly.
- Keep public behavior equivalent by default (status text semantics, MIDI routing behavior, zone interactions, etc.).

### UI Rules

- Prefer bindings over direct control mutation.
- If a control is still directly manipulated, treat it as migration debt and move it to ViewModel in the next safe pass.
- Keep XAML readable; avoid overly clever bindings that reduce clarity.

### Service Rules

- Keep services focused and composable.
- Avoid mixing unrelated responsibilities in one service.
- Throw meaningful exceptions at service boundaries; map to UI-friendly messages at the view/viewmodel layer.

---

## Code Quality and Validation

Before finalizing changes:

1. Build the project successfully.
  - Run: `dotnet build -v minimal`
2. Verify no unintended behavior changes were introduced in touched flows.
3. Keep commits logically grouped and descriptive.

When performing multi-step refactors:

- Commit each stable milestone.
- Push frequently to preserve recovery points.

---

## Git and PR Expectations

- Use small, focused commits for each logical migration slice.
- Keep PR descriptions centered on:
  - what moved where,
  - what behavior is preserved,
  - how it was verified.
- Avoid force-pushing/amending shared history unless explicitly required.

---

## Forward Goals (Post-Migration)

The MVVM/DI migration baseline is now established. Follow-up work should focus on product quality and sustainable velocity rather than additional broad rewrites.

Prioritize in this order:

1. **Reliability and regression safety**
   - Add targeted automated tests for critical flows:
     - startup and configuration load/save,
     - zone configuration/edit/apply behavior,
     - preferences validation and persistence,
     - MIDI send/restart behavior at service boundaries.
   - Prevent regressions in command wiring and XAML bindings with focused UI/integration tests where practical.

2. **Input pipeline hardening**
   - Improve observability around Wintab/mouse fallback transitions and service startup failures.
   - Keep error handling user-friendly and diagnosable (clear UI messages, actionable logs).
   - Preserve graceful degradation when hardware/services are unavailable.

3. **Boundary tightening and simplification**
   - Continue reducing accidental coupling between views and operational logic.
   - Prefer small extractions that improve cohesion (single-purpose services/viewmodel helpers).
   - Remove temporary compatibility bridges only when replacement behavior is verified.

4. **Developer experience**
   - Keep build output clean and actionable.
   - Maintain deterministic startup behavior across contributor environments.
   - Document non-obvious architectural decisions close to the relevant code.

---

## Non-Goals for Routine Changes

- No unrequested feature additions.
- No silent UX/behavior changes.
- No format-breaking config changes.

If a requested task conflicts with these standards, document the tradeoff explicitly in the PR summary.