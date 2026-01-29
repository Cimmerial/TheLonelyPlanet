# AGENTS.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Repo type
- Unity project (Unity Editor **2022.3.23f1**) (see `ProjectSettings/ProjectVersion.txt`).
- Primary source is under `Assets/` (scripts/assets) plus Unity config under `Packages/` and `ProjectSettings/`.

## Common commands

### Open the project
- Open the repo folder in **Unity Hub** (recommended) or open `LonelyPlanet.sln` in Rider/Visual Studio for code navigation.

### Headless Unity “compile/import” (fast sanity check)
This runs Unity in batch mode to import the project and compile scripts.

```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.23f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -quit -projectPath "$(pwd)" -logFile -
```

Notes:
- If Unity is installed elsewhere, update `UNITY` accordingly.
- Editor logs (macOS): `~/Library/Logs/Unity/Editor.log`.

### Run tests (Unity Test Framework)
This project includes `com.unity.test-framework` in `Packages/manifest.json`.

Run EditMode tests:
```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.23f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -quit -projectPath "$(pwd)" -runTests -testPlatform EditMode -testResults "TestResults-EditMode.xml" -logFile -
```

Run PlayMode tests:
```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.23f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -quit -projectPath "$(pwd)" -runTests -testPlatform PlayMode -testResults "TestResults-PlayMode.xml" -logFile -
```

Run a single test (if present) via filter:
```bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.23f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -quit -projectPath "$(pwd)" -runTests -testPlatform EditMode -testFilter "Namespace.ClassName.TestName" -testResults "TestResults.xml" -logFile -
```

### Builds
No scripted build pipeline (e.g., `BuildPipeline.BuildPlayer` / `-executeMethod`) was found in `Assets/Scripts/**`.
- Build from the Unity Editor via **File → Build Settings**.

## High-level architecture (big picture)

### Scene entrypoint & runtime assets
- The main scene appears to be `Assets/Scenes/SampleScene.unity`.
- Runtime-loaded assets live under `Assets/Resources/`:
  - `VehicleSprites/*` (vehicle textures loaded by `Vehicle`)
  - `ComponentSprites/*` and `ComponentMetadata/*` (component sprites + ScriptableObject metadata)
  - `ChassisConfigs/*` (vehicle chassis ScriptableObjects)

### Gravity & orbital mechanics
- `Assets/Scripts/Gravity/GravityManager.cs` is a singleton registry of gravity sources (`IBigGravity`). It must exist in the scene.
- `Planet` (`Assets/Scripts/Planets/Planet.cs`) and `Moon` (`Assets/Scripts/Planets/Moon.cs`) implement `IBigGravity` and register with `GravityManager`.
- **Important behavior:** `GravityManager.CalculateGravityAt(position, affectedObject)` intentionally prevents `IBigGravity` objects from being affected by other `IBigGravity` sources.
- `OrbitalRails` (`Assets/Scripts/Gravity/OrbitalRails.cs`) maintains stable orbits around a `Planet`/`Moon` and can be configured as:
  - strict rails (sets position/velocity directly)
  - soft rails (applies corrective forces and can break if external forces exceed a threshold)
- Design notes for this system exist in `Assets/Scripts/text/orbitalRailsPlan.md`.

### Atmosphere & drag
- `AtmosphericPhysics` (`Assets/Scripts/Gravity/AtmosphericPhysics.cs`) is a reusable component for “objects that experience atmosphere”.
  - Applies atmospheric drag when within a planet’s atmosphere.
  - Applies gravity each physics step via `GravityManager.CalculateGravityAt(transform.position, gameObject)`.
  - Tracks “grounded” state based on collision callbacks forwarded from objects (`OnPlanetCollisionEnter/Stay/Exit`).

### Planets, asteroids, and procedural colliders
- `PlanetGenerator` (`Assets/Scripts/Generators/PlanetGenerator.cs`) generates planet textures + sprite renderers + colliders at runtime.
  - Uses a **circle collider** for perfect spheres, or a generated polygon collider when `shapeVariation` is enabled.
- `AsteroidGenerator` (`Assets/Scripts/Generators/AsteroidGenerator.cs`) generates asteroid textures/sprites/colliders and adds a Rigidbody2D.
- `Utility.GeneratePolygonCollider(...)` (`Assets/Scripts/Utility/Utility.cs`) generates polygon colliders from textures; generation has multiple modes (`Accurate`, `Legacy`, `Convex`). Many runtime objects rely on this.

### Asteroid fragmentation & “breakable” resources
- `Asteroid` (`Assets/Scripts/Planets/Asteroid.cs`) implements `IBreakable` and accumulates impact force; above threshold it fragments.
- `AsteroidFragmentGenerator` (`Assets/Scripts/Generators/AsteroidFragmentGenerator.cs`) splits the source texture into Voronoi fragments, applies optional “edge erosion” mass loss, then spawns new fragment GameObjects (each fragment gets an `Asteroid` component).
- Resource-related types:
  - `Resource` ScriptableObject + enum in `Assets/Scripts/ResourcesTLP/Resource.cs`
  - `DealForceData`, `BrokenResourceData`, `IBreakable` in `Assets/Scripts/ResourcesTLP/`

### Vehicles & component attachment
- `Vehicle` (`Assets/Scripts/Vehicles/Vehicle.cs`) is the abstract base for vehicles (currently `Crawler`).
  - Loads a vehicle texture from `Resources/VehicleSprites/{spriteName}` and builds a sprite + polygon collider.
  - Uses `VehicleChassisConfig` (ScriptableObject) to define component slots; slot positions are converted from pixel offsets to local-space using `Utility.GLOBAL_PPU`.
- `VComponent` (`Assets/Scripts/Vehicles/Components/VComponent.cs`) is the abstract base for components attached to vehicles.
  - Loads `ComponentMetadata/{componentName}_Metadata` from `Resources` and then loads the corresponding `ComponentSprites/*` texture.
  - `Digger` (`Assets/Scripts/Vehicles/Components/Digger.cs`) is a concrete component that applies force damage to `IBreakable` colliders.

### Camera/debug tooling
- `CameraController` (`Assets/Scripts/Camera/CameraController.cs`) supports free camera and locking to planets/vehicles; has a small UI overlay via TextMeshPro.
- `GravityGizmos` (`Assets/Scripts/Debug/GravityGizmos.cs`) provides extensive scene gizmo visualization for gravity/orbits/thresholds.
- `PhysicsSetup` (`Assets/Scripts/System/PhysicsSetup.cs`) tweaks `Physics2D` solver iteration counts/max translation speed at runtime.
