# PROJECT BRIEF: Time Fracture

**Working title:** Time Fracture\
**Genre:** 3D psychological survival horror\
**Target platform:** PC\
**Target experience:** A single 10--15 minute vertical slice, not a full
game

## Purpose

This brief gives an AI coding agent (Antigravity) the context needed to
help build the game in Unity. Work on **one milestone at a time**; never
attempt to build the whole game in one prompt.

## Team and constraints

-   Student team with beginner-to-intermediate Unity skills.
-   No budget; avoid paid assets and packages.
-   Engine: Unity, using a current supported LTS version and C#.
-   Render pipeline: Universal Render Pipeline (URP), for lighting and
    fog.
-   Antigravity writes code only. A human performs Unity Editor work
    such as creating scenes and prefabs, assigning Inspector references,
    and configuring Animators.
-   Every milestone prompt must request:
    1.  Complete C# scripts.
    2.  Exact manual setup steps in the Unity Editor.
    3.  Clear steps to test the result.

## Game concept

A first-person psychological survival horror game set in an abandoned
research facility where a secret experiment broke time. The player is an
outsider investigating a mysterious signal and a disappearance. The
facility violently glitches between two states:

-   **PAST:** An active laboratory.
-   **PRESENT:** An abandoned, decayed facility.

A horrifying **FUTURE** state is planned, but it is **not part of the
first build**. Researchers and test subjects who suffered during the
experiment are now ghosts, each with a distinct relationship to time and
hunting behaviour.

## Design pillars

1.  **Constant dread:** Quiet exploration, with uncertainty about when a
    hunt will begin.
2.  **Ghost sabotage:** Lights flicker, doors change state, objects
    move, and strange sounds warn of nearby ghosts. These effects are
    driven by one ghost-activity meter.
3.  **Stealth and survival:** During a hunt, the player hides in lockers
    or under desks, stays quiet, and avoids light.
4.  **Limited protection:** Two consumable items, each effective against
    one specific ghost.
5.  **Dangerous timeline changes:** Interacting with certain
    objects---such as doors, machines, or recordings---can trigger an
    era swap and raise ghost aggression.
6.  **Story through audio:** Five or six distorted audio logs reveal the
    experiment and its cover-up.

## Core mechanic: era swapping

For the first build, implement only two eras: Past and Present.

-   Each room has two versions of the same layout, represented by two
    parent GameObjects.
-   Swapping eras activates one version and deactivates the other.
-   Changes made in one era can affect the other. For example, a door
    unlocked or barrier removed in the Past may remain open in the
    Present; a hiding spot may exist in only one era.
-   Each swap plays a glitch effect (screen distortion or
    post-processing) and a sound, and increases ghost activity.
-   Implement a central `EraManager` with an event such as
    `OnEraChanged` so other systems can subscribe without tightly
    coupling to the manager.

## Ghosts in the first build

Implement only two ghosts. Each uses a simple state machine with these
states: `Idle`, `Roam`, `Investigate`, `Hunt`, and `Cooldown`. Use Unity
NavMesh for movement.

### The Echo

-   Exists only in the Past.
-   Hunts by **sound**.
-   Sprinting, opening doors, and noise events can attract it.
-   Countered by a noise-decoy item.
-   Hiding works against it.

### The Residue

-   Exists only in the Present.
-   Hunts by **sight and light**.
-   Slow but relentless; the flashlight makes it more likely to notice
    the player.
-   Countered by a flare/light item.
-   Hiding works against it.

A third ghost associated with the Future era is planned for later and
must not be implemented in the first build.

## Systems list

-   First-person controller: walk, sprint, crouch, flashlight, and
    interact key.
-   Noise system: actions emit noise events with a radius ghosts can
    hear.
-   `EraManager`: era swapping and glitch effect.
-   Hiding system: enter and exit lockers or desks, with limited vision
    while hidden.
-   Ghost AI: state machines, NavMesh movement, hearing and sight
    detection.
-   Ghost-activity meter: drives light flickers, door slams, moving
    objects, and ambient sounds.
-   Inventory: two item types with limited quantities.
-   Audio logs: pickup, playback UI, subtitles, and distortion effect.
-   Simple UI: interaction prompts, subtitles, menu, and fail/restart
    screen.
-   Ending: a simple outcome influenced by which logs were found and key
    choices.

## Architecture rules

-   Keep scripts small and single-purpose.
-   Communicate through C# events or ScriptableObject event channels
    rather than unnecessary hard references.
-   Expose tunable values in the Inspector using `[SerializeField]`.
-   Comment code clearly so students can understand and maintain it.
-   Avoid external paid packages. Use built-in Unity features and free
    packages such as TextMeshPro, and Cinemachine or NavMesh components
    only when useful.
-   Prefer the legacy Input Manager for simplicity unless the new Input
    System is selected. If the new Input System is used, provide all
    setup steps.
-   Do not change unrelated scripts or systems while implementing a
    milestone.
-   Explain assumptions and any necessary trade-offs.

## Milestones

Build in this order, with one Antigravity prompt per milestone:

1.  **Player controller:** Movement, camera look, sprint, crouch,
    flashlight, and interact.
2.  **Blockout room:** One grey-box room plus basic interactables (door
    and switch).
3.  **Era toggle:** `EraManager`, two room versions, and glitch effect.
4.  **Hiding system:** Locker.
5.  **Ghost #1 --- The Echo:** NavMesh patrol, hearing, chase, and fail
    state.
6.  **Environmental sabotage:** Effects driven by the ghost-activity
    meter.
7.  **Inventory:** Two item types.
8.  **Audio logs:** Pickup, playback, and subtitles.
9.  **Ghost #2 --- The Residue:** Sight/light detection and a simple
    ending.
10. **Polish:** Lighting, fog, sound design, and menus.

## Optional story seed

The experiment trapped the researchers' final moments in time loops.
Each ghost is a person replaying their worst moment, which is why they
appear only in certain eras. The player's actions in the Past can
accidentally cause the events that created the ghosts, making the
outsider part of the disaster. Endings depend on which logs are found
and which choices are made.

## Art and sound style

-   Low-poly visuals.
-   Dark environments, heavy fog, and minimal lighting.
-   Strong sound design.
-   Simple visuals are intentional: they help conceal asset
    inconsistencies and strengthen the feeling of dread.

## Rules for Antigravity prompts

For every milestone, write **one prompt only** and make it ready to
paste into Antigravity. The prompt must specify:

-   Unity version and URP render pipeline assumptions.
-   The exact goal and scope of the milestone.
-   The scripts to create or modify, including filenames.
-   Required public fields, serialized Inspector fields, and events.
-   Exact manual Unity Editor setup steps.
-   Test criteria and expected results.
-   Any assumptions or limitations.

Always ask Antigravity to: - Return complete C# scripts, not snippets
with missing sections. - Explain the code in beginner-friendly terms. -
Avoid modifying unrelated scripts or features. - Include a suggested Git
commit message after each milestone.

## Current working rule

Implement only the milestone explicitly requested by the user. Do not
jump ahead or build the entire game at once.
