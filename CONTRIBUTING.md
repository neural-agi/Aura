# Contributing to Aura

First of all, thank you for considering contributing to Aura.

Aura is not a typical open-source application.

It is a specification-driven, architecture-first project where implementation follows documented product decisions rather than inventing new behavior during development.

Please read this document before opening an issue or submitting a pull request.

---

# Before You Contribute

Before writing any code, become familiar with the project.

At minimum, read:

- Product Manifesto
- Interaction Model
- State Machine
- System Architecture
- Component Tree
- API Requirements
- Engineering Principles

These documents explain why Aura behaves the way it does.

---

# Project Philosophy

Aura prioritizes:

- Simplicity
- Predictability
- Determinism
- Recoverability
- Accessibility
- Native Windows integration
- Long-term maintainability

Every contribution should strengthen these principles.

---

# Branch Naming

Use descriptive branch names.

Examples:

```
feature/clipboard-history

feature/plugin-runtime

feature/search-index

fix/state-transition

fix/memory-leak

refactor/context-engine

docs/design-system

test/event-bus
```

Avoid names like:

```
new-feature

update

testing

misc

temp

final_v2_real
```

Humanity has suffered enough.

---

# Commit Messages

Follow Conventional Commits.

Examples:

```
feat(runtime): add event dispatcher

feat(search): implement artifact indexing

fix(state): resolve invalid transition

refactor(surface): simplify placement logic

docs(animation): clarify motion principles

test(context): add confidence calculation tests
```

A commit should solve one problem.

Avoid unrelated changes in the same commit.

---

# Pull Requests

Every pull request should include:

- What problem is being solved?
- Why is this solution correct?
- Which documentation supports this implementation?
- Any architectural implications?
- Screenshots or recordings (if UI changes are involved)
- Testing performed

Small pull requests are preferred.

---

# Documentation First

Aura is specification-driven.

If your contribution changes product behavior:

1. Update the relevant documentation.
2. Obtain agreement on the documentation.
3. Then update the implementation.

Documentation precedes implementation.

---

# Coding Principles

Contributions should follow the Engineering Principles.

Prefer:

- readable code
- explicit ownership
- composition
- modularity
- deterministic behavior
- simple solutions
- testable components

Avoid:

- hidden coupling
- duplicated state
- circular dependencies
- unnecessary abstractions
- speculative optimization
- framework-driven architecture

---

# Testing Expectations

Every feature should include appropriate tests.

Examples include:

- Unit tests
- Integration tests
- State transition tests
- Recovery tests
- Accessibility validation
- Performance validation (where applicable)

New features should not reduce overall project reliability.

---

# Documentation Updates

If implementation changes:

- architecture
- APIs
- interactions
- state transitions
- design language
- user flows

then the corresponding documentation should also be updated.

Code and documentation should never intentionally diverge.

---

# Design Contributions

UI changes should follow the Design System.

Do not introduce:

- inconsistent spacing
- arbitrary colors
- new component patterns
- unnecessary animation
- inconsistent interaction behavior

Consistency is more important than visual novelty.

---

# Review Criteria

Every contribution will be reviewed for:

- correctness
- architectural consistency
- maintainability
- documentation alignment
- readability
- performance
- accessibility
- testing
- long-term impact

Passing CI does not automatically mean a contribution is ready to merge.

---

# AI-Assisted Contributions

AI tools are welcome.

However:

- Review all generated code.
- Do not merge AI-generated code without understanding it.
- Verify architectural consistency.
- Ensure generated implementations follow the documentation.
- Never allow AI to invent undocumented product behavior.

The documentation remains the source of truth.

---

# Reporting Bugs

When reporting a bug, include:

- Windows version
- Aura version
- Reproduction steps
- Expected behavior
- Actual behavior
- Screenshots or recordings (if applicable)
- Relevant logs (if available)

The more reproducible the report, the faster it can be investigated.

---

# Suggesting Features

Before proposing a feature, consider:

- Does it reduce context switching?
- Does it preserve user attention?
- Does it align with the Product Manifesto?
- Does it fit the existing architecture?
- Can it be explained through Aura's philosophy?

Feature requests that contradict the core principles are unlikely to be accepted.

---

# Code of Conduct

Please follow the project's Code of Conduct.

Be respectful.

Be constructive.

Critique ideas, not people.

---

# Final Principle

Aura is designed deliberately.

Every subsystem, interaction, animation, and component exists for a reason.

When contributing, preserve that intentionality.

If you're unsure whether something belongs, consult the documentation before writing code.