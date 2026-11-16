# AI workflow

The task said "use AI tools freely", so I treated AI as the team that writes code and generates assets. My job was to design, decide, test and catch mistakes.

## Tools

| Tool | What it was used for |
|---|---|
| **Claude Code** | All C#, editor tools, scene setup, commits |
| **Unity MCP** (Unity AI Assistant) | Building and wiring the scene, reading the console after every change |
| **Blender MCP** (official Blender Lab add-on) | Taxi driver model with rig and Idle/Walk, passengers, suitcase |
| **Claude (chat)** | Planning before the start, reviewing my prompts, economy checks |
| **ChatGPT** | First draft of the economy spreadsheet, which I then reworked |
| **Python** (written by Claude) | Synthesized placeholder SFX: coins, engine |

## How it was set up

- **Plan first.** Before touching Unity I wrote a plan with Claude chat: requirements, scoring weights, who does what, rules for the AI. That plan stays local; the repo files below come from it.
- **`CLAUDE.md`** — rules Claude Code reads at the start of every session:
  - everything in the repo is in English;
  - small commits with `feat / fix / tune / art / docs` prefixes;
  - all gameplay values live in `GameplayConfig` (a ScriptableObject), nothing hard-coded;
  - explicit state machines for anything with states;
  - never invent test results, timings or fps — "not run" is an acceptable answer.
- **Review agents** in `.claude/agents/`:
  - `design-review` — intro, objectives, economy;
  - `qa-stability` — stuck states and mobile performance;
  - `hiring-review` — the repo checked against the task's scoring.
- **Skill** `playtest-log` — moves real playtest timings into `docs/OBJECTIVES.md` without filling gaps.
- **Keeping cost down.** `/clear` between tasks, a file map in `IMPLEMENTATION.md` so new sessions don't search the whole project, editor scripts instead of hundreds of single MCP calls, and a cheaper model for routine changes.

## Who did what

**Me**
- **Level design.** Three iterations of the city in Krita: block layout → loops and roundabouts instead of dead ends, parking bays for pickups → four zones with prices. The sketches are in `docs/level/`. I chose the scale and the start area, and rebuilt the road layout myself in Blender after the first AI build failed.
- **Game design.**
  - Scope: no procedural map (measured objectives need a fixed one), no traffic.
  - Zones as progression, with locked districts visible behind barriers.
  - Luggage rides, so the on-foot part has a purpose.
  - Several orders at once with price-tag arrows, so choosing a fare is a decision.
  - Per-zone fare multipliers and longer cross-zone trips as the city opens up.
  - Engine upgrade raises top speed (12 → 15 → 18 m/s) so it is felt; Drift Boost cut for scope.
  - Tips for clean driving.
  - A short tutorial, then the objective chain.
- **Economy.**
  - Reworked the ChatGPT draft, which padded objectives with empty 45-second steps.
  - Checked that every zone is affordable with zero tips, so the player can't get stuck.
  - Rebalanced fare per metre after the map turned out more compact than planned.
- **Feel and playtests.**
  - Found on the phone that the driving camera showed too little road.
  - Measured my own full run (12.0 min).
  - Ran a playtest with a first-time player and turned the findings into fixes: curbs catching the car, steering, unclear luggage steps.
- **Review.** Checked every AI result in Play Mode. AI writes a lot of code fast, but "done" from the AI only means "it compiles".

**Claude Code**
- All gameplay code:
  - car controller and enter/exit state machine;
  - trip planner, passengers, luggage;
  - payouts, tips and star rating;
  - zones and barriers, garage upgrades;
  - objectives and the timing logger;
  - HUD and arrows.
- Editor tools: the city builder that reads my layout data, automatic curb generation along roads, trigger zones and markers, scene build menus.
- Models through Blender MCP, built to my specs (low-poly, ~800 tris, one palette material, same rig for all characters).
- Sounds through a Python synthesis script.

## Where AI went wrong and how I caught it

| Problem | How I found it | What I did |
|---|---|---|
| First city build: roads far too wide, buildings tiny | Looked at the scene | Rebuilt the road layout myself in Blender; Claude then generated curbs from it |
| Features reported as done without ever running | Played every feature in Play Mode | Found the luggage steps in the wrong order and barriers missing at start (saved debug state) — sent back as bug reports |
| A big task prompt silently didn't land; Claude later built everything from scratch | Its report said "nothing of this existed" | Re-checked the remaining features one by one |
| Economy draft padded objectives with empty steps | Read the table critically | Merged purchases into rides; moved the felt upgrade into the session |
| Objective logger overwrote the file and lost a playtest | Went to fetch the log after the test | One file per run, rows written as each objective completes |
| Trip distance ranges didn't fit the real map | Claude measured the map and asked | Took the measured ranges and raised fare per metre |
| Tutorial hand icon looked like a rude gesture | Saw it in game | Replaced it with a tap-pulse indicator |

## What I'd do differently

- Design the zones as rings around the start from day one. Route length would have scaled with progress naturally, instead of being tuned afterwards.
- Lock the map scale in Unity before drawing details. My sketch and the built city ended up at different scales.
- Run the timing logger on a fresh player earlier. One real playtest changed more than hours of planning.
