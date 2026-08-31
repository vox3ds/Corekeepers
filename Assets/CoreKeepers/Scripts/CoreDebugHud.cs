using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CoreKeepers
{
    public sealed class CoreDebugHud : MonoBehaviour
    {
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private GUIStyle codeStyle;
        private GUIStyle resurrectionStyle;

        private void OnGUI()
        {
            EnsureStyles();
            var manager = NetworkManager.Singleton;
            var local = NetworkWarrior.Local;
            var role = manager == null || !manager.IsListening ? "Offline" : manager.IsHost ? "Host" : "Client";
            var connection = manager != null && manager.IsListening ? "Connected" : "Disconnected";
            var players = manager != null && manager.IsListening ? manager.ConnectedClientsIds.Count : 0;
            var ping = "N/A";
            if (manager != null && manager.IsListening && manager.NetworkConfig.NetworkTransport != null)
                ping = $"{manager.NetworkConfig.NetworkTransport.GetCurrentRtt(manager.LocalClientId)} ms";

            const float width = 470f;
            const float height = 540f;
            var top = Mathf.Max(12f, (Screen.height - height) * 0.5f);
            GUILayout.BeginArea(new Rect(12f, top, width, height), GUI.skin.box);
            GUILayout.Label("COREKEEPERS DEBUG", headerStyle);
            GUILayout.Label($"Nickname: {(local != null ? local.Nickname : CoreSettings.Nickname)}", labelStyle);
            GUILayout.Label($"Role: {role}", labelStyle);
            GUILayout.Label($"Connection: {connection}", labelStyle);
            GUILayout.Label($"Players: {players}/{CoreSessionManager.PlayerLimit}", labelStyle);
            GUILayout.Label($"Ping: {ping}", labelStyle);
            DrawSessionCode();
            var animator = local != null ? local.GetComponent<WarriorProceduralAnimator>() : null;
            GUILayout.Label($"Character state: {(animator != null ? animator.DisplayState : "Waiting for spawn")}", labelStyle);
            GUILayout.Label($"Class: {(local != null ? local.PlayerClass.ToString() : "Waiting")}", labelStyle);
            GUILayout.Label($"Carried: Ore {(local != null ? local.CarriedOre : 0)}  |  Core Shards {(local != null ? local.CarriedCoreShards : 0)}  |  Total {(local != null ? local.CarriedResources : 0)}/{(local != null ? local.CarryingCapacity : 20)}", labelStyle);
            var core = CoreDebugDeposit.Instance;
            GUILayout.Label($"Core storage: Ore {(core != null ? core.DepositedOre : 0)}  |  Core Shards {(core != null ? core.DepositedCoreShards : 0)}", labelStyle);
            GUILayout.Space(7f);
            GUILayout.Label("Hold / drag LMB: continuously move or retarget", labelStyle);
            GUILayout.Label("LMB target: attack / mine / build / revive / deposit", labelStyle);
            GUILayout.Label("RMB ground/building/Core: radial or upgrade menu", labelStyle);
            GUILayout.Label("C: switch class (Warrior / Mage / Builder / Healer)", labelStyle);
            GUILayout.Label("K: debug-down the local player", labelStyle);
            GUILayout.Label("V: spawn selected enemy  |  = / -: change enemy", labelStyle);
            GUILayout.Label("F6: VFX Debug Lab (heroes + enemies)", labelStyle);
            GUILayout.EndArea();
            DrawResurrectionPrompts(local);
        }

        private void DrawResurrectionPrompts(NetworkWarrior local)
        {
            var camera = Camera.main;
            if (camera == null) return;
            foreach (var hero in FindObjectsByType<NetworkWarrior>())
            {
                if (!hero.IsDowned) continue;
                var screen = camera.WorldToScreenPoint(hero.transform.position + Vector3.up * 2.6f);
                if (screen.z <= 0f) continue;
                var rect = new Rect(screen.x - 95f, Screen.height - screen.y - 20f, 190f, 40f);
                if (hero.IsBeingResurrected)
                {
                    GUI.Label(rect, $"Resurrecting... {Mathf.RoundToInt(hero.ResurrectionProgress * 100f)}%",
                        resurrectionStyle);
                }
                else if (local != null && !local.IsDowned && hero != local &&
                         Vector3.Distance(local.transform.position, hero.transform.position) <= local.InteractionRange)
                {
                    if (GUI.Button(rect, "[R] RESURRECT"))
                        local.TryStartResurrection(hero);
                }
            }
        }

        private void DrawSessionCode()
        {
            var sessions = CoreSessionManager.Instance;
            GUILayout.Space(8f);
            GUILayout.Label("MULTIPLAYER JOIN CODE", headerStyle);
            if (sessions != null && !string.IsNullOrWhiteSpace(sessions.JoinCode))
            {
                GUILayout.Label(sessions.JoinCode, codeStyle, GUILayout.Height(42f));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("COPY CODE", GUILayout.Height(28f)))
                    GUIUtility.systemCopyBuffer = sessions.JoinCode;
                GUILayout.Label($"Session players: {sessions.SessionPlayerCount}/{CoreSessionManager.PlayerLimit}", labelStyle);
                GUILayout.EndHorizontal();
                GUILayout.Label("On PC 2: Menu → enter this code → JOIN", labelStyle);
            }
            else
            {
                GUILayout.Label("LOCAL DEBUG HOST — NO RELAY CODE", codeStyle, GUILayout.Height(42f));
                GUILayout.Label("Start with the DEBUG button in Menu to create an online debug session.", labelStyle);
            }
            if (sessions != null && !string.IsNullOrWhiteSpace(sessions.Status))
                GUILayout.Label($"Session: {sessions.Status}", labelStyle);
            GUILayout.Space(8f);
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
                return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            headerStyle = new GUIStyle(labelStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
            codeStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.78f, 0.18f) }
            };
            resurrectionStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 1f, 0.72f) }
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class CoreHeroPanel : MonoBehaviour
    {
        private Text playerNumberText;
        private Text playerLevelText;
        private Text healthText;
        private Text oreText;
        private Text coreShardsText;
        private Text bagText;
        private Image healthFill;
        private Image oreFill;
        private Image coreShardsFill;
        private GameObject bagFullIcon;
        private GameObject builderIcon;
        private GameObject warriorIcon;
        private GameObject healerIcon;
        private GameObject mageIcon;
        private readonly Image[] effectSlots = new Image[8];
        private readonly GameObject[] effectSlotObjects = new GameObject[8];
        private readonly List<HeroSkillDefinition> activePassives = new();
        private static readonly Dictionary<string, Sprite> placeholderIcons = new();
        private float nextEffectSlotBindAt;

        public static void AttachToScenePanel()
        {
            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.name != "HeroPanel")
                    continue;
                if (candidate.GetComponent<CoreHeroPanel>() == null)
                    candidate.gameObject.AddComponent<CoreHeroPanel>();
                return;
            }

            Debug.LogWarning("Core Gameplay Canvas/HeroPanel was not found in the loaded scene.");
        }

        private void Awake()
        {
            playerNumberText = FindText("PlayerNumber", "PlayerNumberText", "PlayerText");
            playerLevelText = FindText("PlayerLevel", "PlayerLevelText", "LevelText");
            healthText = FindText("HpText", "HPText", "HealthText");
            oreText = FindText("OreText", "HpText (1)");
            coreShardsText = FindText("CoreShardsText", "HpText (2)");
            bagText = FindText("BagText");
            healthFill = FindImage("HPFill", "HpFill", "HealthFill");
            oreFill = FindImage("OreFill");
            coreShardsFill = FindImage("CoreShardsFill");
            bagFullIcon = FindChild("BagFullIcon")?.gameObject;
            builderIcon = FindChild("BuilderIcon")?.gameObject;
            warriorIcon = FindChild("WarrorIcon", "WarriorIcon")?.gameObject;
            healerIcon = FindChild("HealerIcon")?.gameObject;
            mageIcon = FindChild("MageIcon")?.gameObject;
            BindEffectSlots();
            ShowWaitingState();
        }

        private void Update()
        {
            var hero = NetworkWarrior.Local;
            if (hero == null)
            {
                ShowWaitingState();
                return;
            }

            var displayedHealth = Mathf.CeilToInt(hero.CurrentHealth);
            var displayedMaximumHealth = Mathf.CeilToInt(hero.MaximumHealth);
            var capacity = hero.CarryingCapacity;
            var carried = hero.CarriedResources;

            SetText(playerNumberText, $"P{hero.PlayerNumber}");
            SetText(playerLevelText, hero.PlayerLevel.ToString());
            SetText(healthText, $"{displayedHealth} / {displayedMaximumHealth}");
            SetText(oreText, hero.CarriedOre.ToString());
            SetText(coreShardsText, hero.CarriedCoreShards.ToString());
            SetText(bagText, $"{carried} / {capacity}");

            SetFill(healthFill, hero.MaximumHealth > 0f ? hero.CurrentHealth / hero.MaximumHealth : 0f);
            SetFill(oreFill, capacity > 0 ? (float)hero.CarriedOre / capacity : 0f);
            SetFill(coreShardsFill, capacity > 0 ? (float)hero.CarriedCoreShards / capacity : 0f);
            if (bagFullIcon != null)
                bagFullIcon.SetActive(carried >= capacity);
            SetClassIcons(hero.PlayerClass);
            RefreshEffects(hero);
        }

        private void ShowWaitingState()
        {
            SetText(playerNumberText, "P-");
            SetText(playerLevelText, "1");
            SetText(healthText, "100 / 100");
            SetText(oreText, "0");
            SetText(coreShardsText, "0");
            SetText(bagText, "0 / 20");
            SetFill(healthFill, 1f);
            SetFill(oreFill, 0f);
            SetFill(coreShardsFill, 0f);
            if (bagFullIcon != null)
                bagFullIcon.SetActive(false);
            SetIcon(builderIcon, false);
            SetIcon(warriorIcon, false);
            SetIcon(healerIcon, false);
            SetIcon(mageIcon, false);
            ClearEffectSlots();
        }

        private void BindEffectSlots()
        {
            for (var index = 0; index < effectSlots.Length; index++)
            {
                var slot = FindChild($"EffectSlot{index + 1}");
                effectSlotObjects[index] = slot != null ? slot.gameObject : null;
                effectSlots[index] = slot != null
                    ? slot.GetComponent<Image>() ?? slot.GetComponentInChildren<Image>(true)
                    : null;
            }
        }

        private void RefreshEffects(NetworkWarrior hero)
        {
            if (Time.unscaledTime >= nextEffectSlotBindAt && effectSlotObjects[0] == null)
            {
                nextEffectSlotBindAt = Time.unscaledTime + 1f;
                BindEffectSlots();
            }

            var slotIndex = 0;
            if (hero.IsDowned)
                AddEffect(ref slotIndex, null, "Dead", new Color(0.22f, 0.02f, 0.02f));
            var statuses = hero.ActiveDebuffs;
            if ((statuses & EnemyDebuff.OnFire) != 0)
                AddEffect(ref slotIndex, HeroSkillCatalog.Find(102)?.Icon64, "On Fire", new Color(1f, 0.18f, 0.02f));
            if ((statuses & EnemyDebuff.Stun) != 0)
                AddEffect(ref slotIndex, HeroSkillCatalog.Find(10)?.Icon64, "Stun", new Color(1f, 0.85f, 0.1f));
            if ((statuses & EnemyDebuff.Freeze) != 0)
                AddEffect(ref slotIndex, null, "Frozen", new Color(0.25f, 0.75f, 1f));
            if ((statuses & EnemyDebuff.Chill) != 0)
                AddEffect(ref slotIndex, HeroSkillCatalog.Find(103)?.Icon64, "Chill", new Color(0.35f, 0.9f, 1f));
            if ((statuses & EnemyDebuff.Poisoned) != 0)
                AddEffect(ref slotIndex, null, "Poison", new Color(0.4f, 0.95f, 0.12f));
            if ((statuses & EnemyDebuff.Swamp) != 0)
                AddEffect(ref slotIndex, null, "Swamp", new Color(0.35f, 0.42f, 0.12f));

            var skills = hero.GetComponent<HeroSkillController>();
            skills?.GetActivePassiveEffects(activePassives);
            foreach (var passive in activePassives)
            {
                if (slotIndex >= effectSlots.Length) break;
                AddEffect(ref slotIndex, passive.Icon64, passive.DisplayName, new Color(0.45f, 0.55f, 0.95f));
            }
            for (; slotIndex < effectSlots.Length; slotIndex++) SetEffectSlot(slotIndex, null, null);
        }

        private void AddEffect(ref int slotIndex, Sprite icon, string effectName, Color placeholderColor)
        {
            if (slotIndex >= effectSlots.Length) return;
            SetEffectSlot(slotIndex, icon != null ? icon : GetPlaceholder(effectName, placeholderColor), effectName);
            slotIndex++;
        }

        private void SetEffectSlot(int index, Sprite icon, string effectName)
        {
            var slotObject = effectSlotObjects[index];
            var image = effectSlots[index];
            if (slotObject != null && slotObject.activeSelf != (icon != null)) slotObject.SetActive(icon != null);
            if (image == null) return;
            image.sprite = icon;
            image.enabled = icon != null;
            image.preserveAspect = true;
            if (icon != null) image.gameObject.name = image.transform == slotObject?.transform
                ? slotObject.name : $"{effectName} Icon";
        }

        private void ClearEffectSlots()
        {
            for (var index = 0; index < effectSlots.Length; index++) SetEffectSlot(index, null, null);
        }

        private static Sprite GetPlaceholder(string key, Color color)
        {
            if (placeholderIcons.TryGetValue(key, out var cached)) return cached;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"Placeholder - {key}",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * size + x] = distance > center ? Color.clear :
                    distance > center - 2f ? Color.white : color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
            sprite.name = $"Placeholder - {key}";
            sprite.hideFlags = HideFlags.DontSave;
            placeholderIcons[key] = sprite;
            return sprite;
        }

        private void SetClassIcons(CorePlayerClass playerClass)
        {
            SetIcon(builderIcon, playerClass == CorePlayerClass.Builder);
            SetIcon(warriorIcon, playerClass == CorePlayerClass.Warrior);
            SetIcon(healerIcon, playerClass == CorePlayerClass.Healer);
            SetIcon(mageIcon, playerClass == CorePlayerClass.Mage);
        }

        private static void SetIcon(GameObject icon, bool active)
        {
            if (icon != null && icon.activeSelf != active)
                icon.SetActive(active);
        }

        private Text FindText(params string[] names)
        {
            var child = FindChild(names);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private Image FindImage(params string[] names)
        {
            var child = FindChild(names);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private Transform FindChild(params string[] names)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                foreach (var requestedName in names)
                    if (child.name == requestedName)
                        return child;
            return null;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private static void SetFill(Image target, float value)
        {
            if (target != null)
                target.fillAmount = Mathf.Clamp01(value);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CoreStatusPanel : MonoBehaviour
    {
        private Image statusFill;
        private Text healthText;
        private Text oreText;
        private Text coreShardsText;
        private GameObject upgrade1Icon;
        private GameObject upgrade2Icon;
        private GameObject upgrade3Icon;
        private GameObject dangerIcon;

        public static void AttachToScenePanel()
        {
            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.name != "CorePanel")
                    continue;
                if (candidate.GetComponent<CoreStatusPanel>() == null)
                    candidate.gameObject.AddComponent<CoreStatusPanel>();
                return;
            }

            Debug.LogWarning("Core Gameplay Canvas/CorePanel was not found in the loaded scene.");
        }

        private void Awake()
        {
            statusFill = FindImage("CoreStatusFill");
            healthText = FindText("CoreHpText", "CoreHPText");
            oreText = FindText("CoreText", "OreText");
            coreShardsText = FindText("CoreShardsText");
            upgrade1Icon = FindChild("Upgrade1Icon")?.gameObject;
            upgrade2Icon = FindChild("Upgrade2Icon")?.gameObject;
            upgrade3Icon = FindChild("Upgrade3Icon")?.gameObject;
            dangerIcon = FindChild("CoreDangerIcon")?.gameObject;
            ShowWaitingState();
        }

        private void Update()
        {
            var core = CoreDebugDeposit.Instance;
            if (core == null)
            {
                ShowWaitingState();
                return;
            }

            var displayedHealth = Mathf.CeilToInt(core.CurrentHealth);
            var displayedMaximumHealth = Mathf.CeilToInt(core.MaximumHealth);
            SetText(healthText, $"{displayedHealth} / {displayedMaximumHealth}");
            SetText(oreText, core.DepositedOre.ToString());
            SetText(coreShardsText, core.DepositedCoreShards.ToString());
            SetFill(statusFill, core.MaximumHealth > 0f ? core.CurrentHealth / core.MaximumHealth : 0f);
            SetActive(upgrade1Icon, core.Level >= 1);
            SetActive(upgrade2Icon, core.Level >= 2);
            SetActive(upgrade3Icon, core.Level >= 3);
            SetActive(dangerIcon, core.IsInDanger);
        }

        private void ShowWaitingState()
        {
            SetText(healthText, "1000 / 1000");
            SetText(oreText, "0");
            SetText(coreShardsText, "0");
            SetFill(statusFill, 1f);
            SetActive(upgrade1Icon, true);
            SetActive(upgrade2Icon, false);
            SetActive(upgrade3Icon, false);
            SetActive(dangerIcon, false);
        }

        private Text FindText(params string[] names)
        {
            var child = FindChild(names);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private Image FindImage(params string[] names)
        {
            var child = FindChild(names);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private Transform FindChild(params string[] names)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                foreach (var requestedName in names)
                    if (child.name == requestedName)
                        return child;
            return null;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private static void SetFill(Image target, float value)
        {
            if (target != null)
                target.fillAmount = Mathf.Clamp01(value);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
