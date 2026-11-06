# Aura Adaptive Surface Model

**Status:** Locked product direction
**Scope:** Product behavior, interaction model, visual direction, and architectural constraints
**Applies to:** Aura Windows desktop application
**Implementation status:** Direction approved; implementation and acceptance remain pending

---

## 1. Purpose

Aura's surface is the primary point of interaction between the user and the Aura runtime.

It must not behave like a permanently expanded floating dashboard or another imitation of the macOS Dynamic Island.

The intended experience is a compact, character-like desktop companion that occupies minimal space while idle and expands into Aura's functional interface when the user intentionally interacts with it.

The surface should feel:

- Present without being intrusive.
- Responsive without being distracting.
- Expressive without being ornamental.
- Useful without requiring constant attention.
- Consistent across different features and application states.

**Core principle:**

Aura occupies as little desktop space as possible until the user asks it to become more.

The adaptive surface is a product-level interaction model, not merely an animation applied to the existing pill.

## 2. Product objectives

The Adaptive Surface must achieve five objectives.

### 2.1 Minimal desktop footprint

The idle presentation should occupy substantially less space than the existing expanded surface.

It should coexist with ordinary desktop workflows without constantly obstructing content or demanding attention.

### 2.2 Recognizable visual identity

Aura should have a recognizable visual identity even when it is not displaying information.

The compact character is intended to give Aura a subtle sense of presence and personality.

### 2.3 Intentional expansion

The user must be able to deliberately reveal Aura's functionality through predictable interactions.

Expansion should feel like the character transforming into the application rather than a separate window appearing.

### 2.4 Functional continuity

Collapsing the surface must not stop the underlying functionality.

Media monitoring, clipboard capture, artifact management, notifications and other runtime services must continue independently of the surface's visibility.

### 2.5 Architectural continuity

The Adaptive Surface must integrate into the existing Aura runtime and presentation architecture.

It must not introduce an alternative runtime, duplicate state ownership, or replace existing functionality with visual-only implementations.

---

## 3. The adaptive interaction model

The primary interaction loop is:

`Idle Character → Intentional Interaction → Expanded Aura → Home/Feature Navigation → Dismissal → Idle Character`

The idle and expanded presentations represent different visual forms of the same Aura application.

They are not independent applications or separate runtime contexts.

### 3.1 Idle Character

The default presentation.

Characteristics:

- Compact circular or near-circular footprint.
- A simple, recognizable character silhouette.
- Small facial details, such as subtle dot eyes.
- Restrained idle animation.
- Subtle cursor awareness.
- Minimal visual noise.
- No permanent display of the expanded feature interface.

The character should feel approachable and alive without behaving like an animated desktop pet.

Its animation must remain restrained. Constant bouncing, excessive eye movement, exaggerated reactions and attention-seeking behavior are explicitly undesirable.

The compact character should remain visually recognizable at its small display size.

The exact character artwork and visual proportions are implementation and design decisions, provided they respect this direction.

### 3.2 Hover anticipation

The character may react subtly when the cursor approaches or enters its interaction region.

Possible responses include:

- A restrained glow.
- A slight change in expression.
- A subtle scale or brightness response.
- A small anticipation animation.

Hover anticipation is not equivalent to expansion.

Moving the cursor across the desktop must not repeatedly open Aura.

Hover-triggered activation must require deliberate pointer dwell and must be cancelled when the pointer leaves before activation.

A direct click must always provide an immediate, predictable activation path.

The interaction threshold and timing should be tuned through real desktop testing rather than treated as arbitrary animation constants.

### 3.3 Expanded Aura

The compact character smoothly transforms into the functional Aura surface.

The expanded presentation may contain:

- Home or overview.
- Media.
- Notifications.
- Clipboard.
- File Shelf.
- Quick Actions.

These sections become available as their corresponding features are implemented.

The interface should not display nonfunctional placeholders merely to imply that future features exist.

The expanded surface must retain a coherent visual hierarchy and provide enough space for its active functionality.

The transition should feel continuous. The character is transformed into the expanded surface rather than dismissed and replaced by an unrelated panel.

### 3.4 Home and navigation

Navigation between Aura's feature surfaces must be predictable.

Home is the primary expanded landing or overview surface.

Returning Home must not be confused with dismissing the entire application surface.

Feature navigation must preserve the application's underlying runtime data and current state.

For example, navigating from Media to Home and reopening Media must display the current media session rather than an obsolete snapshot.

### 3.5 Collapse and dismissal

The expanded presentation should return to the compact idle character when:

- The user explicitly dismisses Aura.
- The user clicks outside the expanded interaction region.
- The user presses Escape when the expanded surface is active and dismissal is appropriate.
- A specifically transient interaction completes and its defined dismissal behavior applies.

Ordinary pointer movement must not collapse Aura while the user is interacting with it.

The surface must not disappear unexpectedly while the user is reading information, using controls, or navigating between features.

Persistent feature views must not automatically collapse merely because their initial interaction has completed.

Dismissal changes the visible presentation. It does not stop background services.

---

## 4. State and presentation model

The Adaptive Surface is a projection of Aura's existing runtime state.

The UI must not invent its own authoritative state to manage the adaptive interaction.

The implementation must map the existing runtime state and typed interaction intents into the following user-visible presentations:

| Presentation | User-visible behavior |
|---|---|
| Idle Character | Compact character, minimal footprint |
| Expanded Home | Expanded Aura overview |
| Expanded Feature | Active feature such as Media or Clipboard |
| Transitioning | Visual transition between presentations |

Transitioning is a presentation concern. It must not become a second source of authoritative application state.

The StateMachine remains the sole authority for runtime state.

SurfaceManager remains responsible for projecting authorized runtime decisions into the visible surface.

If the current runtime contracts cannot express a required interaction, the implementation must identify the smallest necessary contract extension before changing architecture.

Do not introduce a parallel surface state machine.

---

## 5. Interaction and input requirements

### 5.1 Pointer interaction

The compact character must have a clearly defined interactive region.

Only the intended interaction region should receive Aura-related input.

The expanded surface must have correct hit-testing across all interactive controls.

Rendering geometry and input geometry must agree. Invisible or oversized hit regions must not intercept unrelated desktop interactions.

### 5.2 Focus and activation

Passive pointer movement and hover anticipation must not unnecessarily steal keyboard focus from the foreground application.

Explicit user activation may acquire the appropriate interaction or keyboard focus.

When keyboard interaction is available:

- Tab navigation must reach interactive controls.
- Focus must remain visibly identifiable.
- Escape must have predictable dismissal behavior.
- Focus must not become trapped in a collapsed presentation.
- Screen-reader users must be able to identify the character and its primary action.

### 5.3 Dragging

The compact character must be movable.

The user must be able to reposition Aura without accidentally triggering expansion or another interactive action.

Dragging must have a clear initiation and completion behavior.

Position changes must be persisted.

The expanded presentation should support repositioning where practical without interfering with its interactive controls.

### 5.4 Outside interaction

Clicks outside the expanded surface must dismiss it according to the collapse contract.

Clicks inside Aura must not be interpreted as outside clicks.

The implementation must account for event ordering, activation behavior, and changes in the window's interaction region during transitions.

---

## 6. Positioning and desktop coexistence

### 6.1 Position

Aura must support user-controlled positioning.

The position must persist across application restarts.

The initial default placement should follow the existing Aura positioning conventions, with an appropriate desktop-visible location and no unnecessary obstruction.

### 6.2 Multiple monitors

The position model must account for:

- Multiple displays.
- Different display resolutions.
- Monitor removal and reconnection.
- Moving between monitors.
- Different DPI scaling values.
- Available display work areas.

A persisted position must remain meaningful when the display configuration changes.

If the original monitor is unavailable, Aura should recover to a valid position on an available display rather than becoming inaccessible.

### 6.3 DPI scaling

The compact character, expanded surface, hit-testing region and animation geometry must remain consistent under different DPI scaling values.

Window bounds and visual dimensions must not drift or become incorrectly positioned during monitor transitions.

### 6.4 Fullscreen coexistence

Aura must coexist with fullscreen applications without unnecessarily interrupting their use.

The idle character should remain minimally intrusive.

Aura must not automatically expand over a fullscreen application because of a background event or passive media update.

Explicit user interaction may reveal Aura where the Windows presentation environment permits it.

The implementation must not claim reliable overlay behavior in fullscreen scenarios that it has not actually verified.

### 6.5 Window ownership

The existing Aura top-level window and HWND remain the default presentation host.

The preferred implementation is to transform and resize the existing surface rather than introduce an additional top-level window.

An additional HWND is not categorically prohibited, but it requires a concrete platform-level justification and a review of:

- Focus and activation.
- Input routing.
- Z-order.
- Hit-testing.
- Lifecycle management.
- Duplicate-window prevention.
- Accessibility.
- DPI and positioning.

A second window must never be introduced merely to simplify the animation.

---

## 7. Visual and motion direction

### 7.1 Visual identity

Aura should use a cohesive visual language across its compact and expanded presentations.

The compact character and expanded interface must feel like two forms of the same product.

The visual identity should prioritize:

- Clear visual hierarchy.
- Consistent spacing.
- Restrained borders and surfaces.
- Appropriate typography.
- Balanced proportions.
- Readable controls.
- Deliberate use of accent colors.
- Consistent interaction feedback.

The interface must avoid generic debug-panel styling, cramped controls and unnecessary decoration.

### 7.2 Transition quality

The primary transition is:

`Compact Character → Expanded Aura → Compact Character`

The transition should feel smooth, continuous and intentional.

It should not produce:

- Visible window recreation.
- Startup-like flashes.
- Unnecessary position jumps.
- Sudden size changes.
- Clipping.
- Hit-testing mismatches.
- Flickering between compact and expanded states.

Animation must remain subordinate to interaction responsiveness.

A visually impressive transition that makes the interface feel slow is a failure.

### 7.3 Reduced motion

Reduced-motion preferences must be respected.

When reduced motion is enabled:

- Decorative character animation must be minimized or disabled.
- Expansion and collapse must use restrained transitions.
- The interface must retain clear visual state changes.
- Functionality must remain identical.

Reduced motion must not result in inaccessible controls or missing state feedback.

---

## 8. Background functionality and lifecycle

The visible surface is not the runtime itself.

Collapsing Aura must only change its presentation.

The following must continue working independently of visibility:

- Media session detection.
- Media controls and external volume synchronization.
- Clipboard monitoring and capture.
- Clipboard retention and persistence.
- Artifact management.
- Notification ingestion when implemented.
- Background runtime processing.

When Aura is expanded again, its presentation must reflect the latest available runtime data.

### Example

1. Spotify is playing.
2. The user collapses Aura.
3. The user changes the track using physical keyboard media keys.
4. Aura detects the changed media session in the background.
5. The user opens Aura.
6. The Media surface displays the current track and playback state.

The surface must not require a restart or manual refresh to recover current information.

---

## 9. Architecture and ownership constraints

The implementation must preserve the existing Aura ownership model.

| Component | Responsibility |
|---|---|
| StateMachine | Sole authoritative runtime state |
| RuntimeCore | Runtime orchestration |
| Context | Contextual evidence |
| Priority | Event prioritization |
| Policy | Authorization and restrictions |
| Settings | Validated user configuration |
| InteractionEngine | Processes typed user intents |
| SurfaceManager | Visible surface projection |
| AuraSurfaceView | UI rendering and interaction |
| WindowManager | Native window lifecycle and platform behavior |
| Artifact Manager | Artifact ownership, retention and persistence |
| Persistence | Durable application data |

### Non-negotiable constraints

- No second authoritative state machine.
- No duplicate mutable runtime state in the UI.
- No manually constructed parallel runtime graph.
- No hidden service locator.
- No lazy dependency workaround to conceal incorrect composition.
- No new event bus for surface transitions.
- No notification pipeline reuse for unrelated artifact functionality.
- Platform-specific input and window APIs remain behind appropriate existing boundaries.
- UI changes are submitted through the established typed-intent flow.
- Surface rendering remains a projection of authorized runtime state.

Animations may have local presentation progress, but that progress must never determine application authority.

### Existing functionality

The implementation must preserve the current Media and Clipboard implementations.

The Adaptive Surface must not require redesigning the MediaControlService, ClipboardArtifactManager, RuntimeCore or StateMachine merely to achieve its visual behavior.

Any necessary architectural modification must be explicitly justified by a concrete requirement that cannot be satisfied through the existing contracts.

---

## 10. Persistence and settings

The following durable information must survive application restarts:

- User-controlled surface position.
- The monitor association or sufficient placement information to recover position.
- Existing application settings.
- Existing reduced-motion preference, where configured.

The Adaptive Surface must start in its compact idle presentation by default.

The expanded presentation must not be restored automatically merely because Aura happened to be expanded before shutdown.

The character's animation progress, hover state and transition progress are ephemeral presentation details. They must not be persisted as application state.

No new settings interface is required solely for this milestone unless an existing setting needs to be exposed to support the defined behavior.

---

## 11. Privacy and performance

The Adaptive Surface must preserve Aura's local-first product direction.

It must not introduce:

- Unnecessary network requests.
- Remote animation or asset dependencies.
- Telemetry for cursor movement or user interaction.
- Persistent storage of transient pointer information.
- Additional background polling merely to animate the character.

The idle presentation should be resource-efficient.

Animations should avoid unnecessary continuous work when nothing meaningful is changing.

Media and Clipboard event processing must remain independent of rendering frequency.

Rapid visual updates should use appropriate existing latest-value rendering behavior rather than queueing an unbounded number of obsolete UI updates.

Performance must be measured using the published application, not inferred solely from successful builds.

---

## 12. Reliability and recovery

The surface must recover predictably from:

- Failed or interrupted animations.
- Window resizing failures.
- Display configuration changes.
- Monitor disconnection.
- DPI changes.
- Interrupted user interactions.
- Failed surface rendering.
- Runtime events arriving while Aura is collapsed.
- Repeated activation attempts.
- Application shutdown and restart.

A failed transition must not leave Aura permanently invisible or trapped between presentations.

If a visual animation fails, the surface should recover to the correct presentation associated with the authoritative runtime state.

There must be exactly one application instance and no duplicate visible Aura surfaces.

---

## 13. Scope boundaries

### Included in Adaptive Surface v1

- Compact idle character presentation.
- Character idle animation.
- Subtle cursor awareness.
- Intentional hover/click activation.
- Smooth expansion and collapse.
- Expanded Home and existing feature navigation.
- Media access.
- Clipboard access.
- Outside-click dismissal.
- Escape-key behavior.
- Dragging and position persistence.
- Multi-monitor and DPI handling.
- Fullscreen coexistence.
- Accessibility and reduced-motion support.
- Preservation of existing background functionality.

### Explicitly excluded

This milestone does not authorize:

- A new runtime architecture.
- A new StateMachine.
- A second application window without justification.
- A character or animation customization studio.
- An elaborate animated desktop pet.
- A plugin marketplace.
- An AI assistant or conversational interface.
- New weather or news widgets.
- New Media functionality such as playlists or lyrics.
- File Shelf implementation.
- Notifications implementation.
- Quick Actions implementation.
- Cloud synchronization.
- A new theme engine.
- General codebase refactoring unrelated to the Adaptive Surface.

These features may be considered in later milestones.

---

## 14. Product acceptance criteria

Adaptive Surface v1 is accepted only when the following behavior is verified in the published Windows executable.

| ID | Acceptance condition |
|---|---|
| AS-01 | Aura launches into the compact idle-character presentation. |
| AS-02 | The idle presentation occupies substantially less screen space than the existing expanded pill. |
| AS-03 | The character displays restrained idle animation. |
| AS-04 | Cursor approach produces subtle anticipation without immediately expanding Aura. |
| AS-05 | Intentional hover activation works without accidental repeated expansion. |
| AS-06 | Direct click expands Aura predictably. |
| AS-07 | Expansion transforms the existing presentation smoothly. |
| AS-08 | Home and feature navigation work without unintended dismissal. |
| AS-09 | Media remains accessible and functional after expansion. |
| AS-10 | Clipboard history remains accessible and functional after expansion. |
| AS-11 | Outside-click dismissal works correctly. |
| AS-12 | Escape-key behavior is predictable and does not trap focus. |
| AS-13 | Dragging works without accidentally triggering activation. |
| AS-14 | Position persists across application restarts. |
| AS-15 | Position recovers correctly after monitor configuration changes. |
| AS-16 | DPI scaling does not break layout or input geometry. |
| AS-17 | Fullscreen coexistence does not introduce unnecessary interruptions. |
| AS-18 | Media changes continue to be processed while Aura is collapsed. |
| AS-19 | Clipboard capture continues while Aura is collapsed. |
| AS-20 | Reopening Aura displays current runtime information. |
| AS-21 | Reduced-motion behavior is respected. |
| AS-22 | No duplicate HWNDs or duplicate visible surfaces are introduced. |
| AS-23 | Runtime authority is not duplicated in the UI. |
| AS-24 | Failure during a presentation transition does not leave Aura inaccessible. |
| AS-25 | Existing Media, Clipboard, runtime and windowing regression tests continue to pass. |

Automated tests, build success and published-executable verification are separate evidence categories.

A passing test suite alone does not establish visual or interaction acceptance.

---

## 15. Required implementation evidence

The implementation should be validated in the following order:

1. Focused SurfaceManager and interaction tests.
2. Windowing, positioning and lifecycle tests.
3. Media and Clipboard regression tests.
4. Full unit test suite where feasible.
5. Authoritative Debug solution build.
6. Release publication using the existing publishing workflow.
7. Published executable launch verification.
8. Real desktop interaction verification.

Manual verification should explicitly cover:

- Startup into idle.
- Hover anticipation.
- Intentional expansion.
- Outside-click dismissal.
- Escape behavior.
- Home and feature navigation.
- Media operation.
- Clipboard operation.
- Dragging.
- Position persistence.
- Monitor and DPI changes.
- Fullscreen coexistence.
- Background functionality while collapsed.
- Reduced-motion behavior.

Each result must be reported honestly as passed, failed, blocked or unverified.

---

## 16. Definition of completion

Adaptive Surface v1 is complete when Aura has transitioned from a permanently expanded floating interface into a compact, recognizable desktop companion that can be intentionally expanded into its existing functional interface.

It must feel like one coherent application across its idle, expanded and transitional presentations.

It must preserve existing functionality and runtime ownership.

It must remain stable under normal Windows desktop conditions.

The result must be usable in the published executable, not merely demonstrated in a development build.

**Final product principle:**

Aura should be almost invisible when the user does not need it, immediately useful when they do, and consistent enough that interacting with it feels natural rather than like operating another application.

---

## 17. Platform references

These references inform implementation constraints. They do not prescribe a specific API or implementation technique.

- [Microsoft Learn: Title bar customization](https://learn.microsoft.com/en-us/windows/apps/develop/title-bar)
- [Microsoft Learn: Windowing overview for WinUI and Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/develop/ui/windowing-overview)
- [Microsoft Learn: Manage app windows](https://learn.microsoft.com/en-us/windows/apps/develop/ui-input/manage-app-windows)

The product requirements and architectural ownership constraints in this document take precedence over implementation convenience.
