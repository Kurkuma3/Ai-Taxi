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

File: `Assets/Scripts/CameraRig/TopDownCamera.cs`. Fixed-angle follow camera with offset `(0, 18, −8)`, zero yaw (so screen-up = world +Z, which the car and player steering rely on), exponential smoothing. `Target` is swapped by the state machine.

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

## Change log

- 2026-11-13 — Automatic reverse when the stick points behind the car (config: `reverseEnterAngle`, `reverseExitAngle`, `reverseEnterSpeed`, `reverseMaxSpeed`); amber stick knob while reversing.
- 2026-11-15 — Taxi driver character from Blender replaces the player capsule: FBX + palette, Generic rig, Idle/Walk clips, Animator driven by `PlayerAnimator` (config: `walkAnimSpeedMultiplier`, `walkAnimDampTime`).
- 2026-11-13 — EXIT CAR button removed. Exit is now "hold a finger on the stopped car" using the same ring as entering; `InputReader` gained hold-finger detection (`IsHoldTarget`, `HoldActive`, `HoldPos`); config: `exitHoldRadiusScreenFraction`.
