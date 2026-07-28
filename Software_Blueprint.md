# Software Blueprint

This document defines how Aura's frozen architecture maps into the codebase. It is the code-organization constitution for implementation work. It does not add features, alter subsystem boundaries, or change the product model.

---

## 1. Solution Architecture

### Solution name
`Aura.sln`

### Proposed project layout

#### `Aura.App`
The executable application entry point and composition root.

**Responsibilities**
- Process startup and shutdown.
- Build the application host.
- Wire configuration, logging, and dependency injection.
- Launch the runtime and coordinate top-level app state.

**Allowed dependencies**
- `Aura.Runtime`
- `Aura.Infrastructure`
- `Aura.Diagnostics`
- `Aura.Shared`

**Forbidden dependencies**
- `Aura.UI` internals
- `Aura.Features`
- `Aura.Plugins`
- `Aura.Tests`

#### `Aura.Runtime`
The resident runtime core and orchestrator.

**Responsibilities**
- Runtime Core orchestration.
- Lifecycle coordination.
- Event orchestration.
- State orchestration.
- Runtime readiness and shutdown coordination.

**Allowed dependencies**
- `Aura.Contracts`
- `Aura.Shared`
- `Aura.Infrastructure`

**Forbidden dependencies**
- `Aura.UI`
- `Aura.Features`
- `Aura.Plugins`

#### `Aura.Contracts`
Shared public contracts used across projects.

**Responsibilities**
- Interfaces, contract records, enums, and event abstractions.
- Stable boundary types shared by multiple projects.

**Allowed dependencies**
- `Aura.Shared`

**Forbidden dependencies**
- Any feature, UI, runtime, or infrastructure implementation project

#### `Aura.Shared`
Common primitives and value objects.

**Responsibilities**
- Results, identifiers, guards, base models, immutable shared types.
- No business behavior.

**Allowed dependencies**
- None or only framework base libraries

**Forbidden dependencies**
- All feature, runtime, UI, plugin, and infrastructure projects

#### `Aura.Infrastructure`
Cross-cutting technical services.

**Responsibilities**
- Configuration loading.
- Logging setup and delivery.
- Persistence plumbing.
- Serialization.
- filesystem and OS integration services that are not product features.

**Allowed dependencies**
- `Aura.Contracts`
- `Aura.Shared`

**Forbidden dependencies**
- `Aura.UI`
- `Aura.Features`
- `Aura.Plugins`

#### `Aura.Diagnostics`
Diagnostics and observability services.

**Responsibilities**
- Health reporting.
- Metrics and tracing adapters.
- Failure reporting.

**Allowed dependencies**
- `Aura.Contracts`
- `Aura.Shared`
- `Aura.Infrastructure`

**Forbidden dependencies**
- UI and feature projects

#### `Aura.UI`
WinUI 3 application shell and presentation layer.

**Responsibilities**
- Windows application windows, views, view models, and bindings.
- Presentation of runtime state.
- User interaction projection.

**Allowed dependencies**
- `Aura.Runtime`
- `Aura.Contracts`
- `Aura.Shared`
- `Aura.Diagnostics`

**Forbidden dependencies**
- `Aura.Plugins`
- Direct access to infrastructure internals

#### `Aura.Features`
Feature modules that sit on top of the core runtime.

**Responsibilities**
- Artifact-related workflows.
- Search-related workflows.
- Settings-related workflows.
- History-related workflows.
- Feature orchestration that belongs above the core runtime.

**Allowed dependencies**
- `Aura.Contracts`
- `Aura.Runtime`
- `Aura.Shared`
- `Aura.Infrastructure`

**Forbidden dependencies**
- `Aura.UI` internals
- `Aura.Plugins`
- Direct platform internals

#### `Aura.Plugins`
Plugin runtime, adapters, and extension loading.

**Responsibilities**
- Plugin discovery and registration.
- Permission and capability enforcement.
- Plugin lifecycle and isolation plumbing.

**Allowed dependencies**
- `Aura.Contracts`
- `Aura.Runtime`
- `Aura.Infrastructure`
- `Aura.Diagnostics`

**Forbidden dependencies**
- `Aura.UI` internals
- Feature internals

#### `Aura.Tests`
Test projects grouped by test type.

**Responsibilities**
- Unit tests.
- Integration tests.
- End-to-end tests.
- Accessibility tests.
- Performance tests.

**Allowed dependencies**
- All production projects as test targets

**Forbidden dependencies**
- Any test-only reverse coupling into production code

---

## 2. Project Dependency Graph

### Dependency order

`Aura.Shared`
-> `Aura.Contracts`
-> `Aura.Infrastructure`
-> `Aura.Diagnostics`
-> `Aura.Runtime`
-> `Aura.UI`
-> `Aura.Features`
-> `Aura.Plugins`

`Aura.Tests` depends on the production projects it validates.

### Per-project dependency details

#### Aura.Shared
- Depends On: None
- Used By: All other projects
- Public Contracts: Immutable primitives, common result types, identifiers
- Internal Responsibilities: Shared utility and value semantics

#### Aura.Contracts
- Depends On: `Aura.Shared`
- Used By: Runtime, UI, Infrastructure, Diagnostics, Features, Plugins, Tests
- Public Contracts: Interfaces, enums, events, DTOs, boundary abstractions
- Internal Responsibilities: Stable type vocabulary

#### Aura.Infrastructure
- Depends On: `Aura.Contracts`, `Aura.Shared`
- Used By: Runtime, Diagnostics, UI, Plugins, Features
- Public Contracts: Technical services, persistence plumbing, configuration access
- Internal Responsibilities: Technical implementation details that should not leak upward

#### Aura.Diagnostics
- Depends On: `Aura.Contracts`, `Aura.Shared`, `Aura.Infrastructure`
- Used By: App, Runtime, UI, Features, Plugins
- Public Contracts: Health, tracing, metrics, failure reporting
- Internal Responsibilities: Observability and diagnostics

#### Aura.Runtime
- Depends On: `Aura.Contracts`, `Aura.Shared`, `Aura.Infrastructure`, `Aura.Diagnostics`
- Used By: UI, Features, Plugins, App
- Public Contracts: Runtime core, state, lifecycle, event orchestration
- Internal Responsibilities: Orchestration and resident behavior

#### Aura.UI
- Depends On: `Aura.Runtime`, `Aura.Contracts`, `Aura.Shared`, `Aura.Diagnostics`
- Used By: App only
- Public Contracts: Views, view models, presentation services
- Internal Responsibilities: Presentation and input projection

#### Aura.Features
- Depends On: `Aura.Runtime`, `Aura.Contracts`, `Aura.Shared`, `Aura.Infrastructure`, `Aura.Diagnostics`
- Used By: UI, App
- Public Contracts: Feature modules, workflows, feature services
- Internal Responsibilities: Feature-specific logic above the runtime

#### Aura.Plugins
- Depends On: `Aura.Runtime`, `Aura.Contracts`, `Aura.Infrastructure`, `Aura.Diagnostics`
- Used By: Runtime and feature integration points
- Public Contracts: Plugin host, plugin contracts, plugin adapters
- Internal Responsibilities: Extension loading, isolation, enforcement

#### Aura.Tests
- Depends On: Production projects under test
- Used By: CI and local development
- Public Contracts: Test fixtures and assertions only
- Internal Responsibilities: Verification

### Circular reference rule
No project may reference a project at the same or higher layer.
No project may reference `Aura.UI` from runtime, infrastructure, or plugin internals.
No feature project may become a hidden runtime layer.

---

## 3. Namespace Strategy

Namespaces should reflect ownership, not physical folder convenience.

### Root namespaces
- `Aura`
- `Aura.Contracts`
- `Aura.Shared`
- `Aura.Infrastructure`
- `Aura.Diagnostics`
- `Aura.Runtime`
- `Aura.UI`
- `Aura.Features`
- `Aura.Plugins`

### Namespace conventions
- Use singular namespaces for ownership domains.
- Use sub-namespaces for bounded subdomains.
- Keep public namespaces stable once published.
- Use `Internal` sub-namespaces only for implementation details that are not part of the public code contract.

### Sub-namespace pattern
`Aura.Runtime.Events`
`Aura.Runtime.State`
`Aura.Runtime.Lifecycle`
`Aura.Runtime.Persistence`
`Aura.UI.Surfaces`
`Aura.UI.Interaction`
`Aura.Features.Search`
`Aura.Features.Artifacts`
`Aura.Plugins.Runtime`

### Ownership boundaries
- Runtime namespaces own orchestration and state truth.
- UI namespaces own presentation only.
- Infrastructure namespaces own technical plumbing only.
- Features namespaces own product behaviors that sit above the runtime.
- Plugin namespaces own extension hosting and isolation.

---

## 4. Folder Mapping

### Architecture to repository mapping

#### Runtime Core
- Project: `Aura.Runtime`
- Namespace: `Aura.Runtime`
- Folders: `RuntimeCore/`, `Lifecycle/`, `State/`, `Events/`

#### Event System
- Project: `Aura.Runtime`
- Namespace: `Aura.Runtime.Events`
- Folders: `Events/Contracts/`, `Events/Dispatch/`, `Events/Validation/`

#### State System
- Project: `Aura.Runtime`
- Namespace: `Aura.Runtime.State`
- Folders: `State/Model/`, `State/Transitions/`, `State/Recovery/`

#### Persistence System
- Project: `Aura.Infrastructure`
- Namespace: `Aura.Infrastructure.Persistence`
- Folders: `Persistence/`, `Persistence/Stores/`, `Persistence/Migrations/`

#### Lifecycle Manager
- Project: `Aura.Runtime`
- Namespace: `Aura.Runtime.Lifecycle`
- Folders: `Lifecycle/`

#### Power Manager
- Project: `Aura.Runtime`
- Namespace: `Aura.Runtime.Power`
- Folders: `Power/`

#### Scheduler
- Project: `Aura.Runtime`
- Namespace: `Aura.Runtime.Scheduling`
- Folders: `Scheduling/`

#### Context System
- Project: `Aura.Runtime`
- Namespace: `Aura.Runtime.Context`
- Folders: `Context/`

#### Priority System
- Project: `Aura.Runtime`
- Namespace: `Aura.Runtime.Priority`
- Folders: `Priority/`

#### Interaction System
- Project: `Aura.UI`
- Namespace: `Aura.UI.Interaction`
- Folders: `Interaction/`

#### Surface System
- Project: `Aura.UI`
- Namespace: `Aura.UI.Surfaces`
- Folders: `Surfaces/`

#### Animation System
- Project: `Aura.UI`
- Namespace: `Aura.UI.Animation`
- Folders: `Animation/`

#### Artifact System
- Project: `Aura.Features`
- Namespace: `Aura.Features.Artifacts`
- Folders: `Artifacts/`

#### History System
- Project: `Aura.Features`
- Namespace: `Aura.Features.History`
- Folders: `History/`

#### Search System
- Project: `Aura.Features`
- Namespace: `Aura.Features.Search`
- Folders: `Search/`

#### Settings System
- Project: `Aura.Features`
- Namespace: `Aura.Features.Settings`
- Folders: `Settings/`

#### Plugin Runtime
- Project: `Aura.Plugins`
- Namespace: `Aura.Plugins.Runtime`
- Folders: `Plugins/Runtime/`, `Plugins/Isolation/`, `Plugins/Permissions/`

#### Diagnostics
- Project: `Aura.Diagnostics`
- Namespace: `Aura.Diagnostics`
- Folders: `Diagnostics/`, `Logging/`, `Metrics/`, `Tracing/`

#### Shared primitives
- Project: `Aura.Shared`
- Namespace: `Aura.Shared`
- Folders: `Primitives/`, `Results/`, `Guards/`, `Identifiers/`

---

## 5. Class Responsibility Matrix

### App layer

#### `AppHost`
- Responsibility: Build and start the process-level application host
- Non-responsibility: Business logic, state ownership, UI behavior
- Ownership boundary: Process bootstrap only

#### `AppBootstrapper`
- Responsibility: Load configuration, logging, DI, and launch sequence
- Non-responsibility: Runtime state or feature behavior

### Runtime layer

#### `RuntimeCore`
- Responsibility: Root orchestration, readiness, lifecycle coordination
- Non-responsibility: Context semantics, interaction logic, persistence policy

#### `LifecycleManager`
- Responsibility: Startup, login, lock, sleep, resume, shutdown orchestration
- Non-responsibility: State truth or UI rendering

#### `EventBus`
- Responsibility: Publish/subscribe event routing
- Non-responsibility: Event meaning

#### `StateMachine`
- Responsibility: Legal runtime state and transitions
- Non-responsibility: Presentation or storage mechanics

#### `PowerManager`
- Responsibility: Power and session-state interpretation
- Non-responsibility: User intent or artifact behavior

#### `Scheduler`
- Responsibility: Deferred and background work timing
- Non-responsibility: Business semantics of the work being scheduled

#### `ContextEngine`
- Responsibility: Work-situation inference
- Non-responsibility: User-facing interaction behavior

#### `PriorityEngine`
- Responsibility: Relevance, urgency, suppression, merge, queue decisions
- Non-responsibility: Surface rendering

### UI layer

#### `InteractionEngine`
- Responsibility: User intent routing into valid actions
- Non-responsibility: State ownership, artifact storage, search ranking

#### `SurfaceManager`
- Responsibility: Visible state management
- Non-responsibility: Underlying data ownership

#### `AnimationController`
- Responsibility: Motion communication for state transitions
- Non-responsibility: State truth or accessibility policy definition

### Feature layer

#### `ArtifactManager`
- Responsibility: Artifact lifecycle and identity
- Non-responsibility: Surface decisions or priority rules

#### `HistoryManager`
- Responsibility: Historical record and recall data
- Non-responsibility: Action semantics

#### `SearchEngine`
- Responsibility: Retrieval, ranking, and recall
- Non-responsibility: Storage policy

#### `SettingsManager`
- Responsibility: User preferences and configuration access
- Non-responsibility: Event meaning or state truth

### Infrastructure layer

#### `PersistenceSystem`
- Responsibility: Durable and recoverable storage
- Non-responsibility: Business meaning

#### `DiagnosticsService`
- Responsibility: Health, tracing, metrics, failure visibility
- Non-responsibility: Repair logic

#### `LoggingService`
- Responsibility: Logging initialization, sink management, structured log emission
- Non-responsibility: Health interpretation or diagnostics policy

#### `PluginManager`
- Responsibility: Extension lifecycle and containment
- Non-responsibility: Core runtime rules

#### `SecurityPolicy`
- Responsibility: Permissions, privacy, retention, boundary enforcement
- Non-responsibility: Product feature behavior

### Shared layer

#### `Result`
- Responsibility: Explicit success/failure transport
- Non-responsibility: Exception handling policy

#### `Identifier`
- Responsibility: Stable identity semantics
- Non-responsibility: Business behavior

#### `Guard`
- Responsibility: Common validation helpers
- Non-responsibility: Domain rules

---

## 6. Interface Strategy

Interfaces should exist only where there is a real architectural boundary, alternate implementation, or test seam.

### Required interfaces

#### `IRuntimeCore`
- Why: Top-level orchestration boundary and test seam
- Consumers: App host, UI, diagnostics
- Ownership: `Aura.Runtime`
- Lifetime: Singleton

#### `IEventBus`
- Why: Event publication and subscription are shared contracts
- Consumers: Runtime, features, plugins
- Ownership: `Aura.Runtime`
- Lifetime: Singleton

#### `IStateMachine`
- Why: State truth needs a stable contract boundary
- Consumers: Runtime, UI, features
- Ownership: `Aura.Runtime`
- Lifetime: Singleton

#### `IPersistenceSystem`
- Why: Storage implementation may vary behind a stable contract
- Consumers: Runtime, features, diagnostics
- Ownership: `Aura.Infrastructure`
- Lifetime: Singleton with factory-backed stores

#### `ILifecycleManager`
- Why: Session and process lifecycle coordination is a first-class contract
- Consumers: App host, runtime
- Ownership: `Aura.Runtime`
- Lifetime: Singleton

#### `IPluginManager`
- Why: Plugin loading and isolation must remain swappable and testable
- Consumers: Runtime, diagnostics, features
- Ownership: `Aura.Plugins`
- Lifetime: Singleton

#### `ISettingsProvider`
- Why: Settings access should be separated from storage mechanics
- Consumers: Runtime, UI, features, plugins
- Ownership: `Aura.Features`
- Lifetime: Singleton

#### `IDiagnosticsSink`
- Why: Observability should be abstracted from concrete sinks
- Consumers: Runtime, infrastructure, plugins
- Ownership: `Aura.Diagnostics`
- Lifetime: Singleton

### Optional interfaces

Interfaces may also be introduced for:
- platform abstraction around Windows-only APIs,
- file system access,
- clock/time providers,
- background work scheduling,
- accessibility adapters.

Those should only exist when a real test or implementation boundary requires them.

### Avoided abstractions

Do not create interfaces for:
- internal leaf classes with only one implementation,
- data models,
- trivial helper utilities,
- UI view models unless multiple implementations are expected.

---

## 7. Lifetime Strategy

### Singleton
Use for:
- `RuntimeCore`
- `EventBus`
- `StateMachine`
- `LifecycleManager`
- `PowerManager`
- `Scheduler`
- `PersistenceSystem`
- `PluginManager`
- `SecurityPolicy`
- `DiagnosticsService`
- `SettingsManager`

**Why:** Aura is a resident runtime. These are system-wide coordination services, not per-request objects.

### Scoped
Use sparingly and only where a bounded operation context exists.

Potential uses:
- UI interaction sessions,
- plugin operation contexts,
- transient workflow execution contexts.

**Why:** Scoped lifetimes should exist only when a distinct unit of work genuinely needs isolation.

### Transient
Use for:
- stateless helpers,
- parsers,
- formatters,
- small coordinators,
- small command objects.

**Why:** Cheap, stateless, short-lived objects should not be held longer than necessary.

### Factory-created
Use for:
- plugins,
- platform adapters,
- persistence stores,
- per-surface runtime objects,
- background workers requiring explicit control.

**Why:** Some objects need construction-time parameters, special disposal, or conditional creation.

### Static
Use only for:
- pure constants,
- immutable mappings,
- trivial helpers with no dependency needs.

**Why:** Static state must remain rare in a resident runtime.

### Immutable value object
Use for:
- identifiers,
- state snapshots,
- configuration values,
- event metadata,
- result objects.

**Why:** Immutable values preserve determinism and make state flow easier to reason about.

---

## 8. Threading & Concurrency Model

### UI thread ownership
The WinUI UI thread owns all visual tree operations, windowing operations, binding updates, and user interaction rendering. No background service may mutate UI directly.

### Background execution
Background work owns:
- persistence I/O,
- indexing,
- recovery work,
- diagnostics aggregation,
- plugin loading and validation,
- scheduled maintenance.

### Event processing
Event normalization and dispatch should be deterministic and ordered at the system boundary. Event consumers may process in parallel only when their contract explicitly allows it and their state is independent.

### Dispatching
Cross-thread dispatch into the UI must occur through the UI dispatcher. Runtime services should never assume they can touch the UI thread synchronously.

### Scheduling
Scheduling must be owned by the runtime scheduler and never be ad hoc inside feature code.

### Synchronization
Use explicit synchronization only around shared runtime state that cannot be made immutable or serialized through the event/state model. Prefer message passing and ownership boundaries over locks.

### Cancellation
Long-running operations must accept cancellation and stop without leaving invalid partial state. Cancellation is part of the contract, not an error condition.

### Long-running tasks
Long-running tasks should be off the UI thread and should report progress or completion through documented contracts only.

### Concurrency rules
- State machine transitions should remain effectively single-source and serialized.
- Persistence writes should be serialized per storage domain.
- Search indexing and plugin loading may run concurrently with UI rendering if they do not violate state ownership.
- Surface updates must be synchronized to the UI thread.
- Recovery must be coordinated so that concurrent repair paths do not race against each other.

---

## 9. Event Contract Strategy

### Event ownership
The Event System owns event normalization, validation, routing, ordering, and subscription semantics.

### Publishers
Possible publishers include:
- Windows platform signals,
- runtime services,
- plugins,
- user-driven UI actions,
- scheduled runtime tasks.

### Consumers
Possible consumers include:
- Runtime Core,
- Context System,
- Priority System,
- State System,
- Artifact System,
- History System,
- Search System,
- Diagnostics.

### Event categories
Events should be grouped by architectural category, not by UI surface:
- lifecycle events,
- state events,
- context events,
- interaction events,
- artifact events,
- plugin events,
- system health events,
- power/session events.

### Dispatch rules
- Events are normalized before routing.
- Validation occurs before delivery.
- Consumers receive only events within their declared scope.
- Events should not bypass the event bus to reach downstream owners.

### Ordering guarantees
- Ordering must be deterministic within a given event stream.
- Causally related events should preserve their order.
- Independent categories may be processed separately when safe.

### Failure behavior
- Invalid events are rejected explicitly.
- Delivery failures are reported through diagnostics.
- A failed consumer must not corrupt event ownership for other consumers.

### Architectural constraint
Event payload shape is intentionally not defined here. This document defines event contracts, not feature payloads.

---

## 10. Startup Sequence

### Startup order

1. Process launch
2. Application host creation
3. Configuration loading
4. Logging initialization
5. Dependency injection container build
6. Shared kernel initialization
7. Diagnostics initialization
8. Runtime Core creation
9. Event System initialization
10. State System initialization
11. Persistence System initialization
12. Lifecycle Manager initialization
13. Power Manager initialization
14. Scheduler initialization
15. Context System initialization
16. Priority System initialization
17. Security and policy initialization
18. UI shell creation
19. Surface system readiness
20. Interaction readiness
21. Feature services initialization
22. Plugin runtime initialization where enabled
23. Ready state

### Why this order exists
- Configuration and logging must exist before any meaningful failure can be understood.
- Diagnostics must exist before runtime services begin making decisions.
- Runtime Core and state must exist before anything that depends on legal behavior.
- Persistence must be available before recovery-sensitive services start.
- Lifecycle and power must be ready before the app declares itself operational.
- UI should start only after the runtime can tell it what state is legal.
- Plugins should come late so they cannot destabilize the core initialization path.

### Startup rule
The app may enter a degraded ready state if a non-essential subsystem fails, but it must never pretend that a required subsystem is healthy when it is not.

---

## 11. Shutdown Sequence

### Shutdown order

1. Stop new external input
2. Stop plugin acceptance
3. Cancel background work
4. Flush transient runtime state
5. Persist durable state
6. Dispose feature services
7. Dispose UI surfaces
8. Dispose runtime services
9. Dispose diagnostics sinks
10. Dispose host
11. Final shutdown log emission

### Shutdown rules
- Shutdown must be deterministic.
- Shutdown should prefer graceful completion over abrupt disposal.
- Cancellation should be issued before disposal begins.
- Persistence flushing should happen before the runtime tears down its state owners.
- Plugins must terminate before core ownership disappears.
- UI must stop emitting user actions before underlying services are removed.

### Final diagnostics
The final shutdown path should emit enough information to explain whether the runtime stopped cleanly, degraded, or failed during disposal.

---

## 12. Error Propagation Strategy

### Error ownership
Every exception should have an owning subsystem. The owner is responsible for catching, translating, logging, and deciding whether the failure is recoverable.

### Where exceptions originate
Exceptions may originate in:
- platform integration,
- persistence I/O,
- plugin code,
- parsing and validation,
- unexpected runtime failures,
- UI event handling.

### Where they terminate
Exceptions should terminate at subsystem boundaries, not leak through the entire application path. Cross-boundary exceptions must be translated into documented failures.

### Logging boundaries
- Infrastructure and diagnostics layers log technical failures.
- UI logs presentation failures only when they affect user-visible behavior.
- Runtime logs orchestration and state failures.
- Plugins never bypass the host's logging boundary.

### Recovery boundaries
- Recoverable failures should be converted into bounded fallback states.
- Non-recoverable failures should be contained, logged, and surfaced through diagnostics.
- State corruption should trigger recovery paths, not blind continuation.

### Fatal failures
Fatal failures are reserved for cases where the runtime cannot establish or preserve a valid process state. Those failures should be rare and explicit.

### User-visible failures
User-visible errors should be translated into calm, bounded UI states rather than raw exception surfaces.

### Rule
Never allow a failure in one ownership domain to silently become another domain's responsibility.

---

## 13. Coding Standards

### Constructor injection
Use constructor injection for all non-trivial dependencies. Do not resolve services through hidden globals or service locators.

### Dependency direction
Dependencies should point inward toward stable contracts and shared abstractions, not outward toward implementation details.

### File organization
- One primary type per file.
- Files should live under the folder that matches their owning subsystem.
- Test files should mirror the production folder and namespace shape.

### Naming conventions
- Use PascalCase for types, interfaces, methods, and namespaces.
- Use explicit domain names instead of generic labels.
- Names should reflect ownership, not convenience.

### Visibility rules
- Make types internal by default unless they must be public.
- Expose only documented contracts publicly.
- Keep implementation helpers private or internal.

### Immutable models
- Prefer immutable value objects for identifiers, snapshots, and contracts.
- Mutable state should be limited to owning services.

### Async guidelines
- Use async for I/O, persistence, and long-running operations.
- Avoid `async void` except for UI event handlers.
- Do not block the UI thread on async work.
- Preserve cancellation semantics.

### Nullability
- Nullable reference types remain enabled.
- Avoid nullable ambiguity in public contracts.
- Use explicit option-like patterns when absence is meaningful.

### Documentation expectations
- Public classes and interfaces should be documented where the behavior is not obvious.
- Contract-level decisions must be captured in the blueprint or specification, not tribal memory.

---

## 14. Implementation Rules

These rules are mandatory for all future implementation work.

### Ownership rules
- Never bypass the State System.
- Never bypass the Event System.
- UI expresses state; it never owns state.
- No subsystem may violate documented ownership boundaries.
- No subsystem may silently take over another subsystem's responsibility.

### Dependency rules
- No circular dependencies.
- No upward dependencies from lower-level infrastructure into higher-level features.
- No hidden global state.
- No direct UI dependency from runtime or infrastructure internals.

### Contract rules
- Depend on documented contracts, not private implementation details.
- Do not invent undocumented APIs during implementation.
- Keep public contracts small and deterministic.

### Runtime rules
- State machine truth is authoritative.
- Persistence stores truth; it does not define truth.
- Search retrieves truth; it does not own truth.
- Plugins must fail closed.
- Recovery must restore valid state only.

### Threading rules
- UI work stays on the UI thread.
- Background work stays off the UI thread unless explicitly marshaled.
- Shared state should be minimized and serialized through ownership boundaries.

### Quality rules
- Every subsystem must remain testable in isolation.
- Every milestone must leave the repository buildable.
- Every new abstraction must justify its existence.
- Every architectural shortcut must be treated as debt unless documented otherwise.

### Change rules
- Do not collapse layers for convenience.
- Do not move responsibilities simply because a class is large.
- Do not optimize before the contract is proven.
- Do not introduce parallel architecture paths unless the spec explicitly requires them.

---

## 15. Open Ambiguities to Preserve

The blueprint intentionally leaves the following decisions to implementation within the frozen architecture:

- exact concrete class names below the owning service level,
- whether some leaf services are internal classes or nested private helpers,
- exact folder granularity inside each subsystem directory,
- exact state-machine representation form,
- exact event type taxonomy below the architectural categories,
- exact plugin loader implementation mechanics,
- exact view-model decomposition inside `Aura.UI`.

These are implementation details, not architectural decisions.
