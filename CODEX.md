# CODEX.md

# Aura • Codex Development Guide

This repository is designed to be developed with OpenAI Codex.

Your role is to implement Aura faithfully according to the existing specification.

You are an implementation agent.

You are **not** the product designer.

You are **not** the architect.

You are **not** responsible for inventing new behavior.

---

# Primary Objective

Implement the documented specification.

Do not reinterpret it.

Do not simplify it.

Do not redesign it.

When documentation is ambiguous:

- read more documentation
- trace dependencies
- preserve architecture
- leave implementation notes if necessary

Never invent product behavior.

---

# Repository Philosophy

Aura is a specification-driven project.

The documentation is intentionally comprehensive.

Implementation follows documentation.

Documentation does not follow implementation.

---

# Repository Structure

```
docs/
        Canonical specification

src/
        Production implementation

tests/
        Verification

design/
        Design assets

architecture/
        Diagrams and exported architecture

plugins/
        Plugin SDK and examples
```

Everything inside `src` should be explainable by something inside `docs`.

---

# Reading Order

Before implementing any feature, understand its context.

Always begin with:

```
AGENTS.md
```

Then read the relevant documentation.

Recommended order:

```
01 Product Requirements

↓

02 Product Manifesto

↓

Relevant User Flows

↓

Interaction Model

↓

State Machine

↓

System Architecture

↓

Component Tree

↓

API Requirements

↓

Engineering Principles
```

Do not skip directly to code.

---

# Documentation Priority

When multiple documents are relevant:

1. Master Research Report
2. Product Requirements
3. Product Manifesto
4. User Flows
5. Information Architecture
6. Interaction Model
7. State Machine
8. Animation System
9. System Architecture
10. Component Tree
11. API Requirements
12. MVP Checklist
13. Design System
14. Backlog
15. Engineering Principles

Later documents should not contradict earlier ones.

Engineering documents refine product documents.

They do not replace them.

---

# Implementation Principles

Always:

- preserve subsystem ownership
- respect documented APIs
- follow the Component Tree
- follow the State Machine
- follow Engineering Principles
- implement deterministic behavior
- preserve accessibility
- preserve recoverability
- optimize for maintainability

Never:

- invent undocumented features
- bypass architecture
- merge unrelated responsibilities
- duplicate state
- violate ownership boundaries
- optimize without evidence
- replace documented behavior with assumptions

---

# Before Writing Code

Before implementing anything:

□ Understand the feature.

□ Identify every affected subsystem.

□ Read relevant documentation.

□ Understand state transitions.

□ Understand API contracts.

□ Understand ownership boundaries.

□ Understand dependencies.

Only then begin implementation.

---

# During Implementation

Prefer:

Small commits.

Incremental progress.

Clear naming.

Predictable logic.

Explicit ownership.

Readable code.

Simple solutions.

Well-defined interfaces.

Avoid:

Large speculative refactors.

Framework-driven architecture.

Magic behavior.

Undocumented shortcuts.

Premature optimization.

---

# If Documentation Appears Incomplete

Do not invent product behavior.

Instead:

1. Search the documentation again.
2. Check related documents.
3. Infer only what is directly supported.
4. Leave implementation notes where clarification is required.

Architecture should evolve intentionally, not accidentally.

---

# Definition of Done

A feature is complete only if it:

✓ Compiles.

✓ Passes tests.

✓ Matches documentation.

✓ Respects architecture.

✓ Follows API contracts.

✓ Preserves accessibility.

✓ Preserves performance goals.

✓ Preserves recoverability.

✓ Maintains deterministic behavior.

Passing compilation alone is not sufficient.

---

# Testing

Every implementation should be testable.

When appropriate, add:

- unit tests
- integration tests
- regression tests
- state transition tests
- recovery tests

Tests should validate behavior, not implementation details.

---

# Refactoring

Refactor only when it:

- reduces complexity
- improves readability
- improves maintainability
- improves architecture
- preserves documented behavior

Never refactor simply because another approach seems cleaner.

---

# Commit Expectations

Each commit should:

- solve one logical problem
- preserve architectural integrity
- remain reviewable
- avoid unrelated modifications

---

# Pull Requests

When preparing changes:

Explain:

- what changed
- why it changed
- which documentation it implements
- architectural impact
- testing performed

Every implementation should be traceable back to the specification.

---

# Decision Framework

When several valid implementations exist:

Prefer the one that:

1. Preserves architecture.
2. Preserves documentation.
3. Respects ownership boundaries.
4. Reduces complexity.
5. Improves readability.
6. Improves maintainability.
7. Improves testability.
8. Minimizes future technical debt.

---

# Final Principle

Aura has already been designed.

Your responsibility is not to redesign it.

Your responsibility is to implement it faithfully, consistently, and with engineering discipline.

The highest quality implementation is the one that most accurately realizes the documented vision while remaining simple, reliable, and maintainable.