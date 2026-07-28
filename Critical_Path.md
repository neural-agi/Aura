# Critical Path

Aura has a narrow architectural critical path even though the eventual product is broad. The project cannot safely accelerate around the runtime spine because almost every later subsystem depends on it.

## Highest-Impact Blockers

### 1. Repository bootstrap
This creates the actual engineering surface: solution structure, source layout, test layout, documentation anchors, and automation entry points. Without it, every later decision becomes ad hoc.

### 2. Application host, DI, logging, configuration, diagnostics
These establish the composition root and the ability to observe failure. If the host cannot start deterministically, nothing above it is trustworthy.

### 3. Runtime Core
This is the first real product subsystem. It owns orchestration and defines how the resident application lives and dies.

### 4. Event System and State System
These define the runtime's information flow and legal behavior. If the event pipeline or state truth is unstable, context, interaction, surfaces, and recovery will all be reworked later.

### 5. Persistence System
Aura is a resident runtime. Durable state, recovery, retention, and settings cannot be treated as later concerns. They determine whether restart, failure, and continuity work at all.

### 6. Lifecycle Manager and Power Manager
These unlock the platform-specific session model: startup, login, lock, sleep, resume, shutdown, and suspend behavior.

### 7. Context and Priority
These are the first layers that decide what the runtime should care about. They gate interaction and surfacing decisions.

### 8. Interaction and Surface contracts
These create the first user-facing value, but only after the state model is stable enough to avoid rework.

### 9. Artifact, History, Search, Settings
These add continuity, retrieval, and user control. They depend on the runtime being able to preserve and recover truth.

### 10. Plugin Runtime
Plugins should be one of the last major architectural layers because they multiply contract surface area and failure modes.

## Why these are critical

The critical path is not the first visible UI. The critical path is the sequence of layers that make every future subsystem trustworthy:

- bootstrap establishes buildability,
- the host establishes execution,
- runtime core establishes orchestration,
- event/state establish truth,
- persistence establishes recovery,
- lifecycle establishes resident behavior,
- context/priority establish relevance,
- interaction/surface establish usable expression,
- artifact/search/settings establish continuity and user control,
- plugins establish safe extensibility.

If any of those layers is built out of order, later work will be forced to compensate for earlier guesses.

## What blocks the most downstream work

The largest blockers are:

- Runtime Core, because nothing else can live without it.
- State System, because every visible and recoverable behavior depends on legal state.
- Persistence System, because restart and recovery need durable truth.
- Interaction/Surface contracts, because they unlock visible product behavior and expose whether the runtime model is actually usable.

## What can wait

The following can be delayed without damaging the architecture, as long as their contracts are preserved:

- richer animations,
- broad feature expansion,
- plugin marketplace mechanics,
- advanced search tuning,
- performance optimizations beyond the baseline,
- packaged release polish.

Those are important later, but they are not on the true critical path.

