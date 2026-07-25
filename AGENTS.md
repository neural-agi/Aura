# AGENTS.md

# Aura AI Development Guide

Welcome to Aura.

This repository is **specification-driven**.

The purpose of this document is to establish the rules every AI coding agent must follow before modifying this project.

These rules apply regardless of model, provider, or implementation environment.

---

# Mission

Aura is a Windows-native productivity layer that reduces context switching by transforming Events, Artifacts, Context, and Actions into timely interactions.

The implementation exists to realize the specification.

It does **not** redefine the product.

---

# Source of Truth

When making implementation decisions, always follow this order of authority.

1. `D:/PARANJAY/Projects/Dynamic-Island-Windows/docs/docs/research/Master_Research_Report.md`
2. Documentation inside `D:/PARANJAY/Projects/Dynamic-Island-Windows/docs`
3. `AGENTS.md`
4. Existing implementation
5. Personal reasoning

If implementation conflicts with documentation:

**The documentation is correct until explicitly updated.**

Never modify architecture simply because another implementation appears easier.

---

# Philosophy

Aura values:

- Simplicity
- Determinism
- Recoverability
- Native Windows behavior
- Long-term maintainability
- Accessibility
- Calm interaction
- Predictable state
- Performance
- User trust

Optimize for these values.

Never optimize solely for fewer lines of code.

---

# Before Implementing Anything

Before writing code:

1. Identify the feature being implemented.
2. Read every relevant document.
3. Understand subsystem ownership.
4. Understand state transitions.
5. Understand API contracts.
6. Understand component boundaries.
7. Verify MVP requirements.
8. Verify Engineering Principles.

Never skip this process.

---

# Documentation Reading Guide

## Product

- Product Requirements
- Product Manifesto
- User Personas

These explain **why** Aura exists.

---

## Behavior

- User Flows
- Information Architecture
- Interaction Model
- State Machine
- Animation System

These explain **how Aura behaves**.

---

## Engineering

- System Architecture
- Component Tree
- API Requirements
- MVP Checklist
- Design System
- Backlog
- Engineering Principles

These explain **how Aura is built**.

---

# Engineering Rules

Always:

- respect subsystem ownership
- preserve architectural boundaries
- follow public APIs
- keep components cohesive
- keep dependencies explicit
- prefer composition
- preserve determinism
- write testable code
- write understandable code
- preserve accessibility
- preserve recovery behavior

Never:

- introduce hidden coupling
- bypass subsystem APIs
- duplicate state
- invent undocumented product behavior
- create circular dependencies
- violate ownership boundaries
- optimize before measuring
- sacrifice architecture for convenience

---

# Documentation Mapping

Every implementation should trace back to documentation.

A feature should always be explainable by one or more documents inside `/docs`.

If no supporting documentation exists:

Stop.

The feature should not be invented during implementation.

---

# State Ownership

State is authoritative.

UI reflects state.

Animations communicate state.

State must never become a side effect of the UI.

---

# Component Ownership

Every component has:

- one responsibility
- one owner
- explicit dependencies

No component should own another component's data.

---

# APIs

Communicate only through documented subsystem contracts.

Do not access another subsystem's internals.

Depend on contracts, not implementations.

---

# Architecture

Do not redesign architecture during implementation.

If architecture appears insufficient:

Document the limitation.

Do not silently replace it.

---

# Design

Follow the Design System.

Do not invent:

- spacing
- colors
- typography
- motion
- interaction patterns
- component behavior

Consistency is more valuable than novelty.

---

# AI Behavior

As an AI agent:

Do not guess.

Do not hallucinate product decisions.

Do not create features because they seem useful.

When uncertain:

Read more documentation.

If uncertainty remains:

Leave clear implementation notes rather than inventing behavior.

---

# Code Quality

Generated code should be:

- deterministic
- readable
- modular
- testable
- maintainable
- documented
- recoverable

Optimize for long-term maintenance rather than short-term completion.

---

# Definition of Done

A task is complete only when:

- it satisfies the Product Requirements
- it follows the Manifesto
- it matches User Flows
- it respects the State Machine
- it follows the System Architecture
- it respects the Component Tree
- it follows API Requirements
- it satisfies the MVP Checklist
- it follows the Design System
- it follows the Engineering Principles

Passing compilation is not sufficient.

---

# Decision Framework

When multiple valid implementations exist, prefer the one that:

1. Preserves architecture.
2. Preserves documentation.
3. Reduces complexity.
4. Improves maintainability.
5. Improves readability.
6. Improves testability.
7. Improves determinism.
8. Minimizes long-term technical debt.

---

# Commit Philosophy

Each commit should:

- solve one problem
- preserve architectural integrity
- remain understandable in isolation
- avoid unrelated changes

---

# Final Rule

Aura is a specification-driven project.

The documentation defines the product.

The implementation realizes the documentation.

Never reverse that relationship.