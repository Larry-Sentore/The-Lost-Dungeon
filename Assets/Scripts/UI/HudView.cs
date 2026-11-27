using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The extra in-game HUD built on top of the scene's Ingame_UI: ability cooldowns, inventory,
// level info, boss health, interaction prompt, notifications and the content loading overlay.
// Everything updates from GameEvents, so the HUD has no references to gameplay objects.
public class HudView : MonoBehaviour
{
    private class AbilitySlot
    {
        public string Name;
        public RectTransform Cooldown;
        public RectTransform Root;
        public float ReadyPulse;
    }

    private readonly List<AbilitySlot> abilitySlots = new List<AbilitySlot>();
    private readonly Queue<string> notifications = new Queue<string>();
    private readonly Dictionary<string, float> loading = new Dictionary<string, float>();

    private TextMeshProUGUI levelInfo;
    private TextMeshProUGUI inventoryText;
    private TextMeshProUGUI promptText;
    private TextMeshProUGUI healthText;
    private TextMeshProUGUI toastText;
    private CanvasGroup toastGroup;
    private GameObject loadingOverlay;
    private TextMeshProUGUI loadingText;
    private RectTransform loadingFill;
    private GameObject bossBar;
    private RectTransform bossFill;
    private float toastHideAt;
    private float loadingVisibleSince = -1f;
    private PlayerController player;
    private LevelManager levelManager;
    private LevelDefinition level;
    private bool built;

    // Builds all HUD elements. In-game parts go under Ingame_UI so they hide with it; overlays go on the canvas.
    public void Build(Canvas canvas, GameObject gameplayRoot)
    {
        if (built || canvas == null)
        {
            return;
        }
        built = true;
        Transform hudParent = gameplayRoot != null ? gameplayRoot.transform : canvas.transform;
        Transform overlayParent = canvas.transform;

        levelInfo = CreateText(hudParent, "Level Info", new Vector2(640f, 440f), new Vector2(560f, 140f), 30f, TextAlignmentOptions.TopRight);
        healthText = CreateText(hudParent, "Health Value", new Vector2(-415f, 380f), new Vector2(400f, 50f), 28f, TextAlignmentOptions.Center);
        inventoryText = CreateText(hudParent, "Inventory Text", new Vector2(801f, -290f), new Vector2(320f, 130f), 26f, TextAlignmentOptions.BottomRight);
        promptText = CreateText(hudParent, "Interaction Prompt", new Vector2(0f, -320f), new Vector2(900f, 70f), 36f, TextAlignmentOptions.Center);
        promptText.gameObject.SetActive(false);

        AddAbilitySlot(hudParent, "Sword Slash", "1", new Vector2(520f, -460f), new Color(0.95f, 0.6f, 0.15f));
        AddAbilitySlot(hudParent, "Shield Bash", "2", new Vector2(650f, -460f), new Color(0.3f, 0.55f, 0.95f));

        bossBar = CreatePanel(hudParent, "Boss Bar", new Vector2(0f, 360f), new Vector2(800f, 34f), new Color(0f, 0f, 0f, 0.6f)).gameObject;
        bossFill = CreatePanel(bossBar.transform, "Fill", Vector2.zero, Vector2.zero, new Color(0.85f, 0.15f, 0.2f));
        bossFill.anchorMin = new Vector2(0f, 0f);
        bossFill.anchorMax = new Vector2(1f, 1f);
        bossFill.pivot = new Vector2(0f, 0.5f);
        bossFill.offsetMin = new Vector2(4f, 4f);
        bossFill.offsetMax = new Vector2(-4f, -4f);
        CreateText(bossBar.transform, "Boss Name", new Vector2(0f, 36f), new Vector2(800f, 40f), 28f, TextAlignmentOptions.Center).text = "The Cannon King";
        bossBar.SetActive(false);

        RectTransform toast = CreatePanel(overlayParent, "Notification", new Vector2(0f, 300f), new Vector2(1200f, 90f), new Color(0f, 0f, 0f, 0.65f));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        toastText = CreateText(toast, "Text", Vector2.zero, new Vector2(1160f, 80f), 28f, TextAlignmentOptions.Center);
        toastText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform overlay = CreatePanel(overlayParent, "Loading Overlay", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        loadingOverlay = overlay.gameObject;
        loadingText = CreateText(overlay, "Loading Text", new Vector2(0f, 40f), new Vector2(900f, 60f), 36f, TextAlignmentOptions.Center);
        RectTransform track = CreatePanel(overlay, "Track", new Vector2(0f, -20f), new Vector2(600f, 18f), new Color(1f, 1f, 1f, 0.2f));
        loadingFill = CreatePanel(track, "Fill", Vector2.zero, Vector2.zero, new Color(1f, 0.8f, 0.3f));
        loadingFill.anchorMin = Vector2.zero;
        loadingFill.anchorMax = new Vector2(0f, 1f);
        loadingFill.offsetMin = loadingFill.offsetMax = Vector2.zero;
        loadingOverlay.SetActive(false);
        toast.SetAsLastSibling();
        overlay.SetAsLastSibling();

        GameEvents.InventoryChanged += OnInventoryChanged;
        GameEvents.AbilityChanged += OnAbilityChanged;
        GameEvents.InteractionPromptChanged += OnPromptChanged;
        GameEvents.Notification += Notify;
        GameEvents.RequestStatusChanged += OnRequestStatus;
        GameEvents.ContentLoadProgress += OnContentProgress;
        GameEvents.ContentLoadFinished += OnContentFinished;
        GameEvents.LevelStarted += OnLevelStarted;
        GameEvents.BossPhaseChanged += OnBossPhase;
        RefreshInventory();
    }

    // Stops listening to events.
    private void OnDestroy()
    {
        if (!built)
        {
            return;
        }
        GameEvents.InventoryChanged -= OnInventoryChanged;
        GameEvents.AbilityChanged -= OnAbilityChanged;
        GameEvents.InteractionPromptChanged -= OnPromptChanged;
        GameEvents.Notification -= Notify;
        GameEvents.RequestStatusChanged -= OnRequestStatus;
        GameEvents.ContentLoadProgress -= OnContentProgress;
        GameEvents.ContentLoadFinished -= OnContentFinished;
        GameEvents.LevelStarted -= OnLevelStarted;
        GameEvents.BossPhaseChanged -= OnBossPhase;
    }

    // Updates timers, cooldown bars, the boss bar, notifications and the loading overlay.
    private void Update()
    {
        if (!built)
        {
            return;
        }
        UpdateLevelInfo();
        UpdateAbilities();
        UpdateBossBar();
        UpdateToast();
        UpdateLoading();
    }

    // Shows the numeric health next to the health bar.
    public void SetHealth(float current, float maximum)
    {
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
        }
    }

    // Remembers the level so the info panel can show its name.
    private void OnLevelStarted(LevelDefinition definition)
    {
        level = definition;
        levelManager = FindAnyObjectByType<LevelManager>();
        player = FindAnyObjectByType<PlayerController>();
        RefreshInventory();
        Notify($"Level {definition.Number}: {definition.Title}");
    }

    // Shows the level name, enemies left and the timer.
    private void UpdateLevelInfo()
    {
        if (level == null || levelManager == null)
        {
            levelInfo.text = "";
            return;
        }
        float t = levelManager.ElapsedTime;
        levelInfo.text = $"Level {level.Number} - {level.Title}\nEnemies left: {levelManager.AliveEnemyCount}\nTime {Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}";
    }

    // Shrinks each ability's dark cooldown cover as it recharges, and pulses when ready.
    private void UpdateAbilities()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
            if (player == null)
            {
                return;
            }
        }
        for (int i = 0; i < abilitySlots.Count && i < player.Abilities.Count; i++)
        {
            AbilitySlot slot = abilitySlots[i];
            float remaining = player.Abilities[i].CooldownRemaining01;
            slot.Cooldown.anchorMax = new Vector2(1f, remaining);
            slot.ReadyPulse = Mathf.MoveTowards(slot.ReadyPulse, 0f, Time.unscaledDeltaTime * 3f);
            slot.Root.localScale = Vector3.one * (1f + 0.15f * slot.ReadyPulse);
        }
    }

    // Shows the boss health bar while the boss is alive in the current level.
    private void UpdateBossBar()
    {
        EnemyController boss = null;
        if (levelManager != null)
        {
            foreach (EnemyController enemy in levelManager.ActiveEnemies)
            {
                if (enemy != null && enemy.Kind == EnemyKind.GiantCannonBoss && !enemy.IsDead)
                {
                    boss = enemy;
                    break;
                }
            }
        }
        bossBar.SetActive(boss != null);
        if (boss != null)
        {
            bossFill.localScale = new Vector3(Mathf.Max(0.001f, boss.Health01), 1f, 1f);
        }
    }

    // Flashes the ability slot when it becomes ready again.
    private void OnAbilityChanged(string abilityName, bool ready)
    {
        if (!ready)
        {
            return;
        }
        foreach (AbilitySlot slot in abilitySlots)
        {
            if (slot.Name == abilityName)
            {
                slot.ReadyPulse = 1f;
            }
        }
    }

    // Updates the inventory list.
    private void OnInventoryChanged(string itemId, int quantity)
    {
        RefreshInventory();
    }

    // Lists the inventory sorted by type (merge sort), with the potion hotkey.
    private void RefreshInventory()
    {
        if (inventoryText == null)
        {
            return;
        }
        PlayerController current = player != null ? player : FindAnyObjectByType<PlayerController>();
        if (current == null)
        {
            inventoryText.text = "";
            return;
        }
        StringBuilder text = new StringBuilder();
        foreach (IInventoryItem item in current.Inventory.GetSorted(InventorySortMode.ByType))
        {
            ItemDefinition definition = ItemCatalog.Get(item.ItemId);
            string hex = ColorUtility.ToHtmlStringRGB(definition.Color);
            string hint = item.ItemId == ItemCatalog.Potion ? (PlatformBehaviours.UseTouchControls ? "" : " [H]") : "";
            text.AppendLine($"<color=#{hex}>{definition.DisplayName}</color> x{item.Quantity}{hint}");
        }
        inventoryText.text = text.Length > 0 ? text.ToString() : "Inventory empty";
    }

    // Shows or hides the "[E] Open..." prompt.
    private void OnPromptChanged(string prompt)
    {
        promptText.gameObject.SetActive(!string.IsNullOrEmpty(prompt));
        promptText.text = prompt ?? "";
    }

    // Queues a message to show at the top of the screen.
    public void Notify(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
        if (notifications.Count < 5)
        {
            notifications.Enqueue(message);
        }
    }

    // Shows online request failures as notifications (successes stay quiet).
    private void OnRequestStatus(string message, bool success)
    {
        if (!success)
        {
            Notify(message);
        }
    }

    // Shows the next queued message for a few seconds, then fades it out.
    private void UpdateToast()
    {
        float now = Time.unscaledTime;
        if (now >= toastHideAt && notifications.Count > 0)
        {
            string message = notifications.Dequeue();
            toastText.text = message;
            toastHideAt = now + Mathf.Clamp(message.Length * 0.06f, 2.5f, 6f);
        }
        float target = now < toastHideAt ? 1f : 0f;
        toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, target, Time.unscaledDeltaTime * 4f);
    }

    // Tracks the progress of a content load.
    private void OnContentProgress(string label, float progress)
    {
        loading[label] = progress;
        if (loadingVisibleSince < 0f)
        {
            loadingVisibleSince = Time.unscaledTime;
        }
    }

    // Stops tracking a finished load, and reports failures.
    private void OnContentFinished(string label, bool success)
    {
        loading.Remove(label);
        if (!success)
        {
            Notify($"Could not load '{label}'. The game will continue without it.");
        }
    }

    // Shows the loading overlay only for loads that take longer than a moment, so quick loads do not flicker.
    private void UpdateLoading()
    {
        if (loading.Count == 0)
        {
            loadingVisibleSince = -1f;
            loadingOverlay.SetActive(false);
            return;
        }
        bool show = Time.unscaledTime - loadingVisibleSince > 0.25f;
        loadingOverlay.SetActive(show);
        if (!show)
        {
            return;
        }
        float total = 0f;
        foreach (float value in loading.Values)
        {
            total += value;
        }
        float progress = total / loading.Count;
        loadingText.text = $"Loading content... {Mathf.RoundToInt(progress * 100f)}%";
        loadingFill.anchorMax = new Vector2(progress, 1f);
    }

    // Creates one ability slot: coloured square, key label, name, and a dark cover that shows the cooldown.
    private void AddAbilitySlot(Transform parent, string abilityName, string key, Vector2 position, Color color)
    {
        RectTransform root = CreatePanel(parent, abilityName + " Slot", position, new Vector2(110f, 110f), color);
        RectTransform cover = CreatePanel(root, "Cooldown", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.65f));
        cover.anchorMin = Vector2.zero;
        cover.anchorMax = new Vector2(1f, 0f);
        cover.offsetMin = cover.offsetMax = Vector2.zero;
        string keyLabel = PlatformBehaviours.UseTouchControls ? "" : key;
        CreateText(root, "Key", new Vector2(-34f, 34f), new Vector2(40f, 40f), 28f, TextAlignmentOptions.Center).text = keyLabel;
        CreateText(root, "Name", new Vector2(0f, -80f), new Vector2(160f, 40f), 22f, TextAlignmentOptions.Center).text = abilityName;
        abilitySlots.Add(new AbilitySlot { Name = abilityName, Cooldown = cover, Root = root });
    }

    // Creates a plain coloured rectangle.
    private static RectTransform CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    // Creates a TextMeshPro label.
    private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    // Lets the boss phase change pulse the boss bar.
    private void OnBossPhase(int phase)
    {
        if (bossFill != null)
        {
            bossFill.GetComponent<Image>().color = new Color(1f, 0.1f, 0.4f);
        }
    }
}
