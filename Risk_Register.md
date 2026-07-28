# Risk Register

## Milestone 1 - Bootstrap and Foundation

**Risks:** Build system drift, folder sprawl, weak host structure, missing diagnostics.

**Probability:** Medium

**Impact:** High

**Recovery strategy:** Rework the bootstrap before any feature work begins.

**Mitigation strategy:** Keep the first milestone small, documented, and host-centric.

## Milestone 2 - Runtime Spine

**Risks:** Event/state coupling, persistence semantics drift, lifecycle ambiguity, recovery holes.

**Probability:** High

**Impact:** Very High

**Recovery strategy:** Freeze contracts and fix the spine before moving upward.

**Mitigation strategy:** Use contract tests and architecture review before user-facing work.

## Milestone 3 - Context and Priority

**Risks:** Overfitting rules, noisy inference, hidden policy coupling.

**Probability:** Medium

**Impact:** High

**Recovery strategy:** Simplify rules and prefer conservative fallback behavior.

**Mitigation strategy:** Build deterministic tests around stable input sets.

## Milestone 4 - Interaction and Surface

**Risks:** UI leading the architecture, accessibility regressions, state drift in visible surfaces.

**Probability:** High

**Impact:** High

**Recovery strategy:** Re-anchor the UI on state and contract boundaries.

**Mitigation strategy:** Require state-machine and accessibility validation before expanding UI scope.

## Milestone 5 - Artifact, History, Search, Settings

**Risks:** Retrieval policy confusion, persistence corruption handling, duplicate ownership, stale recall.

**Probability:** Medium

**Impact:** High

**Recovery strategy:** Tighten ownership and recovery contracts.

**Mitigation strategy:** Treat restore and recall as first-class tests.

## Milestone 6 - Plugins and Security

**Risks:** Boundary leaks, unsafe extension behavior, privilege creep, incompatible plugin assumptions.

**Probability:** Medium

**Impact:** Very High

**Recovery strategy:** Disable plugin paths until isolation is proven.

**Mitigation strategy:** Enforce least privilege and fail closed behavior.

## Milestone 7 - Feature Expansion

**Risks:** Feature layering outpaces architecture, dependency sprawl, regression growth.

**Probability:** High

**Impact:** Medium to High

**Recovery strategy:** Pause expansion and pay down structural debt.

**Mitigation strategy:** Keep each feature module thin and contract-driven.

## Milestone 8 - Hardening and Release

**Risks:** Performance surprises, accessibility gaps, packaging issues, CI flakiness.

**Probability:** Medium

**Impact:** High

**Recovery strategy:** Reserve time for stabilization and benchmarking.

**Mitigation strategy:** Make performance, accessibility, and packaging explicit gates rather than afterthoughts.

## Cross-Cutting Risks

- Cross-subsidy between subsystems that should be independent.
- Temporary scaffolding becoming permanent architecture.
- Tests validating implementation details instead of contracts.
- Docs drifting from code because ownership was not made explicit.

## Recovery Principle

The right response to architectural risk is not to move faster. It is to slow down at the boundary that is unstable and make that boundary testable before more code accumulates on top of it.

