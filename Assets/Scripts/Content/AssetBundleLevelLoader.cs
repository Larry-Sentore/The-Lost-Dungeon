using UnityEngine;

// Loads the content a level needs when it starts and releases it when the level ends.
// Right now that is the level's music bundle; more per-level content can be added to LevelDefinition.
public class AssetBundleLevelLoader : MonoBehaviour
{
    private string heldBundle;

    // Listens for levels starting.
    private void OnEnable()
    {
        GameEvents.LevelStarted += OnLevelStarted;
    }

    // Stops listening.
    private void OnDisable()
    {
        GameEvents.LevelStarted -= OnLevelStarted;
    }

    // Swaps to the new level's content: acquire the new bundle first, then release the old one,
    // so a bundle shared by both levels is never unloaded and reloaded.
    private void OnLevelStarted(LevelDefinition level)
    {
        if (level == null || ContentManager.Instance == null)
        {
            return;
        }
        string previous = heldBundle;
        heldBundle = level.MusicBundle;
        ContentManager.Instance.Acquire(level.MusicBundle, handle =>
        {
            if (heldBundle != level.MusicBundle)
            {
                return;
            }
            if (!handle.Success)
            {
                GameEvents.RaiseNotification("Level music could not be loaded. Continuing without it.");
                return;
            }
            AudioManager.Instance?.PlayMusic(handle.Get<AudioClip>(level.MusicAsset));
        });
        if (previous != null)
        {
            ContentManager.Instance.Release(previous);
        }
    }

    // Releases the level's content when the scene is unloaded.
    private void OnDestroy()
    {
        if (heldBundle != null && ContentManager.Instance != null)
        {
            AudioManager.Instance?.PlayMusic(null, 0.01f);
            ContentManager.Instance.Release(heldBundle);
            heldBundle = null;
        }
    }
}
