# Taxi Driver — core-loop prototype

A cosy top-down taxi game for mobile (portrait). Walk to your cab, pick up passengers, drive them across town, get paid, and spend fares on new districts and upgrades.

Test assignment for **Game Prototyper (AI)** at Estoty. Built in Unity 6 (URP) with Claude Code, Unity MCP and Blender MCP. How AI was used is described in [`docs/AI_WORKFLOW.md`](docs/AI_WORKFLOW.md).

---

## How to run

- **Unity:** `[UNITY VERSION from ProjectSettings/ProjectVersion.txt]` with Android Build Support.
- **Scene:** open `[Assets/Scenes/SCENE_NAME.unity]` and press Play.
- **Android build:** File → Build Profiles → Android → enable **Development Build** (this turns on the `DBG` panel with the objective log) → Build And Run. Portrait orientation.
- Progress resets on every launch (`resetProgressOnLaunch` in `GameplayConfig`), so every run starts from the intro.

## Controls

One thumb, no on-screen buttons.

- **Move / drive:** floating stick — touch anywhere, drag in the direction you want to go. The car turns toward the stick; pull back to reverse.
- **Get in:** stand next to the taxi until the ring fills.
- **Get out:** stop, then hold your finger on the taxi until the ring fills.
- **Pick up / drop off:** stop in the parking bay next to the passenger or drop-off marker.
- **Buy:** stop at a zone barrier, or stand on a garage pad, until the ring fills.

## What's in the prototype

**R1 — Enter and leave the car**
- Enter by standing close, with a progress ring.
- Exit only when the car is stopped (same ring). Re-entering right after exiting is blocked until you step away.

**R2 — Earn money from rides**
- Up to 3 orders at once. Each passenger shows fare and distance; off-screen passengers get an edge arrow with a price tag.
- Pick up by stopping in the bay; a route arrow and a world marker lead to the drop-off.
- Payout: cash count-up, coin burst into the counter, coin sound.
- **Tips by star rating:** no hits = ★★★★★ +25%, one hit = ★★★★ +10%.
- **Luggage rides (30%):** load the suitcase into the trunk at pickup, carry it to the passenger at drop-off (+$5).
- **Spending:**
  - 4 districts behind barriers: Zone 2 $150, Zone 3 $300, Zone 4 $600. Locked districts stay visible as a goal.
  - 3 garage upgrades: **Engine** (top speed 12 → 15 → 18 m/s), **Steering** (+15% turn rate per level), **Fare Meter** (+10% fare per level).
- Each district has a fare multiplier, and unlocking a district makes trips longer and cross-zone.

**R3 — Feel**
<!-- Delete any line below that is not in the final build. -->
- Arcade handling: the car turns toward the stick, speed follows stick deflection, automatic reverse.
- The car cannot flip; curbs along every road let the car slide along instead of stopping dead.
- Driving camera sits higher than the on-foot camera, pulls out with speed and looks ahead.
- Engine sound with pitch by speed, tyre marks, camera shake on impact, body roll and pitch, sparks on curb hits.
- Brake and reverse lights.

**R4 — Intro + 10 objectives**
- The intro gets the player into the taxi; the first input is immediate.
- 10 objectives run in sequence, each teaching one part of the loop: first fare → luggage → smooth driving and tips → unlocking a district → upgrades → longer routes → rush hour → downtown → top driver.
- The tutorial rides use step-by-step tap hints.
- An in-game logger writes the duration of every objective to a CSV file, one file per run.
- **Measurements and design notes: [`docs/OBJECTIVES.md`](docs/OBJECTIVES.md).** Designer run: 12.0 min total.

## Project structure

- `Assets/Scripts` — gameplay code. `IMPLEMENTATION.md` has a file map.
- `Assets/Config/GameplayConfig.asset` — all tunable values: handling, camera, economy, objectives, timings.
- `Assets/Art` — models made through Blender MCP; `.blend` sources are in `ArtSource/`.
- **Taxi menu** (editor): Build Main Scene, Build City (rebuilds the city from layout data), Setup Trip System, Reset Save.
- `docs/` — objectives table, playtest logs, AI workflow, level sketches.
- `CLAUDE.md` and `.claude/` — rules, review agents and a playtest skill for Claude Code.

## Known limitations

- Greybox art. The map came out more compact than the sketch, so trip distances were rebalanced for it.
- Objectives #1, #6, #8 and #10 were out of the 30–90 s range in the designer run. They need a re-measure with a fresh player before re-tuning (see `docs/OBJECTIVES.md`).
- Built and tested on Android only, on one phone and in the editor (no Mac available for an iOS build).
- Placeholder sounds are synthesized.

## Next steps

- Measure the objectives on 3+ fresh players and re-tune the counts.
- Drift Boost upgrade (hold a drift → speed burst) to deepen "fun to master".
- More passenger types and requests; an art pass on the city.
