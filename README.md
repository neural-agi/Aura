# Aura

Aura is a Windows-native productivity layer that reduces context switching by bringing the next useful thing to you without forcing you to go looking for it.

The Dynamic Island-style UI is only the container. The real product is a priority-aware workflow layer that transforms Events, Artifacts, Context, and Actions into timely interactions.

Aura exists for one reason: modern desktop work is fragmented. Notifications, clipboard items, screenshots, files, media controls, reminders, and small actions are scattered across too many apps and too many surfaces. Aura is designed to coordinate those fragments into one calm, coherent system.

## Why Aura Exists

Aura is built on a simple belief: attention is expensive, and software should protect it.

Modern computing interrupts people constantly. Every tiny switch between apps, windows, trays, and utilities adds up. Aura exists to reduce that cost by surfacing the right information and the right actions at the right moment.

## What Aura Is

Aura is:

- a Windows-native productivity layer
- a context-aware workflow system
- a priority engine for transient desktop information
- a surface for quick, useful actions
- a way to keep work moving without constant app switching

Aura is not:

- a second desktop
- a widget dashboard
- a gimmicky AI assistant
- a replacement for Windows
- another launcher pretending to be a product

## Core Ideas

Aura is organized around four core objects:

- **Event**: something happened
- **Artifact**: something was created, copied, captured, or received
- **Context**: what the user is doing
- **Action**: what Aura can help the user do next

Those objects are interpreted through priority, state, and interaction rules so Aura can decide whether to surface, stay silent, queue, merge, or recover.

## What Aura Should Help With

Aura is designed to make these kinds of desktop moments easier:

- clipboard recovery
- screenshot handling
- temporary file staging
- download recovery
- notification triage
- media controls
- quick actions
- search and recall
- context-aware surfaces
- graceful recovery after interruption

## Design Philosophy

Aura is guided by a few principles:

- reduce context switching
- preserve user attention
- stay silent whenever possible
- surface only what matters
- make transient information recoverable
- keep motion meaningful
- remain native to Windows
- feel calm, predictable, and useful

## Project Status

Aura is currently being defined through a full specification phase.

The project includes canonical documentation for:

- product requirements
- product philosophy
- personas
- user flows
- information architecture
- interaction model
- state machine
- animation system
- system architecture
- component tree
- API requirements
- MVP checklist
- design system
- backlog
- engineering principles

These documents are the source of truth for implementation.

## Documentation

Start here:

- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/00_Founder_Brainstorm.md`](docs/00_Founder_Brainstorm.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/01_Product_Requirements.md`](docs/01_Product_Requirements.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/02_Product_Manifesto.md`](docs/02_Product_Manifesto.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/03_User_Personas.md`](docs/03_User_Personas.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/04_User_Flows.md`](docs/04_User_Flows.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/05_Information_Architecture.md`](docs/05_Information_Architecture.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/06_Interaction_Model.md`](docs/06_Interaction_Model.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/07_State_Machine.md`](docs/07_State_Machine.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/08_Animation_System.md`](docs/08_Animation_System.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/09_System_Architecture.md`](docs/09_System_Architecture.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/10_Component_Tree.md`](docs/10_Component_Tree.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/11_API_Requirements.md`](docs/11_API_Requirements.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/12_MVP_Checklist.md`](docs/12_MVP_Checklist.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/13_Design_System.md`](docs/13_Design_System.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/14_Backlog.md`](docs/14_Backlog.md)
- [`D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/15_Engineering_Principles.md`](docs/15_Engineering_Principles.md)

## Architecture Overview

Aura is designed as a long-running runtime, not a one-off app.

At a high level, it is structured around:

- event observation and normalization
- context evaluation
- priority assignment
- deterministic state transitions
- surface management
- artifact management
- history and persistence
- recovery and graceful degradation
- plugin isolation
- performance-aware background operation

For implementation details, see the architecture documents in `docs/`.

## Contributing

Before contributing, read:

- `docs/02_Product_Manifesto.md`
- `docs/07_State_Machine.md`
- `docs/09_System_Architecture.md`
- `docs/10_Component_Tree.md`
- `docs/11_API_Requirements.md`
- `docs/15_Engineering_Principles.md`

Rules of the project:

- documentation is the source of truth
- subsystem ownership matters
- avoid inventing product behavior
- preserve determinism
- optimize for simplicity, recovery, and long-term maintainability

## Development

Development has not started yet.

When implementation begins, the first goal is not the UI. The first goal is a stable runtime foundation that can observe events, track context, assign priority, and remain recoverable under real desktop conditions.

## Roadmap

The implementation plan is organized around:

1. repository bootstrap
2. runtime foundation
3. event and context systems
4. state and surface systems
5. artifact handling
6. search and history
7. accessibility and performance
8. plugins and extensibility
9. release readiness

See `docs/12_MVP_Checklist.md` and `docs/14_Backlog.md` for the execution plan.

## License

See `LICENSE`.