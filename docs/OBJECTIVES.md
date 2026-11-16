# Intro and objectives

Defined in `Assets/Config/GameplayConfig.asset` (header Objectives) and run by `ObjectiveTracker`.
Progress is shown in the HUD top line, for example "Long haul · 1/3".
Target from the brief: intro with first input within 15 s and 60 s max; each objective 30–90 s; 10–15 min in total.

| # | Title | Done when | What it teaches |
|---|---|---|---|
| 0 | Intro | the player gets into the taxi ("Drag to walk to your taxi, stand next to it") | walking, entering the car |
| 1 | First fare | 1 ride | pick up, route hint, drop off, payment (tutorial mission 1, Stop_Parking) |
| 2 | Heavy bags | 1 ride with luggage | suitcase to the trunk, suitcase to the passenger (tutorial mission 2, Stop_Depot) |
| 3 | Smooth ride | 2 rides without bumps | careful driving, ride rating |
| 4 | New district | open Zone 2, then 2 rides touching Zone 2 | spending at a barrier |
| 5 | Tune-up | buy Engine Lv1, then 1 ride | garage upgrades |
| 6 | Long haul | 3 rides touching Zone 2 | longer routes |
| 7 | Luggage pro | 1 luggage ride without bumps | luggage + clean driving (a luggage order is guaranteed while it is active) |
| 8 | Rush hour | 3 rides within 90 s (the window restarts when it runs out) | speed, choosing close orders |
| 9 | Downtown | open Zone 3, then 2 rides touching Zone 3 | bigger purchase |
| 10 | Top driver | 3 five-star rides in a row | mastery |

A ride "in Zone N" starts or ends in Zone N. A bump is a hit at 3 m/s or more into an obstacle while a passenger is on board (`bumpMinImpactSpeed`).
The rating is 5 stars minus 1 per bump (`starsLostPerBump`).

## Measured lengths

Not measured yet: Play Mode and phone tests have not been run for this version.
After a playtest, copy the objective log (Development build: DBG button > Copy log; file `objective_log.csv` in persistentDataPath) and fill the table with the real durations.
