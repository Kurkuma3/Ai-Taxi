# Implementation notes

How each mechanic of the Taxi Driver prototype is built, why it was built that way, and how to check it.
Add a section (or extend one) with every feature. Keep it factual: what the code does, not what it should do.

Conventions used throughout:
- All tunable numbers live in `Assets/Config/GameplayConfig.asset` (`GameplayConfig` ScriptableObject). Behaviours never hardcode them.
- Unity 6 (6000.6.1f1), URP, new Input System, Android portrait. Reference UI resolution 1080×1920.
- No `Find`/allocations in `Update`; references are wired by `Assets/Editor/SceneBootstrap.cs` (menu **Taxi > Build Main Scene**), which rebuilds the whole greybox scene from primitives.
- Design rule from the brief: as little UI as possible. Every action is a touch gesture in the world; the HUD has no buttons.

---

## 1. Input — one floating stick plus "hold" fingers

File: `Assets/Scripts/Input/InputReader.cs` (singleton `InputReader.I`).

**Floating stick.** The first free finger that touches the screen becomes the stick. Its touch-down point is the origin; the current position minus the origin, divided by the stick radius (`stickRadiusScreenFraction` × screen height), is `StickVector` with magnitude 0..1 (clamped). Releasing the finger deactivates the stick. Any further fingers are ignored for steering.

**Hold fingers.** A consumer can install a predicate `IsHoldTarget(screenPos)`. It is evaluated **once, when a finger lands**. If it returns true, that finger is a "hold" for as long as it stays down: it is reported through `HoldActive` / `HoldPos` and is never used as stick input, even if it later slides away. The decision is made at touch-down (like a UI button) so a thumb that wobbles does not suddenly start driving the car. Pointer ids seen last frame are kept in a small list to detect "new" fingers without allocations.

**Editor fallbacks.** Mouse left button acts as one finger (id −1). WASD / arrows synthesize a full-deflection stick anchored at the lower screen centre. E sets `ExitPressedThisFrame`.

Verify: in the editor, drag with the mouse — a ring (base) and a disc (knob) appear under the cursor; the knob stays inside the ring.

## 2. Car — steer toward the stick, automatic reverse

File: `Assets/Scripts/Vehicle/CarController.cs`. Rigidbody with X/Z rotation frozen (it can never flip), continuous collision, interpolation on.

**Forward driving.** Screen-up is world +Z (the camera has zero yaw). The stick direction is converted to a desired yaw; the signed angle between the car heading and that yaw, divided by `steerFullAngle`, is the steering value in −1..1. Stick deflection past `stickDeadzone` is the throttle; the target speed is `maxSpeed × throttle`, reduced toward `turningSpeedFactor` at full steering lock. Inside the deadzone or with no finger the target speed is 0 (brake). Turn rate is scaled by speed: full at `fullTurnSpeed`, down to `minTurnFactor` when almost stopped, so you can still steer away from a wall.

**Automatic reverse (no button).** Pulling the stick into the sector behind the car (≥ `reverseEnterAngle`, default 120°, off the heading) switches `IsReversing` on:
- If the car is still rolling forward faster than `reverseEnterSpeed`, a backward stick is a straight brake (steer 0, target 0). Predictable, no swerve.
- Once slow enough, the car backs up: its **rear** is aimed at the stick (the steering angle is measured from heading + 180°), target speed is `−reverseMaxSpeed × throttle`.
- Swinging the stick back to the front hemisphere (< `reverseExitAngle`, default 80°) returns to forward. The 80°–120° gap is hysteresis so the mode never flickers at the boundary. Releasing the stick keeps the mode only while the car still rolls backward.

`Speed` is signed (negative while reversing). `IsStopped` uses its absolute value. Direction changes always use `brakeDeceleration`; only "same direction and faster" uses `acceleration`, so a flip from reverse to forward never jerks.

**Collisions.** On impact with static geometry the speed is multiplied by `collisionSpeedKeep` (0.3) so the car does not grind into walls. Getting out of a wall is the reverse rule above: pull the stick back.

Verify: drive nose-first into a wall, pull the stick back → the knob turns amber and the car backs out. Push the stick forward again → knob white, forward driving. PlayMode test `ReverseWhenStickPointsBack`.

## 3. Player on foot

File: `Assets/Scripts/Player/PlayerController.cs`. CharacterController. While the stick is held the character walks forward at `walkSpeed`; the stick direction (outside the deadzone) rotates them at `playerTurnRateDegPerSec`. Inside the deadzone they keep heading and keep walking. Gravity is a simple vertical velocity. `SetVisible` hides the mesh and disables the controller while driving; `Teleport` moves the character safely by toggling the controller off.

## 4. Enter / exit state machine

File: `Assets/Scripts/Player/PlayerStateMachine.cs`. States: `OnFoot → Entering → Driving → Exiting → OnFoot`. Both transitions take `enterExitTransition` seconds. One `holdProgress` value (0..1) drives one ring in the HUD for both gestures.

**Enter.** On foot, standing within `enterRadius` of a car that `IsStopped` fills the ring at `1 / enterHoldTime` per second; leaving the radius drains it twice as fast. At 1.0 → `Entering`: player hidden, camera retargets the car, then `Driving`.

**Exit (hold on the car).** While `Driving` and `IsStopped`, the state machine's `IsOverStoppedCar` is installed as the input `IsHoldTarget`: a finger landing within `exitHoldRadiusScreenFraction` × screen height of the car's screen position becomes a hold finger. While `HoldActive && car.IsStopped` the same ring fills over the car; lifting the finger or moving the car (another finger on the stick) drains it. At 1.0 → `Exiting`: the player is placed at `exitLocalOffset` beside the car, camera retargets the player. In the editor, E exits immediately (same guard: only when stopped).

**No instant re-enter.** After exiting, `canEnter` is false until the player has once been farther than `enterRadius` from the car, so the still-pressed finger (or standing next to the car) cannot bounce the player straight back in.

Invalid transitions: exit requests while moving are ignored; enter needs a stopped car; the ring is reset on every state change.

Verify: PlayMode test `EnterDriveStopExit` (walk in → ring → drive → turn → E refused while moving → stop → hold finger on the car → on foot → no re-enter within 1.5 s).

## 5. HUD

File: `Assets/Scripts/UI/Hud.cs`. Screen-space overlay canvas, `ScaleWithScreenSize` 1080×1920.
- Hold ring: two radial-filled procedural ring sprites positioned at the car's screen point + 1.6 m up; hidden when progress ≈ 0.
- Stick: ring base + disc knob, sized from the stick radius; knob turns amber while the car reverses.
- No buttons. The `InputSystemUIInputModule`/EventSystem stay in the scene for future screens (upgrades shop).
Sprites are generated at runtime by `UiSprites.Ring`, so the prototype needs no art files.

## 6. Camera

File: `Assets/Scripts/CameraRig/TopDownCamera.cs`, config header **Camera**. Zero yaw always (screen-up = world +Z, which the car and player steering rely on). `Target` is swapped by the state machine; the camera also holds `config` and `car` references (wired by SceneBootstrap).
- **On foot:** close, `cameraFootOffset` (0, 18, −8), as before.
- **In the car:** higher, `cameraCarOffset` (0, 30, −13.3): the same pitch, just farther, so on a phone you see where you are driving.
- **Speed zoom:** `zoom` follows |speed| / `maxSpeed` with an exponential rate, faster out (`cameraZoomOutRate` 1.2/s) than back in (`cameraZoomInRate` 0.8/s). The car offset grows along its own direction until it is `cameraSpeedZoomHeight` (+8 m) higher at max speed.
- **Look-ahead:** the follow point moves `cameraLookAhead` (9 m at max speed) along the car's forward × signed speed, so reversing looks behind. It is smoothed by `cameraLookAheadRate` (2/s) so turns swing gently.
- **Transition:** a 0..1 blend between the foot and car offsets eases with `cameraModeBlendRate` (2.5/s) on getting in and out. The rotation is recomputed from the blended offset every frame, so a custom car offset with a different pitch also blends smoothly. Position follow: `cameraFollowSmoothing` (6/s).

Verify: get in and drive at full speed. The view rises by about 8 m and shifts forward. Brake: it settles back down. Get out: it eases down to the close foot view. Play Mode was not run for this change.

## 7. Scene bootstrap and tests

- `Assets/Editor/SceneBootstrap.cs` builds `Assets/Scenes/Main.unity`: ground, blocks/curbs, car (cube body + physics material), player (capsule), camera, light, HUD, `Systems` object holding `InputReader` and `PlayerStateMachine`. Private serialized fields are set via reflection (`Set(obj, "field", value)`). Re-running it is the authoritative way to regenerate the scene; hand edits to the `.unity` file must be mirrored here.
- `Assets/Tests/PlayMode/CoreLoopTests.cs` (`InputTestFixture`): simulated keyboard and touchscreen drive the real scene. Run from **Window > General > Test Runner** or headless:
  `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml`

## 8. Taxi driver character (model, rig, Idle/Walk)

Source: `ArtSource/TaxiDriver.blend` (Blender 5.3, built through Blender MCP by script). Export: `Assets/Art/Models/TaxiDriver.fbx`. Preview: `docs/taxidriver_preview.png`.

**Mesh.** One object, 796 triangles, blocky boxes with a one-segment bevel for a chunky look, flat shaded. 1.80 m to the top of the head (1.88 m with the cap), origin at the feet, faces −Y in Blender → +Z in Unity. One material `TaxiDriver` with an 8×1 palette texture (`Assets/Art/Textures/TaxiDriverPalette.png`): every part's UVs sit in the centre of one colour cell (yellow cap/vest, white sleeves, dark trousers, black shoes/belt/visor/eyes, skin, brown hair). Palette import: point filter, no mipmaps, uncompressed, so cells never bleed. In Unity the Blender material is remapped to `Assets/Art/Materials/TaxiDriver.mat` (URP Lit, smoothness 0.05).

**Rig.** Armature `TaxiDriverRig`, 11 bones: Hips → Spine → Head; UpperArm/LowerArm L+R (parented to Spine); UpperLeg/LowerLeg L+R (parented to Hips). Rigid binding: every vertex is in exactly one group with weight 1, so parts never stretch. Generic avatar in Unity, no root motion.

**Clips** (24 fps, keyed about armature-space axes, Cycles F-modifiers for seamless loop handles, one NLA strip per clip, both import with Loop Time):
- `Idle`, 2 s: spine 0→2° breathing lean, head counter-tilt, hips −8 mm, arms drift 3° outward.
- `Walk`, 1 s: thighs ±32°, knees bend 5°→55° in the swing phase, arms ±28° in counter-phase with 10–30° elbow bend, hips bob ±2 cm and yaw ±5°, shoulders counter-rotate, 4° forward lean. The exported range is 1–24 so the loop frame is not duplicated.

**Unity.** `Assets/Editor/TaxiDriverAssets.cs` (menu **Taxi > Setup Taxi Driver**, also called by SceneBootstrap) configures the importers, the material and builds `Assets/Art/Animations/TaxiDriver.controller`: float `Speed`, states Idle ⇄ Walk, transitions at `Speed` 0.1 with no exit time and 0.15 s blend; the Walk state uses `Speed` as its playback multiplier. `Assets/Scripts/Player/PlayerAnimator.cs` sets `Speed = planarSpeed / walkSpeed × walkAnimSpeedMultiplier` with `walkAnimDampTime` smoothing (`PlayerController.Speed` is the CharacterController's planar velocity, 0 while hidden). SceneBootstrap instantiates the FBX prefab under `Player/Visual` in place of the capsule; `SetVisible` keeps working because it toggles `Visual`.

Verify: Play, walk with the stick — the driver steps in time with the speed, stops into the breathing idle; enter the car — the model hides; exit — it reappears beside the car. Feet sliding → raise `walkAnimSpeedMultiplier`.

## 9. Greybox city from the sketch (4 zones, barriers, passenger stops)

Source of truth: `docs/city_sketch.png` (hand drawing; scale bar 40 m = 200 px, so 1 m = 5 px; map ≈ 280 × 195 m). It was transcribed into metres by a script (road axes, building footprints, red pocket marks, zone frames) and written as `Assets/Editor/CitySketchData.cs`, which fills the ScriptableObject `Assets/Config/CityLayout.asset` (`CityLayout`: roads as polylines, turnaround rings, convex building footprints with height and zone, pockets, barriers, zone rectangles, park, parking lot, spawn points). Coordinates: X east, Z north, origin at the bottom-left of the sketch.

**Builder.** `Assets/Editor/CityBuilder.cs`, menu **Taxi > Build City** (also called by **Taxi > Build Main Scene**). Destroys the previous `City` root and rebuilds everything from the layout using prefabs in `Assets/Prefabs/City`:
- `RoadQuad` — one flat quad per polyline segment, 10 m wide, extended by half a width at both ends so L-corners and T-junctions fill themselves; each segment sits on its own 0.4 mm Y step so overlaps never z-fight. `Turnaround` (flat cylinder) covers bends and the two dead-end rings.
- `Building` — MeshFilter/MeshRenderer/MeshCollider; the footprint is extruded at build time into a flat-shaded prism saved under `Assets/Art/Models/City/`. Material by zone (edge grey, Z1 sand, Z2 tan, Z3 rose, Z4 lilac).
- `PassengerStop` — 8 × 3 m pocket quad beside the road plus an empty `Marker` with the `PassengerStop` component (zone id). 29 stops, one per red mark.
- `ZoneBarrier` — BoxCollider across the whole road (blocks car and player), four cones, a bar and a placeholder price sign (`TextMesh`); the `ZoneBarrier` component stores `closesZone` and `price`. No unlock logic yet.
- `InvisibleWall` — BoxColliders on the four edges of `bounds`.
- Ground is one plane with the only flat collider; roads, park and lot are visual decals on top of it. The old 120 × 120 arena is gone.

**Zones** (rectangles in the layout): Z1 start (47..130, 3..83), Z2 $150 (130..224, 0..84), Z3 $300 (158..278, 83..174), Z4 $600 (40..158, 83..197); the park rectangle is Z4 regardless. A road on a border belongs to the cheaper zone, so the vertical x = 125 road and the z = 78 road are Z1/Z2, the z = 117 road east of x = 158 is Z3.

**Barriers** (9): three on the Z1→Z2 border (south road, z = 45 road and z = 78 road at x = 133), two on Z1→Z4 (west road and park-west road at z = 83), one on Z2→Z3 (east road at z = 83), three on Z3→Z4 (z = 117 and z = 140 roads at x = 153, park path at its east gate). Note for the unlock logic: the Z3/Z4 barriers must stay until **both** zones are open, otherwise buying Z4 from the west gives Z3 for free.

**Spawn.** Player (62, 29) and car (69, 27) on the parking lot with the star; the car faces the diagonal exit road.

Verify: Taxi > Build City, console clean; in Play Mode drive the south road east — the car stops at the cones at x ≈ 131; the checks that were run are in the change log entry.

## 10. Passengers and suitcase (models, Wave clip, prefabs)

Source: `ArtSource/Passengers.blend` (Blender 5.3, built through Blender MCP by script; the taxi driver's rig and Idle/Walk actions were appended from `TaxiDriver.blend`). Export: `Assets/Art/Models/Passenger_A.fbx`, `Passenger_B.fbx`, `Suitcase.fbx`. Same style as the driver: bevelled blocks, flat shading, one material `Passenger` with a 16x1 palette `Assets/Art/Textures/PassengerPalette.png` (UVs in cell centres), remapped in Unity to `Assets/Art/Materials/Passenger.mat` (URP Lit, smoothness 0.05). Passengers and the suitcase share this one material.

**Passenger_A, office worker** (752 tris): navy suit with a long jacket hem, white shirt V, red tie, dark side-parted hair, brown briefcase rigidly bound to `LowerArm.R`. Read from above: dark head and shoulders plus a brown block at the right hand.
**Passenger_B, tourist** (784 tris): turquoise shirt with coral print, belly, khaki wide shorts, bare shins, white sneakers, sunglasses, wide cream panama (12-sided brim, 0.68 m across) and an orange backpack with straps. Read from above: a big cream disc plus an orange block.
Neither has the driver's yellow cap or vest.

**Rig.** Exactly the driver's 11 bones (same rest pose, rigid weights). Both FBX files export the armature under the same name `PassengerRig`, so the clip binding paths are identical and one Animator controller drives both models.

**Clips** (24 fps, looping): `Idle` (2 s) and `Walk` (1 s) are the driver's actions; `Wave` (1 s, keys every 3 frames, Cycles modifiers, range 1–24) raises the **left** arm above the head (upper arm ~155–165° in the frontal plane, forearm sways ±24° so the hand sweeps 0.25–0.65 m sideways at 1.92–1.99 m height), the torso leans 3–4.5° away, the head tilts up 4°. Left arm because Passenger_A holds the briefcase in the right hand.

**Suitcase** (104 tris): 0.40 x 0.25 x 0.60 m (shell 0.03–0.54 m, handle on top), magenta shell with a dark seam band, two foot rails and a top handle. Pivot at the bottom centre. Child empty `HandlePoint` at the grip centre, (0, 0.583, 0) in Unity, for attaching it to a hand when carried. No collider yet (gameplay will decide).

**Unity.** `Assets/Editor/PassengerAssets.cs` (menu **Taxi > Setup Passengers**) mirrors `TaxiDriverAssets`: palette importer (point, no mips, uncompressed), material, FBX importers (Generic, file scale, imported normals, clips named Idle/Walk/Wave with Loop Time; the suitcase has no rig/animation), controller `Assets/Art/Animations/Passenger.controller` (float `Speed`, bool `Wave`; Idle ⇄ Walk at `Speed` 0.1, Idle ⇄ Wave on `Wave`, Wave → Walk when `Speed` rises, 0.2 s blends, Walk speed multiplier = `Speed`), and prefabs `Assets/Prefabs/Characters/Passenger_A.prefab`, `Passenger_B.prefab` (Animator with the controller, no root motion, `CullUpdateTransforms`, skin quality 1 bone) and `Assets/Prefabs/Props/Suitcase.prefab`. Nothing is placed in the scene and no gameplay code uses them yet.

Verify: run **Taxi > Setup Passengers**, console clean; drag a passenger prefab into a scene, open the Animator window in Play Mode and toggle `Wave`. In Blender open `ArtSource/Passengers.blend` and play the `Wave` NLA track. What was checked: tri counts in Unity (752 / 784 / 104), all three clips import on both FBX files, the Wave clip from Passenger_A moves the left arm of a Passenger_B instance (shared controller binds). Play Mode was not run.

## 11. Trips: passengers, fares by zone, cross-zone trips, payment, route hint

Files: `Assets/Scripts/Trips/` (`TripPlanner`, `TripManager`, `ZoneProgress`, `Wallet`, `Passenger`, `RouteHint`), additions to `Hud` and `PassengerStop`, editor `Assets/Editor/TripSystemSetup.cs`. All numbers are in `GameplayConfig` (headers Fares, Trip length, Passenger, Payment, Route hint).

**Zones.** `ZoneProgress` keeps which zones are open (Zone 1 from the start) and `HighestOpen`. `Open(z)` makes z the *new zone* for the next `newZonePriorityTrips` (4) planned trips. Open zones are saved (PlayerPrefs bitmask `Taxi.OpenZones`) and restored on launch without the new-zone priority, unless `resetProgressOnLaunch` (default on, section 13) clears them first. Menu **Taxi > Reset Save (zones, upgrades, objectives)** clears it, together with the upgrade levels and the objective progress. Money and the tutorial counter are not saved. In the editor only (`#if UNITY_EDITOR`, absent from builds), keys **1–4** open a zone for the current session only; they are **not saved**, so they never hide barriers on the next Play. When a saved mask other than Zone 1 is restored, the console logs it once at start.

**Buying zones** (`BarrierShop`, config header Zones for sale).
- Each `ZoneBarrier` sits between two zones: `closesZone` (higher), `otherZone` (lower) and `forwardZone` (the one on its +Z side). **Taxi > Setup Trip System** assigns them from the CityLayout zone rectangles 6 m ahead of and behind the barrier, with the park counting as Zone 4. This also fixed the hand-placed copy `Bar_West_Z4 (1)` at (181, 83), which is a Zone 2 / park (Zone 4) barrier, not Zone 1 / Zone 4.
- A barrier sells whichever of its zones is still closed and disappears only when both are open. So buying Zone 4 from the west never opens Zone 3 for free.
- Buy area: a rectangle in front of the barrier, |x| ≤ `barrierBuyHalfWidth` (4.5 m) across the road and |z| ≤ `barrierBuyRadius` (6 m) along it, on the open side. The car must be stopped there with the driver inside.
- Enough money: the action ring fills over `barrierBuyTime` (1 s). Then `Wallet.TrySpend` runs, coins fly from the counter to the barrier while the counter counts down (`Hud.PlaySpend`), and `ZoneProgress.Open` saves the zone. Every barrier whose two zones are now open loses its colliders at once and pops out (grows 20%, shrinks to 0 over `barrierVanishTime`) with a procedural pop/chime (`UiSprites.PopClip`). Remaining barriers update their price to the zone they now sell.
- Not enough money: an empty ring shows and the price text blinks red (`barrierBlinkRate`).
- Prices come from `zonePrices` ($0 / 150 / 300 / 600) and are written to the signs at start.
- Passenger stops win: the shop is silent while the car is within `stopRadius` of the current trip's stop (`TripManager.IsAtActiveStop`). Stop_06, 17, 18 and 29 lie next to barriers (checked with a 1 m grid over each stop radius against each buy area), and without this rule a pickup there could buy a zone.

**Stop zones.** `PassengerStop.ResolvedZone` takes the zone from the `zone` field. If the field is 0, it uses the nearest parent named `Zone N` or `ZN`. Stops without a zone are ignored with a warning. Menu **Taxi > Validate Passenger Stops** counts the stops per zone (current scene: Z1 = 5, Z2 = 5, Z3 = 7, Z4 = 11 active, none missing). `Stop_25` and `Stop_27` are behind a road barrier with no way in: they are disabled (inactive GameObject, `CityLayout.Pocket.disabled = true`, CityBuilder builds them inactive). Inactive stops get no passengers and the curb is closed in front of them, because the curbs were rebuilt and inactive pockets are not drivable shapes. `Stop_28` was deleted from the scene, the layout asset and `CitySketchData`. Two Zone 1 stops were added on the start lot, which the sketch does not have: `Stop_Depot` (63, 38.5) in front of the taxi garage and `Stop_Parking` (63, 26) at the south end of the parking lot. Both are added to `CitySketchData`, `CityLayout.asset` and the scene. They lie fully inside the lot, so the curbs are unchanged, and they overlap no collider.

**Fare.** `round_half_up((baseFare + farePerMeter × metres) × max(mult[from], mult[to]))`, with $10 + $0.15/m and multipliers ×1.0 / 1.2 / 1.4 / 1.7. Metres are the straight line between the two stop markers. The label above the passenger shows this final number.

**Trip selection** (`TripPlanner.TryPlan`, pure logic):
1. Only ordered pairs whose two stops are in open zones are candidates. A closed zone is never a pickup or a destination.
2. While a new zone has priority, with probability `newZoneTripChance` (0.5) the pair must start or end in it.
3. A random pair inside the distance range of the highest open zone is chosen.
4. If no pair is inside the range, a random one of the `fallbackPairChoices` (3) pairs closest to the range is chosen. The brief said "the longest one", but that repeats a single route forever, so this was changed with the author's approval.
5. The last drop-off stop is avoided as the next pickup when another option exists.
6. **No repeated routes:** the last `recentRoutesToAvoid` (2) routes, A–B in either direction, are excluded while any other pair exists. This rule beats the distance range: a shorter new route is preferred over a repeat.
7. Each later pass relaxes one rule, so the planner returns a trip whenever two usable stops exist. Constraint strength: no-repeat > new-zone priority > avoid pickup > distance range.

A `Trip` is an immutable snapshot, so a zone opening mid-ride does not change the current trip.

**Distance ranges.** The brief's 80–150 / 150–250 / 200–300 / 250–350 m do not fit this map: the longest stop pair is 68 m inside Zone 1 and 209 m over the whole city, so every trip fell back to one pair. With the author's approval the defaults were fitted to the map: **40–70 / 60–115 / 80–190 / 100–210 m**. Planner run on the real 31 stops with the no-repeat rule (2000 trips per stage, editor script, not Play Mode):

| Open up to | In range | Distance | Fare | Routes used | Repeat of the last 2 routes |
|---|---|---|---|---|---|
| Z1 | 100% | 42–68 m | $16–20 | 6 (14–18% each) | 0 |
| Z2 | 100% | 61–115 m | $20–33 | 33 | 0 |
| Z3 | 100% | 80–186 m | $26–53 | 90 | 0 |
| Z4 | 100% | 100–209 m | $31–70 | 218 | 0 |

With the new-zone priority on (measured before the depot/parking stops), 91% of trips touched a newly opened Z3 and 87% a newly opened Z4.

**Tutorial missions.** The first `tutorialTrips` (2) trips of a session pick up at the stop whose `PassengerStop.tutorialMission` equals the mission number. Mission 1 starts at `Stop_Parking`, mission 2 at `Stop_Depot`, both on the start lot. The drop-off is planned by the normal rules with the pickup forced (`TripPlanner.TryPlan(..., forcedPickup)`). Planner check on the real stops: mission 1 goes Parking → Stop_20 ($16), mission 2 goes Depot → Stop_26 ($19; the depot's other Z1 pairs are under 40 m). The mission counter is per session (not saved).

**Start money.** `Wallet` starts with `startMoney` ($50); `TripManager` shows it on the counter at start (`Hud.SetMoney`, no animation).

**Orders: several passengers wait at once** (`TripManager`, config header Orders). After the tutorial `offerCount` (3) orders wait at the same time, as in the brief's mockup ("3 waiting nearby"). The tutorial trips are single: order slots 2 and 3 start filling only when the last tutorial passenger is picked up, so 3 orders are waiting after the second drop-off. Each slot has its own colour (`offerColors`: green, blue, pink; amber is kept for the drop-off).
- Each order is planned by the same `TripPlanner` rules. Two orders never wait at the same stop, and no order waits at the drop-off of the ride in progress (`blockedPickups`, hard). Drop-offs avoid stops where someone waits (`blockedDropoffs`, soft: it is dropped when that is the only way to stay inside the distance range). Editor simulation, 3 concurrent orders, 2000 orders per stage: 0 planning failures, 100% inside the distance range in all four stages; Zone 1 drop-offs land on a waiting stop in 6% of orders, other stages 0%.
- Pick any order: stop the car within `stopRadius` of its stop with the driver inside → `Boarding` for that order. The others keep waiting while you choose (waiting passengers never leave on their own).
- **Fresh orders every ride:** when the passenger is in the car, the other orders disappear. No orders exist during the ride. After the drop-off (payment), a new set is dealt: slot i appears after `nextTripDelay + i × offerSpawnStagger` (1 s, +0.25 s per slot). New pickups avoid the stop where the last passenger still stands.
- **Zone opened:** every waiting order is re-planned with the new zone open (and its new-zone priority). Same passenger, same stop, suitcase kept; new destination and fare. The price pill punches (`offerPulseTime`) so the change is noticed. If no new trip can be planned, the order keeps its old one.
- Passengers are pooled (`offerCount + 2` instances per prefab, a random free one per order: waiting ones + the rider + the one walking away). The dropped-off passenger stays for `leavingPassengerTime` (3 s), then disappears. Suitcases are pooled too.

**Order markers** (`Hud.ShowOrder`, requested per frame, laid out in `LateUpdate`; `Hud` runs after the camera with `[DefaultExecutionOrder(100)]` so world-anchored UI does not lag a frame).
- Every waiting order has a price pill in its colour ("$25", dark text). A luggage order has a suitcase icon inside the pill (procedural `UiSprites.Suitcase`). The pills are clones of the old `FareLabel`, which now stays hidden as the template.
- Passenger's head on screen → the pill hangs over the head, no arrow. Off screen → the pill is pinned to the screen edge with a large arrow (`offerArrowSize` 110 ref px, procedural `UiSprites.Arrow`, same colour) pointing from the car toward the passenger. The edge band keeps `offerEdgeMargin` (80 ref px) from the sides, stays below `offerEdgeTop` (13% of the height: money counter) and above `offerEdgeBottom` (30%: the thumb/stick area), so markers never sit in the middle of the screen or under the stick.
- On the ground, each waiting stop has a thin pulsing ring in the order's colour (`RouteHint.ShowOrderRing`, MaterialPropertyBlock, no extra material).
- Markers and rings show only while searching. From boarding to the end of the drop-off they are hidden and only the drop-off hint (amber arrow + ring) is visible.
- Luggage: picking up an order's suitcase commits the player to that order. The other orders' markers hide and only that passenger can board.
- Known: when two off-screen orders lie in nearly the same direction, their edge markers can overlap.

**Ride state machine** (`TripManager`):
- `Searching`: orders wait (above). The passenger faces the car and waves while the car is within `waveDistance` and the player is driving. Car stopped within `stopRadius` of an order, driver inside → `Boarding`.
- `Boarding`: the passenger walks to the nearest door, at most `passengerWalkMaxTime`, then → `Riding`. If the car drives away → `Searching`; the order keeps waiting and the passenger walks back to the stop.
- `Riding`: the route hint points to the drop-off. When the car is stopped within `stopRadius` (driver inside or not) → `Alighting`.
- `Alighting`: payment happens at once (wallet + coins), then the passenger walks to the stop, time-limited → `Searching` (or `AwaitLuggage`).

No state waits for something the player cannot do.

**Luggage** (`TripManager`, config header Luggage). Tutorial mission 2 always has a suitcase and mission 1 never does. After the tutorial each order has one with `luggageTripChance` (0.3), which adds `luggageBonus` ($5) to the fare (the overhead label includes it). Suitcase state: `None → AtStop → Carried → InTrunk → Carried → Delivered`.
- Pickup: the suitcase stands at the passenger's right. Stop at the pocket and get out (normal hold on the car). Stand by the suitcase until the action ring fills (`luggageInteractRadius` 1.6 m, `luggageHoldTime` 0.6 s), and it is carried at the right hand (`carryLocalOffset`, parented to the player root). As soon as the suitcase is picked up, the passenger walks to the nearest car door and waits there, but only if the car is within `stopRadius + passengerFollowExtra` (3 m) of the stop; farther away they keep waiting at the stop, since walks are straight and physics-free. Fill the ring at the trunk (`trunkOffset` 2.7 m behind the car centre; the route hint points there in the order's colour). **Only when the suitcase is in the trunk does the passenger get in**, with or without the driver inside (the ride starts, the other orders disappear). If the car was too far from the stop, they board normally when it stops there with the driver inside.
- Drop-off: the passenger gets out of the door on the stop's side and walks `passengerStandOffset` (1.6 m) away from the car, then waits there (state `AwaitLuggage`, not paid yet). Fill the ring at the trunk, carry the suitcase **to the passenger**: the drop point is `luggageDropDistance` (1.2 m) beside them along the car's direction, never at the car. Fill the ring there → it is put down and the payment plays from it. (Before this fix the drop point was 1.2 m from the stop marker toward the car; the car parks on the marker, so the target ended up at the car.) Passengers without luggage also step away to the same point instead of walking into the car.
- While the suitcase is carried, `PlayerStateMachine.EnterBlocked` is set and the enter ring does not fill. Driving off at the pickup leaves the suitcase at the stop. Driving off at the drop-off keeps it in the trunk; the passenger keeps waiting and the hint leads back.
- The route hint follows the current target (suitcase, trunk, drop point; the stop while far away in the car).
- The action ring is a second HUD ring, `Hud.RequestActionRing`: an empty ring appears within 3× the radius, fills inside it and drains outside. It is requested per frame and hidden in `LateUpdate` when nobody asked, so the barrier shop can share it.
- Side change: the enter ring now also drains while the player is next to a moving car or blocked (before, it froze).

**Payment.** `Wallet.Add` raises `Earned`. `Hud.PlayPayment` flies `coinsPerPayment` pooled coin images from the passenger to the top counter along small arcs (staggered). Each landing plays a procedural "ding" (two sines, pitch rising per coin) and punches the counter scale. The counter counts up over `moneyCountUpTime`, starting when the first coin lands. The money string is rebuilt only when the shown value changes.

**Route hint.** `RouteHint` builds an arrow, a ring and a beam mesh in Awake. They use one URP Unlit material (`Assets/Art/Materials/RouteHint.mat`), tinted through a MaterialPropertyBlock: the order's colour on the way to a pickup or suitcase, amber for drop-off. The arrow circles the car (or the player on foot) at `routeArrowRadius` and points straight at the target. It hides closer than `routeArrowHideDistance`. The ring pulses on the target stop.

**Scene.** `TripSystemSetup` (menu **Taxi > Setup Trip System**) adds the components, the RouteHint object and the HUD elements to the open scene. It does not rebuild the scene, so hand edits survive, and it is idempotent. `SceneBootstrap` calls it as well.

**Tests.** `Assets/Tests/EditMode/TripPlannerTests.cs` covers the fare formula, closed zones, in-range picks, both fallback modes, no repeated routes (and repeating only when nothing else exists), the new-zone share, pickup avoidance and stop zone parsing. Written but **not run** yet (Window > General > Test Runner > EditMode).

Verify:
- In Play Mode, get in the car: the first (tutorial) passenger appears with a coloured pill and ring, and waves as you approach. After the second trip starts, two more orders appear; while searching, three pills/edge arrows in three colours are visible, hidden during the ride. Stop next to the passenger: they walk to the door. The arrow turns amber. Stop at the amber ring: coins fly to the counter with a ding.
- Press 2 in the editor: Zone 2 opens, its barriers disappear, and most of the next 4 trips involve Zone 2.

Known: `Stop_Depot` is 31–35 m from Stop_20 and Stop_23 (below the 40 m minimum), so in Zone 1 it only pairs with Stop_26.

## 12. Upgrades at the garage

Files: `Assets/Scripts/Upgrades/` (`Upgrades`, `UpgradePad`, `UpgradeShop`), editor `Assets/Editor/UpgradeSetup.cs` (menu **Taxi > Setup Upgrades**, also called by SceneBootstrap). Config header **Upgrades (garage pads)**.

**Pads.** Three yellow discs in a row on the paved start lot below the garage, between the Stop_Parking and Stop_Depot pockets, right next to the player spawn: Engine (59.0, 31.8), Steering (63.4, 31.8), Fare Meter (67.4, 31.8). Each has a sign on a post behind it (toward the garage, so it never hides the player on the pad), tilted 55° back so it reads from the top-down camera. The sign is one line: "Engine Lv1 · $80 (15 m/s)" (Engine shows the top speed it gives), "Steering Lv1 · $80", or "Engine MAX" at the top level. Character size 0.055. Boards are sized to the measured text: Engine 4.5 m (its longest text, "Engine Lv2 · $160 (18 m/s)", is 4.27 m), the others 3.7 m ("Fare Meter Lv1 · $120" is 3.5 m). There is a 0.3 m gap between boards. The row ends at x 69.3, clear of the car spawn. There are no colliders. Checked: no collider other than the ground within 1.3 m of any pad, and an editor render from the foot camera shows the signs readable. The first spot (in front of the garage's east half) was moved because two pads sat on the exit road.

**Buying** (`UpgradeShop`, on foot only). Standing within `upgradePadRadius` (1.4 m) of a pad:
- enough money: the action ring fills over `upgradeBuyTime` (0.8 s), then the price is paid, coins fly from the counter to the pad (`Hud.PlaySpend`), a pop plays (pitch 1.25), the disc and the sign punch, and the sign shows the next level;
- not enough: an empty ring and the sign blinks red (`upgradeBlinkRate`);
- max level: nothing, the sign says MAX.
After a purchase that pad re-arms only once the player steps off, so standing still never buys two levels.

**Effects** (`Upgrades`, levels saved per upgrade in PlayerPrefs `Taxi.Upgrade.<Name>`, max level = price array length):
- Engine: top speed `engineTopSpeeds` per level: 12 → 15 → 18 m/s (level 0 = Car `maxSpeed`), $80 / $160. Acceleration is multiplied by the same ratio (top / `maxSpeed`), so the time to full speed stays about the same (12 / 10 = 1.2 s at every level). Braking is unchanged. `CarController.MaxSpeed` exposes the current top speed. The camera's speed zoom and look-ahead are measured against it, so full zoom is reached at the real top speed, not at 12.
- Steering: turn rate × (1 + `steeringTurnPerLevel` × level), +15% per level, $80 / $160.
- Fare Meter: fare × (1 + `fareMeterPerLevel` × level), +10% per level, $120 / $240. It applies in `TripPlanner.Fare` (`TripRules.fareBonus`) before rounding and before the luggage bonus. Buying it re-prices the waiting orders at once (the pill punches). The ride in progress keeps its fare.
`CarController` and `TripManager` hold an optional `Upgrades` reference (null = no bonus).

Verify: Taxi > Reset Save, Play, get $80 or more, walk onto the Engine pad: the ring fills, $80 flies out, the sign reads "Engine Lv2 · $160 (18 m/s)", and the car reaches a higher top speed in about the same time (the camera zooms out to the same height at the new top speed). With less money the sign blinks red. Buy Fare Meter while orders wait: their prices go up and punch. Restart: the levels are kept. Play Mode was not run for this feature.

## 13. Intro, objectives, ride rating

Files: `Assets/Scripts/Objectives/` (`ObjectiveDef`, `ObjectiveTracker`), editor `Assets/Editor/ObjectiveSetup.cs` (menu **Taxi > Setup Objectives**, also called by SceneBootstrap). Config header **Objectives**. The design table is in `docs/OBJECTIVES.md`.

**Tracker.** Step 0 is the intro: the top line says "Drag to walk to your taxi, stand next to it" and the step is done when the player gets in. Steps 1–10 are `config.objectives` in order, each with a title, a kind, a count and optional zone, upgrade level and time limit. The top line shows "Title · progress/count". While a precondition is missing it shows "New district · open Zone 2 at a barrier" or "Tune-up · buy Engine Lv1 at the garage". Rush hour adds a countdown ("Rush hour · 1/3 · 0:42"). After the last step: "All objectives done! Free ride". The step index and the summed time are saved in PlayerPrefs (`Taxi.Objective`, `Taxi.ObjectiveTotal`).

**Counting** (on `TripManager.RideCompleted`, raised when the ride is finished and paid):
- AnyRide: every ride. LuggageRide: rides with a suitcase.
- ZoneRide: the ride starts or ends in the zone, and the zone is open.
- EngineThenRide: rides after Engine reaches the level.
- CleanRide and CleanLuggageRide: rides with 0 bumps. A bumped ride is a failed attempt; progress is kept.
- PerfectStreak: 5-star rides in a row. A worse ride resets to 0 and counts as a new attempt.
- TimedRides: the window starts with the objective. When it runs out, progress resets and a new attempt starts with a fresh window.
`Completed(number, title, start, finish, attempts)` and `AllCompleted(total)` are raised for the log.

**Luggage guarantee.** While Heavy bags or Luggage pro is active, `TripManager.ForceLuggageOrder` makes sure every new set of orders has a luggage order. With the normal 30% chance, about a third of the sets would have none.

**Bumps and rating.** `CarController.Bumped(impact)` fires when the speed into an obstacle (|speed| × how head-on) is at least `bumpMinImpactSpeed` (3 m/s). Ground contacts and curb grazes do not count. `TripManager` counts bumps only in `Riding`. Rating = 5 − `starsLostPerBump` × bumps (minimum 1). At payment a toast shows "★★★★★ perfect ride!" (or "★★★★☆ nice ride", "★★★☆☆" …) for `rideToastTime` (2.5 s). The editor font has the star glyphs (`Font.HasCharacter` true on Windows). On Android they come from the system font fallback, not checked on a phone.

**Tips.** At payment the passenger adds a tip by the ride rating: `tipByStars` (element 0 = 1★ … element 4 = 5★) = 0 / 0 / 0 / +10% / +25% of the fare, rounded half up. For example, a $28 fare gets $7 at 5★ and $3 at 4★. The wallet gets fare + tip in one `Wallet.Add`. `Hud.PlayPayment(from, total, fare, tip)` flies the usual `coinsPerPayment` coins, then `tipCoins` (4) more after `tipCoinDelay` (0.2 s); the coin pool is sized for both. The counter counts up to the full total. A label rises from the payment point and fades over `payLabelTime` (1.6 s): "+$28" in gold and "+$7 tip" in green under it (no tip line without a tip). Both payment paths go through `TripManager.Pay`: on getting out for normal rides, on delivering the suitcase for luggage rides. `RideResult.tip` carries the amount. Play Mode was not run for this feature.

**HUD.** `Hud.SetObjective` uses a dark pill under the money counter (top, y −205, 960 × 84, font 46). `Hud.ShowToast` uses a pill in the upper middle (y +420 from the centre). Both are cloned at runtime from the fare-label template. `offerEdgeTop` was raised from 0.13 to 0.17 so the order edge arrows stay below the objective line.

**Objective log** (`ObjectiveLog`). Each run writes its own file in `Application.persistentDataPath`: `objective_log_yyyy-MM-dd_HH-mm.csv` (the run's start time; `_2`, `_3` … if that minute already has one). Old files are never overwritten or deleted, `resetProgressOnLaunch` included. The columns are `number,title,start,finish,duration_s,attempts`. A row is appended with `File.AppendAllText` (opened, written, closed) the moment each step finishes, intro included, so a crash or restart keeps everything finished so far. Start and finish are wall-clock `yyyy-MM-dd HH:mm:ss`, the duration is in seconds with one decimal, and numbers use the invariant culture. The console prints `[OBJ] Log: <path>` at start, `[OBJ] 6 Long haul — 54.2 s` per step, and `[OBJ] Total — 12:34` after the last objective (sum of objectives 1–10, intro excluded), which is also written as a `total` row. A run that starts from the intro gets a new file. A resumed run (only with `resetProgressOnLaunch` off) continues its file, whose name is kept in PlayerPrefs `Taxi.ObjectiveLogFile`. Durations use unscaled time, so time in the background is not counted. Editor folder: `%USERPROFILE%/AppData/LocalLow/AiTaxi/Taxi Driver/`. Android: `/storage/emulated/0/Android/data/<package>/files/` (not checked on a device). An older single `objective_log.csv` from before this change stays in the folder untouched.

**Debug panel** (`DebugPanel`, IMGUI, only when `Debug.isDebugBuild`: editor and Development builds; disabled in release builds). A small "DBG" button sits in the top-left corner. It opens a box with the current run's log path, **Copy log** (puts this run's CSV on the clipboard and reports the line count) and **Open folder** (in the editor, `EditorUtility.RevealInFinder` selects the file in Explorer; desktop players use `Application.OpenURL(file:///folder)`; the button is hidden on phones). Tapping the panel also starts the floating stick for that finger; a tap without a drag does not move anything. For the panel on a phone, build with **Development Build** checked.

**Reset on launch** (`LaunchReset`, `[DefaultExecutionOrder(-1000)]`; config header **Save**, `resetProgressOnLaunch`, default **true**). On every launch it deletes the saved open zones, upgrade levels and objective progress before `ZoneProgress` and `Upgrades` read them in Awake. The barriers are back, upgrades are at level 0, the objectives start at the intro and a new objective log is started. Money is never saved; the wallet starts with `startMoney`. The tutorial counter is per session. Turn the flag off to keep progress between launches (the saves above are then used as before). The ordering relies on the script execution order applying to Awake; Play Mode was not run to confirm it.

Verify: Reset Save, Play. The top line shows the intro text. Get in: "First fare · 0/1". Finish the ride: the rating toast appears and the line switches to "Heavy bags · 0/1". Play Mode was not run for this feature.

## Change log

- 2026-11-16 — Objective log per run: `objective_log_<date>_<time>.csv`, never overwritten or deleted, a row written as each step finishes; DBG panel: Copy log copies the current run, new Open folder button (editor/desktop).
- 2026-11-16 — Tips: 5★ +25%, 4★ +10%, lower none (config `tipByStars`); extra coin burst for the tip (`tipCoins`, `tipCoinDelay`); floating "+$fare / +$tip tip" label at the payment point (`payLabelTime`).
- 2026-11-16 — `resetProgressOnLaunch` (default true): every launch clears zones, upgrades and objective progress (`LaunchReset`, runs first); money was never saved; the game always starts with the intro.
- 2026-11-16 — Objective timing: `objective_log.csv` in persistentDataPath (number, title, start, finish, duration_s, attempts), `[OBJ]` console lines and total, debug panel (Development builds) with the path and Copy log.
- 2026-11-16 — Objectives: intro + 10 objectives from config (`ObjectiveTracker`, top line "Title · n/m"), ride bumps (`CarController.Bumped`, `bumpMinImpactSpeed`) and 1–5 star rating toast, luggage order guaranteed while a luggage objective is active, `docs/OBJECTIVES.md`; `Taxi > Setup Objectives`.
- 2026-11-16 — Engine upgrade now raises top speed 12 → 15 → 18 m/s (config `engineTopSpeeds`, replaces `engineAccelPerLevel`), acceleration scales with it; camera zoom uses the current top speed; Engine sign shows the speed, its board is wider.
- 2026-11-16 — Upgrades: three garage pads (Engine +15% acceleration, Steering +15% turn rate, Fare Meter +10% fares; two levels each), ring purchase on foot, red blink when short, MAX sign, saved, cleared by Reset Save; config header Upgrades; `Taxi > Setup Upgrades`.
- 2026-11-16 — Fix luggage order: at pickup the passenger boards only after the suitcase is in the trunk; at drop-off the passenger waits beside the car (config `passengerStandOffset`) and the suitcase goes to them, not to the car.
- 2026-11-16 — Camera: separate foot and car offsets (car 30 m high), speed zoom (+8 m at max speed), look-ahead along the direction of travel, smooth blend on getting in/out; config header Camera (the old serialized offset/smoothing on the camera are gone).
- 2026-11-16 — Orders refresh: the other orders vanish when a passenger boards, a fresh set pops in after the drop-off (config `offerSpawnStagger`); opening a zone re-plans waiting orders (same stop, new destination and fare, pill punch, config `offerPulseTime`); a luggage passenger gets in as soon as the suitcase is picked up, the trunk is loaded during the ride.
- 2026-11-16 — Orders: 3 passengers wait at once after the tutorial, each with its own colour; edge-of-screen arrows with the price (suitcase icon for luggage), pill over the head when on screen, hidden during a ride; coloured ring on each waiting stop; passengers and suitcases pooled; planner `blockedPickups` (hard) / `blockedDropoffs` (soft); config header Orders.
- 2026-11-16 — Fix: Zone 2 barriers were missing on Play because a debug key had saved Zone 2 as open. Debug opens (keys 1–4) are no longer saved; restored progress is logged at start; the local save was reset.
- 2026-11-16 — Zone purchase at barriers: stop in front, ring fills, pay, barrier pops with sound, zone saved; red blinking price when short of money; barriers know both zones (Z3/Z4 rule); config header Zones for sale; debug keys 1–4 editor-only; Taxi > Reset Save.
- 2026-11-16 — Luggage: suitcase trips (tutorial mission 2 always, then 30%, +$5): load at pickup (suitcase → trunk on foot with action rings), unload at drop-off (trunk → drop point, then paid); no entering the car while carrying; config header Luggage; `Hud.RequestActionRing`.
- 2026-11-16 — Tutorial: the first two trips start on the depot lot (mission 1 Stop_Parking, mission 2 Stop_Depot), `PassengerStop.tutorialMission`, config `tutorialTrips`.
- 2026-11-16 — Stops: Stop_25 and Stop_27 disabled (unreachable behind a barrier), Stop_28 deleted; curbs rebuilt (376 colliders) so the closed pockets get a curb.
- 2026-11-16 — Zone 1 variety: `Stop_Depot` and `Stop_Parking` on the start lot; routes are not repeated (last 2, either direction, beats the distance range; config `recentRoutesToAvoid`); wallet starts with $50 (config `startMoney`).
- 2026-11-16 — Trip loop: one passenger at a time (wait, board, ride, alight, pay), fare by zone multiplier, cross-zone trips between open zones, distance range by highest open zone (fitted to the map), new-zone priority, coins + count-up + procedural ding, route arrow and target ring; `Taxi > Setup Trip System`, `Taxi > Validate Passenger Stops`; editor keys 1–4 open zones.
- 2026-11-15 — Passenger_A (office) and Passenger_B (tourist) models on the driver rig with a new Wave clip, suitcase prop with HandlePoint, shared palette material, Passenger controller and prefabs (`PassengerAssets`, menu Taxi > Setup Passengers).
- 2026-11-13 — Automatic reverse when the stick points behind the car (config: `reverseEnterAngle`, `reverseExitAngle`, `reverseEnterSpeed`, `reverseMaxSpeed`); amber stick knob while reversing.
- 2026-11-15 — Greybox city built from `docs/city_sketch.png` via `CityLayout` + `CityBuilder`: 22 road polylines, 2 turnarounds, 26 buildings, 29 passenger stops, 9 zone barriers, invisible walls; arena removed; spawns on the parking lot. Checked: 789 car-sized overlap samples along all road axes hit nothing; all 9 barriers have colliders; in Play Mode a coasting car (10–12 m/s) stops at the south and west barriers and runs the Z1 south road freely.
- 2026-11-15 — Taxi driver character from Blender replaces the player capsule: FBX + palette, Generic rig, Idle/Walk clips, Animator driven by `PlayerAnimator` (config: `walkAnimSpeedMultiplier`, `walkAnimDampTime`).
- 2026-11-13 — EXIT CAR button removed. Exit is now "hold a finger on the stopped car" using the same ring as entering; `InputReader` gained hold-finger detection (`IsHoldTarget`, `HoldActive`, `HoldPos`); config: `exitHoldRadiusScreenFraction`.
