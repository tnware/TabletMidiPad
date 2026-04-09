# Test Project Guide

This project contains focused tests for behavior-critical application flows.

## Test Scope

- Startup and lifecycle
  - Cold start sequencing, MIDI init outcomes, input backend shutdown behavior.
- Input pipeline
  - Backend/fallback routing, packet flow decisions, controller session behavior.
- Settings and workflow
  - Validation/save decisions, preferences application, toolbar mode/curve behavior.
- Zone behavior
  - Zone actions, editor commit/load logic, resize rules, overlay calculations.
- UI projection/mapping
  - State projection and view-model mapping helpers.

## Conventions

- Keep tests small and behavior-focused.
- Name tests as `MethodOrFlow_Condition_ExpectedOutcome`.
- Prefer deterministic fakes/stubs over broad integration setup.
- Keep stubs local to each test file unless reused across many suites.

## When To Add Tests

- Refactor-only changes:
  - Add/adjust tests only where boundaries move or seams change.
  - Preserve existing behavior assertions.
- Behavior changes:
  - Add tests that capture new behavior and edge cases explicitly.

## Boundaries

- Avoid testing WPF rendering details here.
- Test service/viewmodel decisions and state transitions.
- Keep infrastructure dependencies mocked or stubbed unless integration is intentional.
