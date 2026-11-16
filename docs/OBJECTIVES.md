# Intro + 10 Objectives

**Targets (from the task):** intro ≤ 60 s with first input ≤ 15 s · each objective 30–90 s · total 10–15 min.

All times below are **real measurements** from the in-game objective logger (`objective_log_<date>_<time>.csv`, one file per run). Nothing is estimated. Empty cells = not measured yet.

**Testers**
- **P1** — designer (me). Knows the game and the map, so P1 is a lower bound for time. Run: 2026-11-16 16:27, log `docs/playtests/objective_log_2026-11-16_16-27.csv`.
- **P2** — first-time player (games-industry background). Timings lost: the old logger overwrote the file on the next run. Bug fixed in `68f8446` (one file per run, rows written as each objective completes). Qualitative feedback recorded below.
- **P3** — not run yet.

## Table

| # | Objective | Teaches | Completion condition | Target, s | P1, s | P2 | P3 | Notes |
|---|---|---|---|---|---|---|---|---|
| Intro | Intro | Walking, hold-to-enter | Walk to the taxi and get in | ≤ 60 (first input ≤ 15) | 2.9 | — | | First input is immediate |
| 1 | First fare | Pickup, route arrow, drop-off, payout | 1 ride | 30–90 | 20.6 | — | | Under 30 s for the designer. Re-check with a new player |
| 2 | Heavy bags | Exiting the car, walking, luggage | 1 ride with luggage | 30–90 | 38.2 | — | | |
| 3 | Smooth ride | Tips (no bumps = bigger tip) | 2 rides with no hits | 30–90 | 68.2 (2 attempts) | — | | |
| 4 | New district | Saving up, buying a zone at a barrier | Unlock Zone 2 + 2 rides there | 30–90 | 79.4 | — | | |
| 5 | Tune-up | Spending, upgrades | Buy Engine Lv1 at the garage + 1 ride | 30–90 | 53.8 | — | | |
| 6 | Long haul | Longer cross-zone routes | 3 rides in Zone 2 | 30–90 | 106.1 | — | | Over 90 s. Re-check with a new player |
| 7 | Luggage pro | Luggage under pressure | 1 luggage ride with no hits | 30–90 | 36.1 | — | | |
| 8 | Rush hour | Pace | 3 rides within 90 s | 30–90 | 126.3 (2 attempts) | — | | Failed once. Re-check with a new player |
| 9 | Downtown | Progression | Unlock Zone 3 + 2 rides there | 30–90 | 55.8 | — | | |
| 10 | Top driver | Mastery | 3 perfect (★★★★★) rides in a row | 30–90 | 136.3 (2 attempts) | — | | Failed once. Re-check with a new player |
| **Total** | | | | 600–900 (10–15 min) | **720.8 (12.0 min)** | — | | In range |

## Definitions

- Objectives are defined in `Assets/Config/GameplayConfig.asset` (Objectives header) and run by `ObjectiveTracker`. Progress is shown in the HUD top line, e.g. "Long haul · 1/3".
- Tutorial missions 1 and 2 start at the depot (`Stop_Parking`, `Stop_Depot`). While Luggage pro is active, a luggage order is always available.
- A ride "in Zone N" starts or ends in Zone N.
- A bump is a hit at ≥ 3 m/s into an obstacle while a passenger is on board (`bumpMinImpactSpeed`). Rating = 5★ minus 1★ per bump (`starsLostPerBump`). Tips: 5★ +25%, 4★ +10%.
- Rush hour: if the 90 s window runs out, it restarts.
- How to measure: Development Build → `DBG` → Copy log. One file per run: `objective_log_<date>_<time>.csv` in `persistentDataPath`.

## Observations

**P1 (designer)**
- Total session is 12.0 min, inside the 10–15 min target. Intro meets both limits.
- Out of range: #1 (20.6 s, under), #6, #8 and #10 (over 90 s). #8 and #10 include one failed attempt each.
- Decision: objective counts are kept for now. The overruns on #8 and #10 came from my own mistakes in that run, and the next step is a measured run with a fresh player before changing the design.
- Fallback if a fresh player is also out of range: #1 → 2 rides · #6 → 2 rides · #8 → 2 rides in 90 s · #10 → 2 perfect rides in a row. Each is one value in `GameplayConfig → Objectives`.

**P2 (first-time player, qualitative)**
- Crashed into curbs often → curb geometry fixed (no protruding or overlapping segments), steering made more responsive, less sliding.
- Luggage steps were unclear → added a step-by-step pointing-hand tutorial for the first luggage ride (exit car → bag → trunk, then trunk → passenger).
- First passenger now appears only after the player gets in the taxi, so the intro has one clear goal.
