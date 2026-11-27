using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays all music and sound effects (Singleton). It only LISTENS to GameEvents (Observer),
// so no gameplay script ever needs a reference to it. Clips come from the content system.
public class AudioManager : MonoBehaviour
{
    private const int SfxVoices = 12;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    private AudioSource musicA;
    private AudioSource musicB;
    private AudioSource activeMusic;
    private AudioClip requestedMusic;
    private int nextVoice;
    private bool sfxLoaded;
    private bool menuMusicHeld;
    private Coroutine fadeRoutine;

    public static AudioManager Instance { get; private set; }

    // Creates the audio sources, loads the shared sound bundle, and subscribes to game events.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        for (int i = 0; i < SfxVoices; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            sfxSources.Add(source);
        }
        musicA = CreateMusicSource();
        musicB = CreateMusicSource();
        activeMusic = musicA;

        ApplySettings(SaveSystem.Data.Settings);
        Subscribe();
    }

    // Asks the content manager for the shared SFX bundle. It is kept loaded for the whole session.
    private void Start()
    {
        ContentManager.Instance?.Acquire(ContentCatalog.SfxBundle, handle =>
        {
            if (!handle.Success)
            {
                GameEvents.RaiseNotification("Sound effects could not be loaded. The game will continue without them.");
                return;
            }
            foreach (AudioClip clip in handle.GetAll<AudioClip>())
            {
                clips[clip.name] = clip;
            }
            sfxLoaded = true;
        });
        PlayMenuMusic();
    }

    // Makes a looping 2D source for music.
    private AudioSource CreateMusicSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    // Unsubscribes from everything.
    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }
        Unsubscribe();
        Instance = null;
    }

    // Plays a sound effect by name. The same sound will not stack more than once every 50 ms.
    public void PlaySfx(string clipName, float volume = 1f, float pitchVariance = 0.08f)
    {
        if (!sfxLoaded || string.IsNullOrEmpty(clipName) || !clips.TryGetValue(clipName, out AudioClip clip))
        {
            return;
        }
        if (lastPlayed.TryGetValue(clipName, out float last) && Time.unscaledTime - last < 0.05f)
        {
            return;
        }
        lastPlayed[clipName] = Time.unscaledTime;

        AudioSource source = sfxSources[nextVoice];
        nextVoice = (nextVoice + 1) % sfxSources.Count;
        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        source.PlayOneShot(clip, volume);
    }

    // Cross-fades to a new music track. Passing null fades the music out.
    public void PlayMusic(AudioClip clip, float fadeSeconds = 1.2f)
    {
        requestedMusic = clip;
        if (clip != null && activeMusic.clip == clip && activeMusic.isPlaying)
        {
            return;
        }
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(CrossFade(clip, fadeSeconds));
    }

    // Fades the old track out while the new one fades in (uses unscaled time so it works while paused).
    private IEnumerator CrossFade(AudioClip clip, float seconds)
    {
        AudioSource from = activeMusic;
        AudioSource to = activeMusic == musicA ? musicB : musicA;
        float target = MusicVolume;
        if (clip != null)
        {
            to.clip = clip;
            to.volume = 0f;
            to.Play();
            activeMusic = to;
        }

        float startFrom = from.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            from.volume = Mathf.Lerp(startFrom, 0f, k);
            if (clip != null)
            {
                to.volume = Mathf.Lerp(0f, target, k);
            }
            yield return null;
        }
        if (from != activeMusic || clip == null)
        {
            from.Stop();
            from.clip = null;
        }
        fadeRoutine = null;
    }

    // Music volume from the settings (0 when music is switched off).
    private float MusicVolume => SaveSystem.Data.Settings.MusicEnabled ? 0.45f : 0f;

    // Applies volume settings straight away.
    private void ApplySettings(SettingsData settings)
    {
        AudioListener.volume = settings.MasterVolume;
        if (activeMusic != null && fadeRoutine == null)
        {
            activeMusic.volume = settings.MusicEnabled ? 0.45f : 0f;
        }
    }

    // Loads and plays the menu music, holding its bundle until a level starts.
    private void PlayMenuMusic()
    {
        if (menuMusicHeld || ContentManager.Instance == null)
        {
            return;
        }
        menuMusicHeld = true;
        ContentManager.Instance.Acquire(ContentCatalog.MenuMusicBundle, handle =>
        {
            if (handle.Success && menuMusicHeld)
            {
                PlayMusic(handle.Get<AudioClip>(ContentCatalog.MenuMusicAsset));
            }
        });
    }

    // Lets go of the menu music bundle once the level music has faded in (it is unloaded when nothing else uses it).
    private void ReleaseMenuMusic()
    {
        if (!menuMusicHeld)
        {
            return;
        }
        menuMusicHeld = false;
        StartCoroutine(ReleaseMenuMusicLater());
    }

    // Waits for the cross-fade to finish so the menu track is not cut off, then releases its bundle.
    private IEnumerator ReleaseMenuMusicLater()
    {
        yield return new WaitForSecondsRealtime(3f);
        if (menuMusicHeld)
        {
            yield break;
        }
        foreach (AudioSource source in new[] { musicA, musicB })
        {
            if (source.clip != null && source.clip.name == ContentCatalog.MenuMusicAsset && source != activeMusic)
            {
                source.Stop();
                source.clip = null;
            }
        }
        ContentManager.Instance?.Release(ContentCatalog.MenuMusicBundle);
    }

    // Goes back to menu music whenever a scene (re)loads, because every reload starts at the menu.
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        PlayMenuMusic();
    }

    // Subscribes to every gameplay event that should make a sound.
    private void Subscribe()
    {
        GameEvents.PlayerAttacked += OnPlayerAttacked;
        GameEvents.PlayerDamaged += OnPlayerDamaged;
        GameEvents.PlayerHealed += OnPlayerHealed;
        GameEvents.PlayerDied += OnPlayerDied;
        GameEvents.EnemyHit += OnEnemyHit;
        GameEvents.EnemyBlocked += OnEnemyBlocked;
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.EnemyAttacked += OnEnemyAttacked;
        GameEvents.BossPhaseChanged += OnBossPhaseChanged;
        GameEvents.AbilityActivated += OnAbilityActivated;
        GameEvents.ItemCollected += OnItemCollected;
        GameEvents.ChestOpened += OnChestOpened;
        GameEvents.LevelCompleted += OnLevelCompleted;
        GameEvents.GameOver += OnGameOver;
        GameEvents.GameStateChanged += OnGameStateChanged;
        GameEvents.UiSound += OnUiSound;
        GameEvents.SettingsChanged += ApplySettings;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Unsubscribes from every event.
    private void Unsubscribe()
    {
        GameEvents.PlayerAttacked -= OnPlayerAttacked;
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        GameEvents.PlayerHealed -= OnPlayerHealed;
        GameEvents.PlayerDied -= OnPlayerDied;
        GameEvents.EnemyHit -= OnEnemyHit;
        GameEvents.EnemyBlocked -= OnEnemyBlocked;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.EnemyAttacked -= OnEnemyAttacked;
        GameEvents.BossPhaseChanged -= OnBossPhaseChanged;
        GameEvents.AbilityActivated -= OnAbilityActivated;
        GameEvents.ItemCollected -= OnItemCollected;
        GameEvents.ChestOpened -= OnChestOpened;
        GameEvents.LevelCompleted -= OnLevelCompleted;
        GameEvents.GameOver -= OnGameOver;
        GameEvents.GameStateChanged -= OnGameStateChanged;
        GameEvents.UiSound -= OnUiSound;
        GameEvents.SettingsChanged -= ApplySettings;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Sword swoosh.
    private void OnPlayerAttacked() => PlaySfx("dash", 0.7f);
    // Player grunt.
    private void OnPlayerDamaged(int amount) => PlaySfx("hurt", 0.9f);
    // Potion gulp.
    private void OnPlayerHealed() => PlaySfx("heal", 0.8f);
    // Player death.
    private void OnPlayerDied()
    {
        PlaySfx("death", 1f, 0f);
        PlayMusic(null, 1.5f);
    }
    // Sword hitting an enemy.
    private void OnEnemyHit(EnemyKind kind, Vector3 position) => PlaySfx("enemy_hit", 0.9f);
    // Hit bouncing off a guard.
    private void OnEnemyBlocked(EnemyKind kind, Vector3 position) => PlaySfx("land", 1f);
    // Enemy death: a big boom for machines and the boss, a crunch for the rest.
    private void OnEnemyKilled(EnemyKind kind, Vector3 position)
    {
        switch (kind)
        {
            case EnemyKind.GiantCannonBoss:
                PlaySfx("boom_big", 1f, 0f);
                break;
            case EnemyKind.CannonRobot:
                PlaySfx("explosion_01", 0.8f);
                break;
            default:
                PlaySfx("death", 0.6f, 0.15f);
                break;
        }
    }
    // Each enemy type has its own attack sound.
    private void OnEnemyAttacked(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.CannonRobot:
                PlaySfx("charge", 0.35f);
                break;
            case EnemyKind.GiantCannonBoss:
                PlaySfx("laser_01", 0.7f);
                break;
            case EnemyKind.RockGiant:
                PlaySfx("charge", 0.6f, 0.02f);
                break;
            default:
                PlaySfx("jump_classic", 0.5f, 0.15f);
                break;
        }
    }
    // Boss rage roar.
    private void OnBossPhaseChanged(int phase) => PlaySfx("boom_big", 1f, 0f);
    // Ability sounds.
    private void OnAbilityActivated(string ability) => PlaySfx(ability == "Shield Bash" ? "explosion_01" : "teleport", 0.8f);
    // Pickup sounds per item.
    private void OnItemCollected(string itemId)
    {
        PlaySfx(itemId == ItemCatalog.Potion ? "heart" : itemId == ItemCatalog.Gold ? "coin_double" : "gem", 0.8f);
    }
    // Treasure!
    private void OnChestOpened() => PlaySfx("powerup_arp", 1f, 0f);
    // Victory jingle.
    private void OnLevelCompleted(int level)
    {
        PlayMusic(null, 0.5f);
        PlaySfx("level_complete", 1f, 0f);
    }
    // Defeat jingle.
    private void OnGameOver() => PlaySfx("game_over", 1f, 0f);
    // UI clicks and other one-off sounds.
    private void OnUiSound(string soundId) => PlaySfx(soundId, 0.8f, 0f);

    // Menu music in the menu; level music is started by the level's content loader.
    private void OnGameStateChanged(GameStateId previous, GameStateId next)
    {
        if (next == GameStateId.MainMenu)
        {
            PlayMenuMusic();
        }
        else if (next == GameStateId.Playing && previous == GameStateId.MainMenu)
        {
            ReleaseMenuMusic();
        }
    }
}
