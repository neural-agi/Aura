# CI/CD Roadmap

Aura's automation should mature alongside the repository. CI should validate the current stage of the architecture, not pretend the whole product exists on day one.

## Stage 1 - Bootstrap

**Checks:** Restore, build, formatting, basic linting, host smoke test.

**Goal:** Prove the repository is wired correctly.

## Stage 2 - Core Validation

**Checks:** Unit tests, contract tests, state-machine tests, configuration tests, logging tests.

**Goal:** Protect the runtime spine.

## Stage 3 - Integration Validation

**Checks:** Integration tests for lifecycle, persistence, event flow, recovery, and surface synchronization.

**Goal:** Ensure the major subsystems cooperate correctly.

## Stage 4 - Accessibility and Runtime Quality

**Checks:** Accessibility smoke checks, keyboard navigation checks, reduced-motion checks, DPI-aware rendering checks.

**Goal:** Make accessibility a gating concern rather than an afterthought.

## Stage 5 - Performance Validation

**Checks:** BenchmarkDotNet jobs, startup timing, memory trends, idle CPU checks.

**Goal:** Ensure Aura remains suitable as a resident runtime.

## Stage 6 - Packaging and Release

**Checks:** Packaging verification, installer validation, versioning checks, release artifact creation.

**Goal:** Produce a releaseable build without changing the architecture.

## Stage 7 - Long-Term Maintenance

**Checks:** Coverage reporting, dependency review, security scanning, regression suites, release smoke tests.

**Goal:** Keep the repository healthy after the first release.

## CI Principles

- CI should fail fast on structural problems.
- The earliest pipeline stages should be cheap and always-on.
- Heavier jobs should only appear once the codebase can benefit from them.
- Performance and accessibility should graduate from local checks to CI gates when their implementations exist.
- Release automation should come only after the product and test surface are stable.

