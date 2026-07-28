# Phase A Mental Model

## 1. What Aura Is

Aura is a Windows-native desktop productivity layer whose job is to reduce context switching. It uses a Dynamic Island-style surface as its visible interaction container, but the product is not the pill, the animation, or a notification skin. Aura is the runtime behind that surface: a system that observes Events, tracks Artifacts, understands Context, ranks Priority, and exposes Actions only when doing so helps the user continue work with less friction.

The product is best understood as an OS-adjacent coordination layer for transient work. Windows constantly produces short-lived state: copied text, screenshots, downloads, notifications, media changes, meeting reminders, battery warnings, recent links, temporary files, and unfinished actions. Today the user manually coordinates that state by switching between apps, system trays, notification centers, file explorers, clipboard managers, browser tabs, and chat tools. Aura exists to coordinate those fragments into timely, recoverable, low-friction interactions.

Aura is not a second desktop, a taskbar replacement, a generic launcher, a widget dashboard, a weather panel, an AI chatbot, or a visual clone of Apple behavior. It complements Windows by making small, repeated workflow transitions faster and calmer.

### Derived From

- README.md
- AGENTS.md
- CODEX.md
- specs/00_Founder_Brainstorm.md
- specs/01_product_requirements.md
- specs/02_product_manifesto.md
- specs/05_information_architecture.md
- specs/aura_design_excercise_brainstorming.md

## 2. Why Aura Exists

Aura exists because modern desktop work is fragmented. The cost it attacks is not one large delay; it is the repeated attention tax caused by small interruptions and handoffs. A user copies something, loses it, downloads a file, hunts for it, receives a notification, decides whether it matters, changes media, checks a meeting link, stages a screenshot, and then has to rebuild the original task context. Each moment is small enough to tolerate and frequent enough to matter.

The central product promise is that Aura brings the next useful thing to the user without forcing the user to go looking for it. Its value is measured by preserved context, useful actions completed without switching apps, and the feeling that Windows is quietly helping the user stay in flow. Aura should minimize time spent inside Aura; it should help the user act and return to work.

### Derived From

- README.md
- ROADMAP.md
- specs/00_Founder_Brainstorm.md
- specs/01_product_requirements.md
- specs/02_product_manifesto.md
- specs/04_user_flows.md
- research/Dynamic_Island_Windows_Master_Strategy.md

## 3. Who Aura Serves

Aura is built for behavior patterns, not demographics. The primary users are people who spend sustained time on Windows and repeatedly move between apps, files, messages, browser tabs, clipboard items, media, and short-lived work artifacts.

The strongest MVP users are power users and developers because they are utility-sensitive, keyboard-heavy, interruption-aware, and quick to notice whether a tool saves time or adds overhead. Knowledge workers are the largest long-term market because they face high notification and coordination load. Students, creators/designers, and IT professionals are also strong fits because they share the same underlying friction: copied material, temporary files, recent references, noisy notifications, and context recovery.

Aura intentionally does not optimize first for users who mainly want visual novelty, heavy customization, a launcher, an AI chat interface, a Windows replacement, or a dashboard. Gamers are important for fullscreen restraint and performance validation, but they do not define the MVP.

### Derived From

- specs/01_product_requirements.md
- specs/03_user_personas.md
- specs/04_user_flows.md
- specs/12_mvp_checklist.md
- research/Dynamic_Island_Windows_Master_Strategy.md
- research/deep-research-report(Claude).md
- research/deep-research-report(Perplexity).md

## 4. What Makes Aura Unique

Aura's differentiation is not the top-center floating surface. Research treats the pill shape as a commodity. Aura is unique because it combines workflow utility, attention discipline, local context, deterministic state, and recoverability into one Windows-native runtime.

The product's durable model is the four-object system:

- Event: something happened.
- Artifact: something was created, copied, captured, downloaded, received, or produced.
- Context: what the user is doing now.
- Action: what Aura can help the user do next.

Priority connects these objects. Events carry urgency, artifacts carry relevance, context amplifies or suppresses meaning, and actions carry usefulness and friction cost. A surface should appear only when the combined signal justifies the user's attention.

Every surface must answer four questions quickly: what happened, why it matters, what the user can do, and what happens if the user ignores it. That rule separates Aura from notification feeds, dashboards, passive widgets, and decorative clones.

### Derived From

- specs/00_Founder_Brainstorm.md
- specs/01_product_requirements.md
- specs/05_information_architecture.md
- specs/06_interaction_model.md
- specs/aura_design_excercise_brainstorming.md
- research/Dynamic_Island_Windows_Master_Strategy.md
- research/deep-research-report(ChatGPT).md
- research/deep-research-report(Gemini).md
- research/deep-research-report(Perplexity).md

## 5. Core Philosophy

Aura is governed by restraint. It should appear only when valuable, stay silent whenever possible, communicate in one glance, allow one clear action, and return the user to work. Silence is not absence of behavior; it is an intentional product state when interruption cost exceeds value.

The UX philosophy is calm, compact, predictable, reversible, native, and accessible. Motion exists to explain state transitions, preserve object identity, and reduce uncertainty; stillness is equally valid when motion adds no meaning. The design system translates this philosophy into restrained color, strong typography, shallow layouts, consistent states, purposeful surfaces, visible focus, reduced-motion support, and component patterns that avoid ad hoc UI.

The engineering philosophy mirrors the product philosophy: simplicity, determinism, recoverability, explicit ownership, native-first integration, performance, accessibility, privacy, and security. The implementation exists to realize the specification, not redefine it.

### Derived From

- AGENTS.md
- CODEX.md
- CONTRIBUTING.md
- SECURITY.md
- specs/02_product_manifesto.md
- specs/06_interaction_model.md
- specs/08_animation_system.md
- specs/13_design_system.md
- specs/15_engineering_principles.md

## 6. Product Vision

The MVP should prove that Aura is useful enough to keep installed by solving repeated, visible desktop friction around clipboard recall, temporary file staging, notification triage, screenshots/downloads, media controls, search, recovery, keyboard invocation, and multi-monitor-aware placement.

Longer term, Aura should become a trusted Windows context layer: an OS-adjacent capability that users treat as part of their normal desktop rhythm. It may later support richer quick actions, plugins, app-specific workflows, automation, optional AI enrichment, cloud or cross-device continuity, and enterprise controls. Those future capabilities remain subordinate to the core mission: reduce context switching without becoming noisy, heavy, or conceptually broad.

### Derived From

- README.md
- ROADMAP.md
- specs/01_product_requirements.md
- specs/12_mvp_checklist.md
- specs/14_backlog.md
- research/Dynamic_Island_Windows_Master_Strategy.md

## 7. System Architecture

Aura is a long-running resident runtime composed of cooperating subsystems. The runtime is event-driven and state-aware. The high-level flow is:

Event Sources -> Runtime Core -> Event System -> Context System -> Priority System -> State System -> Interaction System -> Surface System -> Animation System, with Artifact, Search, Persistence, Plugin, Diagnostics, Security, Power, Lifecycle, and Scheduler systems supporting the loop.

Subsystem ownership is narrow:

- Runtime Core owns orchestration and readiness.
- Event System owns normalized event flow.
- Context System owns work-situation inference and confidence.
- Priority System owns relevance, urgency, suppression, queueing, merging, and conflict arbitration.
- State System owns legal runtime truth.
- Interaction System owns user intent routing.
- Surface System owns visible lifecycle and placement.
- Animation System owns motion expression downstream from state.
- Artifact System owns clipboard, screenshots, downloads, file shelf items, recent links, retention, and recovery.
- Search System owns retrieval and recall.
- Persistence System stores durable and recoverable state without defining behavior.
- Plugin Runtime owns extension lifecycle, permissions, compatibility, and isolation.
- Diagnostics observes health and failures without becoming a control plane.
- Security & Policy owns permissions, privacy, retention, access control, and boundaries.

The dependency direction is intentional: context informs priority, priority informs state, state authorizes interaction and surface, and surfaces never invent state.

### Derived From

- specs/05_information_architecture.md
- specs/07_state_machine.md
- specs/09_system_architecture.md
- specs/10_component_tree.md
- specs/11_api_requirements.md
- specs/15_engineering_principles.md

## 8. Runtime Model

Aura's runtime loop is deterministic:

Idle -> Observe -> Evaluate Context -> Assign Priority -> Decide -> Surface/Silence/Queue/Merge -> User Response -> Action -> Resolve -> Return to Idle.

Every event must eventually resolve by being surfaced, queued, merged, silenced, archived, ignored, or expired. No event should linger indefinitely. Every visible state must have priority and context behind it. Every action must originate from a valid state. Every failure must either recover or degrade to a stable state.

Core runtime states include Idle, Observing, Evaluating Context, Prioritizing, Silent, Pending, Queued, Merged, Surfacing, Compact, Expanded, Interactive, Executing Action, Waiting, Recovering, Suspended, Error, and Shutdown. Context states include coding, meeting, gaming, creative work, browsing, studying, focus session, presentation, offline, and battery saver. Context modifies existing rules; it does not create a second behavior system.

Persistence is divided into session state, persistent state, temporary state, cached state, history, recovery data, and configuration. Temporary objects should decay. Persistent objects should be explicit. Recovery data should be bounded and purposeful.

### Derived From

- specs/04_user_flows.md
- specs/05_information_architecture.md
- specs/06_interaction_model.md
- specs/07_state_machine.md
- specs/09_system_architecture.md
- specs/11_api_requirements.md
- specs/12_mvp_checklist.md

## 9. Interaction Model

Aura communicates proportionally. Its interaction types range from silent logging and passive observation to informational, actionable, critical, and emergency surfaces. Most interactions should live below critical. User-initiated search and recall are always valid recovery paths; automatic surfacing requires stronger justification.

Common flows follow the same pattern:

- Clipboard is usually captured silently and recalled on demand.
- File Shelf supports temporary staging, drag/drop, reuse, and expiration without becoming a file manager.
- Notifications are triaged, suppressed, merged, queued, or surfaced according to urgency and context.
- Media controls are low-pressure and generally user-invoked.
- Quick actions appear only when they clearly reduce steps.
- Search and Recents provide universal recovery.
- Screenshots and downloads become artifacts when they are likely useful to the current workflow.
- Meetings and battery/system events rise in priority when time or continuity is at risk.

Fullscreen, gaming, presentations, meetings, screen recording, and focus sessions require strong restraint. Multi-monitor behavior follows active context: the surface should appear near the user's current work, avoid jumping between displays, and preserve access when topology changes.

Accessibility is part of the interaction model: keyboard, pointer, touch, screen reader, high contrast, and reduced motion flows must preserve the same core outcomes.

### Derived From

- specs/03_user_personas.md
- specs/04_user_flows.md
- specs/06_interaction_model.md
- specs/07_state_machine.md
- specs/08_animation_system.md
- specs/13_design_system.md

## 10. Engineering Philosophy

Aura should be built as a maintainable resident runtime, not as a feature bundle. Engineering decisions must preserve subsystem ownership, documented APIs, deterministic behavior, recoverability, performance, accessibility, security, privacy, and testability.

Important invariants:

- One source of truth per domain.
- No duplicated authoritative state.
- No circular dependencies.
- No hidden communication.
- State drives behavior.
- Surfaces and animation express state; they do not create it.
- Persistence stores valid truth; it does not define behavior.
- Plugins extend the model; they do not bypass it.
- Diagnostics observe; they do not secretly control.
- Every visible behavior maps to documentation and state.

Testing must include unit, integration, state transition, recovery, failure, performance, accessibility, and regression validation. Review criteria prioritize architecture, readability, simplicity, performance, accessibility, maintainability, documentation alignment, and long-term impact.

### Derived From

- AGENTS.md
- CODEX.md
- CONTRIBUTING.md
- SECURITY.md
- specs/09_system_architecture.md
- specs/10_component_tree.md
- specs/11_api_requirements.md
- specs/12_mvp_checklist.md
- specs/15_engineering_principles.md

## 11. Technology Stack

The repository has a frozen Phase 1 technology stack in `TECH_STACK.md`. The implementation is Windows-native, with `C#` on `.NET 9`, targeting `Windows 11` and `x64` for the initial release, with `ARM64` planned later. The UI framework is `WinUI 3` on the latest stable Windows App SDK. The architectural style is `MVVM` within `Clean Architecture`, with dependency injection through `Microsoft.Extensions.DependencyInjection`.

The rest of the stack is equally explicit: logging with `Serilog`, configuration via `Microsoft.Extensions.Configuration`, serialization with `System.Text.Json` by default, `xUnit` for tests, `FluentAssertions` for assertions, `NSubstitute` for mocking, `BenchmarkDotNet` for benchmarking, `dotnet format` for formatting, GitHub Actions for CI, Roslyn analyzers and nullable reference types enabled, and Git/GitHub as the version-control and hosting layer.

The tech choices are deliberately narrow: native Windows integration, minimal third-party dependencies, strong performance, and a stable developer experience matter more than portability. Cross-platform UI frameworks are explicitly not part of the MVP.

The repository structure still anticipates the same major implementation areas:

- `src/runtime`, `src/platform`, `src/ui`, `src/features`, `src/infrastructure`, `src/plugins`, `src/shared`, `src/app`
- `tests/unit`, `tests/integration`, `tests/e2e`, `tests/performance`, `tests/accessibility`
- `design/tokens`, `design/icons`, `design/mockups`, `design/prototypes`
- `architecture/diagrams`, `architecture/exports`, `architecture/generated`
- `plugins/sdk`, `plugins/examples`, `plugins/registry`
- `installer`, `scripts`, `benchmarks`, `website`, `docs/decisions`

### Derived From

- TECH_STACK.md
- README.md
- CODEX.md
- ROADMAP.md
- specs/09_system_architecture.md
- specs/10_component_tree.md
- specs/11_api_requirements.md
- specs/12_mvp_checklist.md
- specs/13_design_system.md
- specs/15_engineering_principles.md
- Repository structure inspection

## 12. Architectural Constraints

Aura is specification-driven. Implementation must trace back to the product requirements, manifesto, user flows, IA, interaction model, state machine, animation system, system architecture, component tree, API requirements, MVP checklist, design system, backlog, and engineering principles.

Key constraints:

- Do not invent undocumented product behavior.
- Do not redesign architecture during implementation.
- Do not bypass subsystem APIs or ownership boundaries.
- Do not duplicate state or create parallel history, search, priority, or state systems.
- Do not let UI, animation, persistence, plugins, diagnostics, or telemetry redefine runtime truth.
- Do not surface anything without priority and context.
- Do not treat context as a second source of truth.
- Do not make core local utility depend on the internet.
- Do not let plugins arbitrarily interrupt, override priority rules, render uncontrolled UI, or access sensitive data without permission.
- Do not sacrifice accessibility, recovery, or performance for feature count or visual polish.
- Do not optimize before measuring.

When uncertainty remains after reading relevant docs, the correct behavior is to leave clear implementation notes or escalate, not to guess.

### Derived From

- AGENTS.md
- CODEX.md
- CONTRIBUTING.md
- SECURITY.md
- specs/05_information_architecture.md
- specs/07_state_machine.md
- specs/09_system_architecture.md
- specs/10_component_tree.md
- specs/11_api_requirements.md
- specs/12_mvp_checklist.md
- specs/15_engineering_principles.md

## 13. Key Project Assumptions

Aura assumes that high-frequency micro-friction is a real productivity cost and that reducing it is more valuable than adding another place to spend time. It assumes the best early market is Windows power users, developers, and other high-switching desktop workers. It assumes users will keep Aura installed only if it is useful daily, quiet by default, fast, reliable, privacy-conscious, and recoverable.

It assumes local-first core utility is mandatory: clipboard, notification triage basics, media controls, quick actions, file staging, local search, window/app context, keyboard shortcuts, priority logic, settings, and preferences should not require internet access. AI may enhance future context, summarization, or action ranking, but it is not the product's identity or MVP foundation.

It assumes Windows-native behavior is part of trust. Aura must handle fullscreen, gaming, presentations, multi-monitor setups, power transitions, sleep/wake, restarts, unavailable APIs, permission denial, and plugin failure without breaking the desktop or confusing the user.

It assumes the product will grow, so the architecture must support extension through Events, Artifacts, Context, Actions, Priority, History, Session, Surface, documented APIs, and plugin boundaries rather than by adding exceptional systems.

Finally, it assumes attention is finite. Aura earns trust not by appearing often, but by appearing for the right reasons and staying silent the rest of the time.

### Derived From

- README.md
- ROADMAP.md
- SECURITY.md
- specs/00_Founder_Brainstorm.md
- specs/01_product_requirements.md
- specs/02_product_manifesto.md
- specs/03_user_personas.md
- specs/04_user_flows.md
- specs/05_information_architecture.md
- specs/06_interaction_model.md
- specs/07_state_machine.md
- specs/08_animation_system.md
- specs/09_system_architecture.md
- specs/12_mvp_checklist.md
- specs/14_backlog.md
- research/Dynamic_Island_Windows_Master_Strategy.md
- research/deep-research-report(Claude).md
- research/deep-research-report(Perplexity).md
