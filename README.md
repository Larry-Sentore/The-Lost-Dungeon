# Chronicles of the Lost Dungeon

A level-based dungeon action game made in Unity 6 (6000.6.0f1, URP) for Windows, WebGL and Android.
Fight through five dungeon levels. Each level has its own enemies, and you claim a treasure chest at the end
of each one. Progress, inventory, stats and settings are saved as JSON.

---

## Gameplay

1. **Main Menu**: Start Game, Continue, Level Selection, Settings, Exit.
2. **Level Selection**: each level shows one of three icons: completed, available or locked. Locked levels
   cannot be entered; clicking one plays an error sound.
3. **In a level**: defeat every enemy. When the last one falls, a **treasure chest** appears where it died.
   Walk up to it and press **Interact** to open it. You get soul gems, gold and a potion, and the level is complete.
4. **Level complete**: shows your score, leaderboard rank and time, plus anything you unlocked. The next
   level unlocks, and you can continue with **Next Level**. If you die, the fail screen offers **Retry**.

| Level | Title | Enemies | Featured enemy (bestiary) |
|---|---|---|---|
| 1 | The Forgotten Halls | 1 Skeleton | Skeleton |
| 2 | The Machine Crypt | 1 Skeleton, 1 Cannon Robot | Cannon Robot |
| 3 | The Golem Vault | 2 Skeletons, 1 Rock Giant | Rock Giant |
| 4 | The Iron Barracks | 2 Skeletons, 3 Cannon Robots, 3 Rock Giants | Rock Giant |
| 5 | Throne of the Cannon King | 4 Cannon Robots, 4 Rock Giants, the Cannon King (boss) | Boss |

**Progression rewards:** chest loot (gems, gold, potion), loot drops from enemies, a score and leaderboard entry,
the next level unlocked, and unlockable titles for finishing levels 4 and 5.

### Enemy types

| Enemy | Role | Behaviour |
|---|---|---|
| Skeleton | Warrior | Rushes you and slashes after a short, glowing wind-up. Any hit staggers it. |
| Cannon Robot | Archer | Can't move. Tracks you, charges (glows orange) and fires slow cannon balls you can dodge. |
| Rock Giant | Guardian | Defensive. Raises a guard that **blocks every hit from the front**, then slams the ground around it with heavy knockback. Hit it from behind, during its long slam recovery, or break its guard with **Shield Bash**. |
| Cannon King | Boss | Two phases. Phase 1 fires single shots plus a three-way burst every third shot. Below 50% health it becomes enraged: it turns faster, fires sooner and every shot is a five-way fan. It has its own health bar. |

### Controls

| Action | Keyboard / Mouse | Gamepad | Touch (mobile / tablet browsers) |
|---|---|---|---|
| Move | WASD / Arrows | Left stick | Floating joystick (left half of the screen) |
| Attack | Left click / Space / J | X (West) / RT | ATTACK button |
| Sword Slash (ability 1) | 1 / Q | LB | SLASH button |
| Shield Bash (ability 2) | 2 / Right click | RB | BASH button |
| Interact (open chest) | E / F | A (South) | USE button |
| Drink potion | H | Y (North) | POTION button |
| Pause / back | Esc or P (P also works in browsers, where Esc leaves fullscreen) | Start | II button |

Attacks auto-aim at the nearest enemy within 3.5 m.

---

## Project structure

```
Assets/Scripts/
  Core/         Interfaces, GameEvents (Observer), GameManager (Singleton), GameStateMachine + StateMachine<T> (State),
                ObjectPool<T>, pooled HitEffects, GameSystemsBootstrap, DamageInfo
  Algorithms/   GameplayAlgorithms (search, merge sort, weighted random, steering), DamageCalculator
  Player/       PlayerController, Abilities (Strategy), CameraController
  Enemies/      EnemyController, EnemyVisuals, EnemyStates (State), EnemyBehaviours (Strategy), EnemyProfile, Projectile(+Spawner)
  Level/        LevelManager, LevelCatalog
  Interaction/  Interactable, PlayerInteractor, TreasureChest, Pickup(+Spawner), LootTable
  Inventory/    InventorySystem, ItemCatalog
  Save/         SaveData, SaveSystem (JSON + per-platform storage), ProgressionRules
  Content/      ContentManager (AssetBundles), ContentCatalog, ReferenceCounter, AssetBundleLevelLoader
  Audio/        AudioManager
  Online/       RestClient, BestiaryService (REST API), LeaderboardService
  Platform/     PlatformBehaviours, PlayerInput (input sources), TouchControls, VirtualJoystick, TouchButton
  UI/           UiGameController, HudView
  Editor/       ContentBuilder (AssetBundle build + pre-build hook)
Assets/Tests/EditMode/  NUnit tests
Assets/Audio/Music/     Original music loops (managed content)
```

Gameplay code compiles into `LostDungeon.Runtime.asmdef`, editor tools into `LostDungeon.Editor.asmdef`,
and the tests into `LostDungeon.EditModeTests`, which references the runtime assembly. This means the
tests exercise the real game code.

`GameSystemsBootstrap` creates every system at start-up. Session services (`ContentManager`,
`AudioManager`, `BestiaryService` and the object pools) live on a `DontDestroyOnLoad` object. The scene systems
(`LevelManager`, `GameManager`, `UiGameController`) are added if they are missing, so nothing has to be
wired up by hand in the scene.

---

## Advanced C#

### Interfaces (10)
`IDamageable`, `IAttackable`, `IMovable`, `IInteractable`, `ICollectable`, `IAbility`, `ISaveable`,
`IEnemyBehaviour`, `IState<TId,TContext>`, `IGameState`, plus `ISaveStorage`, `IRestClient` and `IPlayerInputSource`.

Examples of how they reduce coupling:
- **`IDamageable`** is implemented by `PlayerController` and `EnemyController`. Attacks, projectiles and
  abilities only ever call `TakeDamage(DamageInfo)`, so they work on anything that can be hurt.
- **`ISaveable`** is implemented by `InventorySystem` and `PlayerController`. `SaveSystem.Save()` asks every
  registered object to write its own data, so the save system never needs to know what an inventory is.
- **`ISaveStorage`** has a file version (Windows and Android), a `PlayerPrefs` version (WebGL) and a memory
  version (tests). The JSON code is the same everywhere.
- **`IPlayerInputSource`** has keyboard/mouse, gamepad and touch versions. The player reads one merged
  `PlayerInputFrame` and doesn't care which device was used.

### Delegates and events
`GameEvents` is a static event hub with about 30 events, including health, damage, enemy hit/blocked/killed,
boss phase, level started/completed, game state, abilities, inventory, items, chests, content loading,
REST status, notifications, UI sounds and settings. Other delegates are used too: `Func<float>` clocks in the
abilities (so they can be tested), `Action<ContentHandle>` callbacks for content loading, `Comparison<T>` in
the sort algorithm, and `Func<Vector3,bool>` in the steering algorithm.

**One event, many systems:** when an enemy dies, `EnemyController` raises `GameEvents.EnemyKilled`. The
`AudioManager` plays a death sound, the `PickupSpawner` rolls the loot table and drops an item, and the
`LevelManager` (through the enemy's own `Died` event) updates the objective, the stats and the chest. None of
these systems reference each other.

---

## Design patterns

| Pattern | Where | Why |
|---|---|---|
| **Singleton** | `GameManager`, `AudioManager`, `ContentManager`, `BestiaryService` | One global instance each. Duplicates remove themselves. Session services persist across scene reloads. |
| **Observer** | `GameEvents`, plus `EnemyController.Died`, `InventorySystem.Changed`, `GameStateMachine.Changed` | The UI, audio, loot and saving all react to gameplay without direct references. The HUD, for example, has no reference to the player's inventory. |
| **Strategy** | Abilities (`IAbility`: `SwordSlashAbility`, `ShieldBashAbility`, kept in an `AbilitySet`); enemy AI (`IEnemyBehaviour`: melee, guardian, cannon, boss) | The player just calls `Activate`, and each enemy body runs whichever behaviour its type is registered with. New abilities or behaviours are new classes, with no new `if`/`switch` in existing code. |
| **State** | `StateMachine<TId,TContext>` with enemy states (`Idle`, `Chase`, `MeleeAttack`, `RangedAttack`, `Guard`, `Stagger`, `Dead`); `GameStateMachine` with `MainMenu`, `Playing`, `Paused`, `LevelComplete`, `GameOver` | Each state is a class with Enter/Tick/Exit. Game states declare which transitions are allowed (you can't pause from the menu) and set `Time.timeScale`. |
| **Object Pooling** | `ObjectPool<T>`, used for cannon projectiles (`ProjectileSpawner`), hit sparks (`HitEffects`) and loot pickups (`PickupSpawner`) | These are spawned constantly in combat, so reusing them avoids garbage-collection spikes, which matters most on WebGL and mobile. Pools are recalled when a scene reloads. |

Behaviours are also **composed** from reusable states. For example, the Guardian is
`Idle → Chase → Guard → MeleeAttack(slam) → Guard`, built from the same state classes as the Skeleton but
with different parameters.

---

## Data management (JSON save system)

`SaveSystem` serialises a single `SaveData` object with `JsonUtility`. It contains:

- **Level progression:** highest unlocked level and per-level completion
- **Player statistics:** enemies defeated, deaths, chests opened, potions used, total score, best time per level
- **Inventory:** item stacks
- **Settings:** master volume, music on/off, player name
- **Unlockable content:** earned titles
- **Leaderboard:** top 10 local scores
- **Bestiary cache:** the last REST API responses, kept for offline use

Storage is chosen with conditional compilation:

- **Windows / Android / iOS:** `Application.persistentDataPath/lost-dungeon-save.json`. The file is written
  to a `.tmp` file first and then swapped in, so a crash can't corrupt the save.
- **WebGL:** the same JSON is stored in `PlayerPrefs`, which browsers back with IndexedDB.

Corrupt or old saves never crash the game. Corrupt JSON gives a fresh save, and `ProgressionRules.Normalize`
repairs missing fields and upgrades version-1 saves.

The game saves when a level is completed, when you die, pause or close settings, when the app is paused or
sent to the background (important on mobile), and on quit.

**Why JSON:** it is human-readable, so saves are easy to inspect and debug during development. It works on every
target platform, `JsonUtility` is built in and fast, and the format can grow with new fields without breaking
older saves.

---

## Asset and content management (AssetBundles)

**Approach:** Unity **AssetBundles**, built per platform from one list (`ContentCatalog`) and loaded at runtime by
`ContentManager` through `UnityWebRequestAssetBundle`. That loading call works the same on Windows, WebGL
(over HTTP) and Android (from inside the APK).

### Managed assets

| Bundle | Contents | Who loads it | Lifetime |
|---|---|---|---|
| `audio-sfx` | All 30 sound effects (combat, enemies, UI, jingles) | `AudioManager` | Shared and held for the whole session |
| `music-menu` | Menu music | `AudioManager` when the menu shows | Released about 3 s after a level starts (after the cross-fade) |
| `music-dungeon` | Dungeon music (levels 1–4) | `AssetBundleLevelLoader` when a level starts | Released when the level or scene ends |
| `music-boss` | Boss music (level 5) | `AssetBundleLevelLoader` | Released when the level or scene ends |

None of these clips are referenced by the scene or stored in `Resources`. Their only way into a build is
through the bundles.

### Loading and release

- `Acquire(bundle, callback)` adds a reference and loads the bundle if nobody else has. `Release(bundle)`
  removes a reference.
- `ReferenceCounter<T>` makes sure a bundle is **only unloaded (`Unload(true)`) when the last user releases it**.
  Shared content therefore stays available while in use. For example, levels 1–4 share `music-dungeon`, so
  moving between them never unloads it, because `AssetBundleLevelLoader` acquires the new bundle before
  releasing the old one.
- If a bundle is released while it is still downloading, it is unloaded as soon as the download finishes.

### Loading feedback and failures

- Progress is raised through `GameEvents.ContentLoadProgress`. The HUD shows a **"Loading content… NN%" overlay
  with a progress bar** for any load that takes longer than 0.25 s.
- Failures (missing file, timeout after 20 s, wrong platform) **never stop the game**. The caller gets a
  `ContentHandle` with `Success = false`, the player sees a notification ("…continuing without it"), and the
  game keeps running without that audio. Failed loads are forgotten, so they can be retried later.
- **Editor convenience:** if the bundles haven't been built, the editor loads the same assets straight from the
  project ("simulation mode", `#if UNITY_EDITOR`), so Play mode always works. Player builds always use
  real bundles.

### Adding or replacing content
Add the asset path to a bundle in `ContentCatalog.Bundles`, or add a new `ContentBundleDefinition`. Point a
`LevelDefinition` at it if it's level content. No gameplay code changes.

### Building and deploying the content

**Bundles build automatically.** `ContentBuildPreprocessor` (an `IPreprocessBuildWithReport`) rebuilds the
bundles for the target platform right before every player build and writes them to
`Assets/StreamingAssets/Content/<Platform>/`, so they ship inside every build. If the bundle build fails,
the player build stops.

To build them by hand: **Lost Dungeon ▸ Content ▸ Build Bundles (Active Platform)**, or
**… (Windows + WebGL + Android)**. **Delete Built Bundles** removes them.

| Target | Bundle folder | Notes |
|---|---|---|
| Windows | `StreamingAssets/Content/StandaloneWindows` | Loaded through a `file://` URL |
| WebGL | `StreamingAssets/Content/WebGL` | Served next to the page and loaded over HTTP(S). Upload the whole build folder (including `StreamingAssets`) to Unity Play. |
| Android | `StreamingAssets/Content/Android` | Packed into the APK/AAB and loaded from the `jar:file://` path |

Built bundles are build output, so they are git-ignored.

---

## REST API: Bestiary (dnd5eapi.co)

`BestiaryService` calls the public, key-free, CORS-enabled **D&D 5e API**
(`https://www.dnd5eapi.co/api/2014/monsters/{index}`) for each level's featured enemy (skeleton, iron-golem,
stone-golem or shield-guardian).

- **What it adds:** a lore notification when the level starts (type, armour class, HP, challenge rating and a
  known attack), and a **score bounty** worked out from the monster's challenge rating (50 points per CR,
  between 10 and 600).
- **Failure handling:** `UnityRestClient` sets an 8 s timeout and retries once, but only for connection
  errors or 5xx responses. A 404 is not retried. If the request still fails, the game uses the **copy cached in
  the save file**, labelled "(saved)". With no cache, it shows "Bestiary unavailable right now", and the bounty
  falls back to the enemy's built-in value. Parsing is wrapped so bad JSON or an HTML error page returns
  `null` instead of throwing. Gameplay never waits on the request.
- The HTTP client is behind `IRestClient`, so it can be swapped or mocked.

---

## Algorithms

| Algorithm | Purpose | How it works | Why this one |
|---|---|---|---|
| **Nearest-neighbour search** (`GameplayAlgorithms.FindNearest`) | Player auto-aim, finding the closest interactable (chest) | One linear pass with an optional filter and range, comparing **squared** distances | There are only a few dozen candidates, so a linear scan is O(n) with no allocations and needs no spatial structure. Squared distance skips the square root. |
| **Merge sort** (`GameplayAlgorithms.MergeSort`) | Leaderboard ranking, inventory display order | Recursive split and merge into a shared buffer. Stable. | Guaranteed O(n log n), and **stable**, so equal scores keep the order they were achieved in, which is fair for ties. |
| **Weighted random selection** (`WeightedRandomIndex`) | Enemy loot drops (gem, potion or nothing) per enemy type | Roulette wheel: sum the weights, then walk the running total until it passes `roll × total` | Lets drop rates be tuned as data per enemy. The roll is passed in, so it is deterministic in tests. |
| **Feeler steering** (`ChooseSteeringDirection`) | Enemy navigation around pillars and walls | If the straight path is blocked, try turning ±35°, ±70°, ±110° and take the first clear direction | Cheap and local, with no baked NavMesh needed for these open dungeon rooms. Enemies flow around obstacles instead of getting stuck. |
| **Damage maths** (`DamageCalculator`) | All damage, critical hits (15%), guardian blocking, knockback | `round(base × mult × crit) − defence`, never below 0. Guard check is the angle between the guardian's forward and the attacker. Knockback starts at `v₀ = 2d / T` so it covers exactly `d` metres while slowing linearly. | One place for all combat maths, so every attacker and defender follows the same rules and it is easy to unit-test. |

---

## Multi-platform and conditional compilation

| Where | What changes per platform |
|---|---|
| `PlatformBehaviours.Configure` | Mobile: 60 fps cap, shorter shadow distance, screen stays awake, landscape only. WebGL: the browser drives frame timing. Windows: vsync on, full shadow distance. |
| `PlatformBehaviours.ConfigureCanvas` | The UI scales with screen size from the 1920×1080 design. Mobile matches height more (0.75) so buttons stay large in landscape. |
| `PlayerInputRouter` | Mobile: touch and gamepad. WebGL: keyboard/mouse and gamepad, plus touch on phone and tablet browsers. Windows: keyboard/mouse and gamepad. |
| `SaveSystem.CreateDefaultStorage` | WebGL: `PlayerPrefs` (IndexedDB). Everything else: a JSON file. |
| `ContentManager.GetBundleUrl` / `ContentCatalog.PlatformFolder` | Different bundle folder and URL form for each platform. |
| `ContentManager` | `#if UNITY_EDITOR` simulation mode. |
| `UiGameController.ExitGame` | Editor: stop play mode. WebGL: return to the menu (pages can't close themselves). Others: `Application.Quit`. |
| `PlatformBehaviours.InteractButtonLabel` | The prompt reads "[Tap USE]" on mobile and "[E]" on desktop. |

---

## UI and audio

- **HUD:** health bar plus a number, ability slots with cooldown covers and a pulse when ready, inventory list
  sorted with merge sort, objective, level title, enemies left and a timer, a boss health bar, the interaction
  prompt and notifications.
- **Screens:** main menu, level select (icons only), settings (volume slider, music toggle, player name), pause,
  level complete (score, rank, time, unlocks, Next Level) and game over (Retry). Screens switch only in
  response to `GameStateChanged`.
- **Audio:** `AudioManager` subscribes to gameplay events and plays combat sounds (swing, hit, block, potion,
  abilities), enemy sounds (each type has its own attack and death sound), UI sounds (every button, plus an
  error sound on locked levels), and level-complete and game-over jingles. Background music cross-fades between
  menu, dungeon and boss tracks. Music and SFX use a 12-voice round-robin SFX pool.

---

## Unit tests (NUnit, Edit Mode)

**Run them:** Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.

There are 34 tests in `Assets/Tests/EditMode/LostDungeonTests.cs`, all against the real runtime code:

- **Damage calculations:** multiplier, criticals and defence; damage never goes negative; guard blocks only from the front; knockback distance maths; critical rolls; heal cap
- **Inventory:** add and remove; invalid input; sort by quantity; save/restore round trip
- **Level unlocking:** new-save state; unlocks only the next level; no level 6; legacy save upgrade; score formula
- **Save/load:** JSON round trip through storage; corrupt JSON; New Game keeps settings
- **State transitions:** game state time scale; invalid transitions rejected; generic state machine exit/enter order
- **Algorithms:** merge sort correctness and stability; weighted selection boundaries; nearest search range; steering; loot table; leaderboard ranking
- **Object pooling:** reuse; double release ignored
- **Other systems:** content reference counting; ability cooldowns; enemy profile copies; behaviour registry; bestiary JSON parsing; level catalog → content bundles

---

## Extending the game

**New enemy type:**
1. Add a value to `EnemyKind`.
2. Register its numbers with `EnemyProfiles.Register`.
3. Write a behaviour class that builds its states from the existing state classes, or adds new ones.
4. Register it with `EnemyBehaviourRegistry.Register`.
5. Place the model in a `Level_N_Enemies` group with an `EnemyController` set to the new kind.

The movement, damage, knockback, visuals, loot, audio, HUD and level tracking all work without changes.

**New ability:** subclass `BaseAbility` and `Add` it to the player's `AbilitySet`.

**New item:** call `ItemCatalog.Register`.

**New level:** add a `LevelDefinition`, plus the scene's `Level_N_Enemies` group and `SpawnPoint_Level_N`.

---

## Credits

- Characters and animations: Mixamo (Ch46 and its animation clips), *Humanoid Giant* by Kevin Iglesias,
  and a Skeleton model pack
- Environment: Low Poly Dungeons Lite
- Sound effects: *Pixel SFX Free* by HeyHeyThere
- Music: original loops synthesised for this project (`Assets/Audio/Music`)
- Bestiary data: [D&D 5e API](https://www.dnd5eapi.co) (SRD content)
