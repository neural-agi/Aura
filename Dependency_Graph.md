# Dependency Graph

This graph lists the major subsystems in dependency order, with the architectural direction running from infrastructure toward expressive behavior.

## Foundation Layer

### Repository Bootstrap
- Requires: Nothing
- Provides: Solution layout, build entry points, docs structure, test structure
- Blocks: Everything else
- Can be implemented independently: Yes
- Can be implemented in parallel: No
- Can be postponed safely: No

### Application Host
- Requires: Repository bootstrap
- Provides: Process startup, shutdown, app composition root
- Blocks: Runtime Core, diagnostics, configuration, tests that need a runnable host
- Can be implemented independently: Partially
- Can be implemented in parallel: Yes, with build/bootstrap
- Can be postponed safely: No

### Dependency Injection
- Requires: Application host
- Provides: Composition wiring, lifetime boundaries
- Blocks: Runtime Core and all subsystem registration
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes, with logging/config
- Can be postponed safely: No

### Configuration
- Requires: Application host, DI
- Provides: Typed settings, environment resolution, boot configuration
- Blocks: Runtime Core startup, settings, policy, diagnostics
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Logging
- Requires: Application host, DI, configuration
- Provides: Structured diagnostic output
- Blocks: Diagnostics, runtime observability, CI validation
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Diagnostics Foundation
- Requires: Logging, configuration
- Provides: Health reporting, failure reporting, observability contracts
- Blocks: Runtime validation, plugin failure handling, recovery visibility
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Shared Kernel
- Requires: Repository bootstrap
- Provides: Common types, result primitives, event identifiers, utility abstractions
- Blocks: All major subsystems by type dependency
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

## Core Runtime Layer

### Runtime Core
- Requires: Host, DI, logging, configuration, diagnostics, shared kernel
- Provides: Orchestration, readiness, lifecycle ownership
- Blocks: Most resident subsystems
- Can be implemented independently: No
- Can be implemented in parallel: With event and state contract design only
- Can be postponed safely: No

### Event System
- Requires: Runtime Core, shared kernel, logging, diagnostics
- Provides: Normalized event flow, subscription, validation
- Blocks: Context, priority, artifact, history, plugin event routing
- Can be implemented independently: Partially
- Can be implemented in parallel: Yes, with state contract work
- Can be postponed safely: No

### State System
- Requires: Runtime Core, shared kernel, logging, diagnostics
- Provides: Legal runtime truth, transitions, recovery
- Blocks: Interaction, surface, animation, lifecycle coordination
- Can be implemented independently: Partially
- Can be implemented in parallel: Yes, with event system
- Can be postponed safely: No

### Persistence System
- Requires: Runtime Core, configuration, logging, diagnostics, shared kernel
- Provides: Durable state, recovery data, retention policy enforcement
- Blocks: Startup restoration, history, settings, artifact recovery, plugin state
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Lifecycle Manager
- Requires: Runtime Core, State System, Persistence System, Power Manager contract
- Provides: Startup/login/lock/sleep/resume/shutdown coordination
- Blocks: Resident runtime transitions
- Can be implemented independently: No
- Can be implemented in parallel: After state and persistence stabilize
- Can be postponed safely: No

### Power Manager
- Requires: Runtime Core, State System, shared kernel
- Provides: Power and session state interpretation
- Blocks: Suspend/resume behavior and power-aware scheduling
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Scheduler
- Requires: Runtime Core, Persistence System, Power Manager, State System
- Provides: Deferred work, retry, cleanup timing
- Blocks: Recovery retries, maintenance, deferred actions
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: Yes, until runtime spine is stable

## Meaning and Presentation Layer

### Context System
- Requires: Event System, Power Manager, Persistence System
- Provides: Current work situation, confidence, environment state
- Blocks: Priority, suppression, context-aware presentation
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Priority System
- Requires: Context System, Event System, State System
- Provides: Relevance, urgency, suppression, merge, queue decisions
- Blocks: Interaction timing, surface visibility
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Interaction System
- Requires: State System, Surface System contract, shared kernel
- Provides: User intent resolution, commands, accessibility actions
- Blocks: Most user-driven behaviors
- Can be implemented independently: Partially
- Can be implemented in parallel: After state and surface contracts
- Can be postponed safely: No

### Surface System
- Requires: State System, Interaction System contract, Animation contract
- Provides: Visible state, placement, recovery of surfaces
- Blocks: Any visible experience
- Can be implemented independently: Partially
- Can be implemented in parallel: After state contract
- Can be postponed safely: No

### Animation System
- Requires: State System, Surface System, accessibility policy
- Provides: Motion communication, reduced motion semantics
- Blocks: Polished state expression
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: Yes, after minimal surface rendering exists

## Memory and Retrieval Layer

### Artifact System
- Requires: Event System, Persistence System, Search contract
- Provides: Artifact lifecycle, recovery, categorization
- Blocks: Clipboard, downloads, screenshots, temporary objects
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### History System
- Requires: Event System, Interaction System, Persistence System
- Provides: Event and action record, recency, recall data
- Blocks: Search recall and recovery flows
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Search System
- Requires: Artifact System, History System, Persistence System, Context System
- Provides: Retrieval, ranking, recall, recent results
- Blocks: Search-driven recall and recovery
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

### Settings System
- Requires: Persistence System, Security & Policy contract, Runtime Core
- Provides: User preferences, behavior policy, accessibility preferences
- Blocks: User-configured behavior
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes
- Can be postponed safely: No

## Platform Extension Layer

### Plugin Runtime
- Requires: Runtime Core, Persistence System, Diagnostics, Security & Policy
- Provides: Extension lifecycle, permissions, isolation
- Blocks: Plugin-based extension growth
- Can be implemented independently: Yes
- Can be implemented in parallel: Yes, after core contracts stabilize
- Can be postponed safely: Yes

### Security & Policy
- Requires: Persistence System, Runtime Core, Settings System
- Provides: Permission boundaries, privacy enforcement, retention policy
- Blocks: Unsafe behavior across the runtime
- Can be implemented independently: Partially
- Can be implemented in parallel: Yes
- Can be postponed safely: No

## True Critical Path

The non-negotiable path is:

Repository Bootstrap -> Application Host -> DI/Logging/Configuration/Diagnostics -> Runtime Core -> Event System -> State System -> Persistence System -> Lifecycle Manager -> Context System -> Priority System -> Interaction/System Surface contracts -> Artifact/History/Search/Settings -> Plugin Runtime -> broader feature modules.

