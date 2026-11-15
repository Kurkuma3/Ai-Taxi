# Taxi Driver — core-loop prototype

Unity **6000.6.1f1**, URP, Android (portrait). Open `Assets/Scenes/Main.unity` and press Play.

## Controls
- **On foot (one finger):** touch and hold anywhere — the character walks forward. Drag the finger to steer (a floating stick appears under it).
- **Enter the car:** stand next to it; the ring over the car fills in 1 s and you get in.
- **Driving (same one-finger stick):** touch and drag — the car steers toward the stick direction, deflection = throttle. Release — brake to a stop.
- **Reverse (automatic, no button):** pull the stick to the sector behind the car (≥ 120° off its heading). Rolling forward it brakes first, then backs up aiming its rear at the stick; the stick knob turns amber. Swing the stick back to the front (< 80°) to drive forward again. This is how you get out of a wall.
- **Exit:** stop the car, then hold a finger on it — the same ring fills in 1 s and you get out. No buttons anywhere.
- Editor fallbacks: WASD / arrows = stick, E = instant exit, mouse = one finger.

How each piece works is written up in [IMPLEMENTATION.md](IMPLEMENTATION.md).

## Structure
- `Assets/Scripts` — runtime code (`Taxi.Runtime`): `InputReader`, `CarController`, `PlayerController`, `PlayerAnimator`, `PlayerStateMachine` (OnFoot → Entering → Driving → Exiting), `TopDownCamera`, `Hud`.
- `Assets/Art` — `Models/TaxiDriver.fbx` (low-poly driver with Idle/Walk, made in Blender through MCP), `Textures/TaxiDriverPalette.png`, `Materials`, `Animations`.
- `ArtSource/` — Blender sources (`TaxiDriver.blend`), outside `Assets` so Unity does not import them.
- `IMPLEMENTATION.md` — how each mechanic is built and how to verify it; updated with every feature.
- `Assets/Config/GameplayConfig.asset` — every tunable number (speeds, turn rate, enter radius/time, stick size).
- `Assets/Editor/SceneBootstrap.cs` — menu **Taxi > Build Main Scene** rebuilds the greybox scene from primitives.
- `Assets/Tests/PlayMode/CoreLoopTests.cs` — simulated-input smoke test of the whole enter/drive/stop/exit loop.

## Headless commands
```
Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod Taxi.EditorTools.SceneBootstrap.Build
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```
