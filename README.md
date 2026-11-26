# The Lost Dungeon

## Gameplay

The game includes five levels. Each level starts from the main scene and unlocks the next level after every enemy is defeated.

| Level | Enemies |
|---|---|
| 1 | 1 Skeleton |
| 2 | 2 Skeletons, 1 Cannon Robot |
| 3 | 2 Rock Giants |
| 4 | 3 Skeletons, 2 Cannon Robots, 2 Rock Giants |
| 5 | 1 Giant Cannon Boss, 5 Cannon Robots |

Enemy durability and player damage:

- Skeleton: 2 hits to defeat; 3 player hits deal 25 health damage each.
- Cannon Robot: 2 hits to defeat; attacks deal 25 health damage.
- Rock Giant: 3 hits to defeat; attacks deal 25 health damage.
- Giant Cannon Boss: 10 hits to defeat; attacks deal 50 health damage.

## Controls

- Move: WASD or arrow keys.
- Attack: left mouse button or Q.
- Sword Slash ability: 1.
- Shield Bash ability: 2.
- Mobile: left side drag movement and right side tap attack.

## Architecture

- Interfaces are used for damage, combat, movement, interaction, collectibles, abilities, saveable state, and enemy behaviours.
- `GameManager` uses the Singleton pattern.
- `GameEvents` uses Observer-style C# events.
- Enemy behaviours use a Strategy and State approach.
- `ObjectPool<T>` provides reusable gameplay components.
- `JsonProgressStore` saves level progress, unlocks, health, and future inventory data as JSON.
- `AssetBundleLevelLoader` loads optional level AssetBundles and unloads the previous bundle. Scene assets remain as a fallback if a bundle is not built.

## Asset Bundle Build

1. Mark level-specific prefabs and AudioSources with AssetBundle names `level-1` through `level-5`.
2. Build the bundles for each target with Unity's AssetBundle Build pipeline.
3. Copy the output folders to `StreamingAssets/StandaloneWindows`, `StreamingAssets/WebGL`, `StreamingAssets/Android`, or `StreamingAssets/iOS`.
4. At runtime, the game loads `level-N` for the selected level and unloads the previous bundle.

The code currently uses scene content as the fallback. Bundles are not duplicated into the repository until level assets are assigned bundle names.

The loader caches each bundle's assets, exposes named prefabs to `LevelManager`, and unloads
the previous bundle before loading the next level. A missing bundle or asset falls back to
scene content and reports the failure without blocking gameplay.

## REST API

`LeaderboardService` submits level completion scores to an HTTP endpoint. If the request fails or the endpoint is unavailable, the game reports a local fallback message and continues to preserve JSON progress.

## Platform Behaviour

Conditional compilation configures input and frame/performance settings for Windows, WebGL, Android, and iOS.

## Tests

The EditMode test assembly contains coverage for enemy health, player damage, inventory operations, level progression, sorting/damage algorithms, and pooling setup.

## Algorithms

- **Nearest-enemy search**: Iterates enemies and compares squared distances to select the
  closest live target, avoiding expensive square roots.
- **Damage calculation**: Combines hit count and base damage, then subtracts defence and
  clamps the result at zero.
- **Descending damage ranking**: Sorts damage values and exposes the highest score for
  leaderboard-style ranking.

## Mobile and build platforms

The project uses Unity conditional compilation for Windows, WebGL, Android, and iOS input
and performance settings. Mobile movement uses the left half of the touchscreen, attacks
use the right half, and the upper/right and lower/right regions activate separate abilities.
