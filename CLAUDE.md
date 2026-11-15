# Taxi Driver — core-loop prototype (Game Prototyper test)

## Context
Test assignment: a cozy top-down taxi game for mobile, portrait orientation.
Requirements: docs/TASK.pdf (primary source). Plan: docs/WORKFLOW.md. Both are local-only files, not committed.
Scoring: Feel 35%, Intro + objectives 25%, Core loop 20%, Stability 10%, AI workflow 10%.
Expected effort is about 8 hours of work: the scope is small, quality matters more than quantity.

## Stack
Unity 6, URP, Android, portrait. C#. New Input System (touch).
Integrations: Unity MCP (scene, components, console), Blender MCP (simple assets).

## Roles
- I (the author) own the design, feel decisions, objectives and balance. I test on the phone.
- You (Claude) are the developer and reviewer.

## Language
- Everything in the repo (code, comments, docs, commit messages, UI text) is in English.
- Talk to me in Russian.

## Working rules
- One task at a time. Do not touch systems outside the current task.
- Before a large feature: a short plan and questions first, then code.
- All gameplay parameters (movement, camera, economy, timings) live in ScriptableObjects, never hardcoded.
- Mechanics with states are explicit state machines that handle invalid transitions.
- No stuck states: if the player can get stuck, propose a way out.
- Mobile performance: no allocations in Update, no Find in Update, few materials and draw calls, lightweight effects.
- UI is minimal, large, and thumb-reachable in portrait.
- After changes made through MCP, check the Unity console for errors.
- Never invent test results, timings or fps. If you did not run it, say so.
- Not sure about a Unity API or setting? Say so and suggest checking the documentation.
- After a feature: what was done, how to verify it, and a proposed commit message.

## Commits
- Commit after each finished task: small commits, conventional prefixes (feat/fix/tune/art/docs/chore).
- Before committing, check that the project compiles and show me what changed.
- One feature per commit, short message.

## Folders
Assets/Scripts, Assets/Prefabs, Assets/Art (Models, Props, Materials), Assets/Config, Assets/Audio, Assets/Scenes.

## Docs to keep updated
IMPLEMENTATION.md (after every feature), docs/OBJECTIVES.md, docs/AI_WORKFLOW.md, README.md.
