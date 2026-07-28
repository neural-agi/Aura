# Validation Gates

## Gate 1 - Bootstrap Review

**When:** After Milestone 1.

**Checks:** Repository builds, host starts, tests execute, automation runs, docs are in place.

**Pass Criteria:** The project is structurally ready for subsystem work.

## Gate 2 - Runtime Review

**When:** After Milestone 2.

**Checks:** Runtime orchestration, event flow, state legality, persistence round-trips, lifecycle transitions, power handling.

**Pass Criteria:** The resident runtime works deterministically without user-facing feature work.

## Gate 3 - State Review

**When:** After Milestone 3.

**Checks:** Context classification, priority arbitration, suppression, queueing, merge behavior, policy enforcement.

**Pass Criteria:** The runtime makes stable decisions about relevance and allowed behavior.

## Gate 4 - Interaction Review

**When:** After Milestone 4.

**Checks:** Surface lifecycle, user intent routing, dismiss/expand/compact behavior, accessibility baseline, animation-state consistency.

**Pass Criteria:** The UI expresses state truth without drifting from it.

## Gate 5 - Continuity Review

**When:** After Milestone 5.

**Checks:** Artifact persistence, history recovery, search recall, settings persistence, restart behavior.

**Pass Criteria:** User continuity survives interruption and restart.

## Gate 6 - Security and Plugin Review

**When:** After Milestone 6.

**Checks:** Permission boundaries, plugin isolation, failure containment, compatibility handling, no core compromise.

**Pass Criteria:** Extensions are safe to enable in the same process model.

## Gate 7 - Feature Integration Review

**When:** After Milestone 7.

**Checks:** Broader feature modules integrate through documented contracts, no new circular dependencies, no ownership drift.

**Pass Criteria:** Feature expansion has not damaged the architecture.

## Gate 8 - Performance and Accessibility Review

**When:** After Milestone 8.

**Checks:** Benchmarks, startup time, memory, idle CPU, keyboard paths, screen reader behavior, reduced motion, high contrast, DPI scaling.

**Pass Criteria:** The app is ready for long-lived production use.

## Gate Rules

- A gate is mandatory before the next milestone begins.
- A gate can fail for architecture, behavior, performance, or test coverage.
- A failed gate requires correction before any further scope is added.
- Gates should be documented in the repository and reflected in CI where practical.

