# Testing Strategy

Aura's testing strategy should grow in the same order as the architecture. Tests should appear as soon as there is a contract worth protecting, and they should always verify behavior at the subsystem boundary first.

## Phase 1 - Foundation

**When tests begin:** Immediately after the build and host exist.

**Test types:** Unit tests for shared primitives, host startup tests, configuration tests, logging smoke tests.

**Purpose:** Prove the repository can be built and exercised automatically.

## Phase 2 - Runtime Spine

**When tests expand:** As Runtime Core, Event System, State System, and Persistence System arrive.

**Test types:** Unit tests, contract tests, state-machine tests, persistence round-trip tests, lifecycle tests.

**Purpose:** Protect the architectural spine before UI or plugin complexity is added.

## Phase 3 - Interpretation Layers

**When tests expand:** As Context and Priority are implemented.

**Test types:** Deterministic scenario tests, classification tests, suppression/queue/merge tests, policy tests.

**Purpose:** Verify that the runtime decides consistently about relevance and behavior.

## Phase 4 - Interaction and Surface

**When tests expand:** As user-facing presentation begins.

**Test types:** Integration tests, surface lifecycle tests, accessibility tests, interaction tests, state synchronization tests.

**Purpose:** Ensure visible behavior reflects state truth and remains usable.

## Phase 5 - Continuity Systems

**When tests expand:** As Artifact, History, Search, and Settings mature.

**Test types:** Recovery tests, persistence corruption tests, search recall tests, history retention tests, settings round-trip tests.

**Purpose:** Verify continuity through restart, interruption, and partial failure.

## Phase 6 - Plugins and Security

**When tests expand:** As Plugin Runtime and policy enforcement are introduced.

**Test types:** Isolation tests, permission tests, fail-closed tests, plugin recovery tests.

**Purpose:** Prove extensions cannot compromise core runtime behavior.

## Phase 7 - Performance and Accessibility

**When tests expand:** After core feature coverage is in place.

**Test types:** BenchmarkDotNet benchmarks, startup timing checks, memory profile checks, accessibility automation, keyboard flow checks, reduced-motion checks.

**Purpose:** Ensure the resident runtime remains lightweight and usable.

## Phase 8 - End-to-End

**When tests begin:** Once the application can drive complete spec-defined workflows.

**Test types:** E2E flows, startup-to-surface flows, recovery flows, plugin enable/disable flows, accessibility smoke flows.

**Purpose:** Validate the product as a whole, not just isolated parts.

## Testing Rules

- Prefer real implementations where practical.
- Use fakes before mocks when the behavior is deterministic enough.
- Test contracts, not internal implementation details.
- Keep state-machine tests authoritative.
- Add regression tests whenever a gate fails.
- Make accessibility and recovery testable from the beginning, not retrofitted later.

## Coverage Expectations by Stage

- Bootstrap: build and host smoke coverage
- Runtime spine: core contract coverage
- Interaction stage: surface and accessibility coverage
- Continuity stage: persistence and recovery coverage
- Plugin stage: isolation and security coverage
- Hardening stage: performance and E2E coverage

