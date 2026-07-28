# Executive Summary

Phase B.5 tightened the specification where it was actually ambiguous for implementation: the Search/Interaction ownership loop was broken, persistence terminology was normalized, lifecycle and recovery behavior were made explicit, accessibility behavior was clarified, and security/policy contract enforcement was pinned to existing owning subsystems.

I did not redesign Aura. I only made the minimum clarifications needed to preserve the existing product model while making the spec set more implementation-ready.

# Findings

## 1. Interaction and Search form a circular dependency

Finding: Interaction and Search depended on each other at the subsystem level.

Validation Result: Confirmed. The dependency loop existed in the architecture and component tree, and it made ownership boundaries harder to preserve.

Resolution: Removed the Search dependency from the Interaction branch and removed Interaction from the Search branch. Query parsing was also detached from Interaction as an owning dependency.

Documents Modified:

- `specs/09_system_architecture.md`
- `specs/10_component_tree.md`
- `specs/11_api_requirements.md`

Reasoning: This was a real specification bug, not an implementation choice. The smallest safe fix was to keep search behavior available through documented contracts while removing the structural cycle.

## 2. The technology stack froze a platform choice that the product spec does not fully constrain

Finding: `TECH_STACK.md` targets Windows 11 while the product docs describe Aura more broadly as a Windows-native product.

Validation Result: Not a blocking contradiction. The stack is a valid implementation constraint, and the product specification does not require broader OS support in conflict with it.

Resolution: No specification change.

Documents Modified: None

Reasoning: This is an implementation scope decision, not a product inconsistency. The frozen stack is compatible with the documented product direction.

## 3. Subsystem lifecycles are not fully specified outside the runtime core

Finding: Many major subsystems had responsibilities but not full lifecycle rules.

Validation Result: Confirmed as a specification gap.

Resolution: Added subsystem lifecycle rules covering creation, readiness, suspension, resume, shutdown, and initialization failure.

Documents Modified:

- `specs/09_system_architecture.md`

Reasoning: A resident runtime needs predictable lifecycle behavior. The added rules preserve the existing architecture and simply make the startup and failure model explicit.

## 4. Search ownership is split across storage, retrieval, and interaction in a way that is still not fully bounded

Finding: Search, History, Artifact, and Interaction responsibilities were not bounded tightly enough for implementation.

Validation Result: Confirmed as a spec gap, but it was addressed by the same ownership cleanup that removed the circular dependency.

Resolution: Clarified Search as the retrieval owner, kept persistence as the storage owner, and removed Interaction from Search ownership paths. Query Parser no longer depends on Interaction.

Documents Modified:

- `specs/10_component_tree.md`
- `specs/11_api_requirements.md`

Reasoning: The product already intended these as separate concerns. The edit makes the separation explicit without changing behavior.

## 5. Security and policy are not given a first-class contract surface

Finding: Security and policy were clearly important but not explicit enough as an enforcement contract.

Validation Result: Supported as a concern, but the existing model already gave the necessary ownership boundaries once reviewed alongside the architecture and API documents.

Resolution: Clarified that policy is enforced through the owning subsystem contracts rather than through side channels.

Documents Modified:

- `specs/09_system_architecture.md`

Reasoning: This keeps security within the existing architecture and makes the enforcement model explicit without inventing a new subsystem.

## 6. Recovery behavior is broad but not yet operationally complete for all failure classes

Finding: Partial initialization, persistence corruption, plugin failure, and restart recovery were described too generally.

Validation Result: Confirmed as a gap for implementation readiness.

Resolution: Added explicit recovery behavior for partial initialization, corrupted persistence, plugin failure containment, and restart recovery.

Documents Modified:

- `specs/09_system_architecture.md`

Reasoning: The spec already valued recoverability. This change turns that principle into concrete expected behavior.

## 7. Accessibility has strong principles but incomplete behavioral contracts for several surfaces

Finding: Accessibility intent was present, but some surface behavior was still underspecified.

Validation Result: Supported and clarified.

Resolution: Added behavioral rules for focus order, accessible dismissal, and reduced motion semantics.

Documents Modified:

- `specs/13_design_system.md`

Reasoning: This does not redesign the interaction model. It defines the expected accessible behavior of the existing surfaces.

## 8. The architecture assumes plugin isolation, but plugin runtime boundaries are still incomplete at the contract level

Finding: Plugin lifecycle, permissions, and isolation existed, but the contract edges were not sharp enough for implementation.

Validation Result: Mostly already covered by the existing plugin architecture and API sections; the remaining concern was about boundary clarity, not missing subsystem scope.

Resolution: No additional subsystem was added. The existing plugin, diagnostics, persistence, and security contracts remain the enforcement model.

Documents Modified: None

Reasoning: The spec already has the correct direction. The remaining detail belongs in implementation of those documented contracts rather than in a new architectural layer.

## 9. Persistence / Persistence Layer / Persistence System terminology is inconsistent

Finding: The same architectural area used multiple names.

Validation Result: Confirmed.

Resolution: Standardized on `Persistence System`.

Documents Modified:

- `specs/01_product_requirements.md`
- `specs/09_system_architecture.md`
- `specs/10_component_tree.md`
- `specs/11_api_requirements.md`
- `specs/12_mvp_checklist.md`
- `specs/14_backlog.md`

Reasoning: This is a naming problem, but in a large codebase naming drift becomes architectural drift. The fix was mechanical and intentionally small.

## 10. Some documents use different names for the same surface/container concept

Finding: Surface vocabulary varies across docs.

Validation Result: Acceptable ambiguity for now.

Resolution: No specification change.

Documents Modified: None

Reasoning: The variations are stylistic rather than contradictory. They do not block implementation, and they are not the same kind of risk as a true ownership or lifecycle gap.

## 11. The research docs are directionally useful but not part of the canonical contract

Finding: Research material supports the product thesis but is not normative.

Validation Result: Correct as-is.

Resolution: No specification change.

Documents Modified: None

Reasoning: The repository already treats specs as canonical. Research stays contextual.

# Remaining Open Questions

These are implementation decisions that intentionally remain unresolved at the specification level:

- Exact internal mechanics of plugin sandboxing and isolation enforcement.
- Detailed UI behavior for specific accessibility edge cases beyond the documented baseline.
- Concrete recovery strategy selection when multiple valid restore points exist.
- Implementation-level storage layout inside the `Persistence System`.
- Specific search ranking heuristics and weighting formulas.

