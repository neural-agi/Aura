# Repository Evolution

Aura's repository should evolve in a way that mirrors the architecture rather than fighting it. The repo must remain understandable at every stage, which means new folders and projects should appear only when the subsystem beneath them is ready to exist.

## Empty Repository

The repository contains only governance and specification artifacts.

**Goal:** Establish the product contract, stack, and engineering rules before code begins.

## Bootstrap

The repo gains solution files, build scripts, CI entry points, and the first project structure.

**Expected shape:**

- `src/`
- `tests/`
- `architecture/`
- `docs/`

The first code should be tiny and infrastructural.

## Runtime

The repository gains the app host, shared kernel, runtime core, event system, persistence, logging, configuration, and diagnostics.

**Expected shape:** clear separation between application, runtime, infrastructure, and shared primitives.

## Core Services

Context, priority, state, lifecycle, power, scheduler, and policy contracts are added.

**Expected shape:** deterministic service layers beneath the application host and above platform integration.

## Platform Integration

Windows-specific behavior, session transitions, power handling, and native interop become visible in the appropriate infrastructure projects.

**Expected shape:** platform code remains isolated from business logic.

## State

State machine coverage expands with transition tests, recovery tests, and legal behavior modeling.

**Expected shape:** state truth is now deeply validated and documented.

## Interaction

Interaction and surface projects become meaningful and begin expressing runtime state to the user.

**Expected shape:** UI is a projection of state, not the source of truth.

## Surface

Surface management, compact/expanded behavior, placement, and lifecycle handling mature.

**Expected shape:** visible state becomes stable and predictable.

## Animation

Motion support grows once state and surfaces are already reliable.

**Expected shape:** animation code is clearly subordinate to state and accessibility.

## Features

Artifact, history, search, settings, and feature modules expand incrementally.

**Expected shape:** each feature lives in its own bounded module with clear dependencies.

## Plugins

Plugin runtime, sandboxing, permissions, and extension contracts are added after the core model has hardened.

**Expected shape:** plugins are isolated and cannot reshape architecture.

## Optimization

Performance, accessibility, packaging, release automation, benchmarks, and maintenance tooling are added as the system matures.

**Expected shape:** the repository remains clean, not cluttered with temporary experiments.

## Structural Rules

- Each subsystem should live in a clearly named folder or project.
- Shared abstractions should be promoted only when reused more than once.
- Infrastructure code should not leak into feature modules.
- UI code should not own domain truth.
- Tests should mirror the production boundaries.
- Documentation should remain close to the architectural layer it describes.

