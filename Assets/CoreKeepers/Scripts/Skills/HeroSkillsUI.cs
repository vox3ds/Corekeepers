using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreKeepers
{
    public sealed class HeroSkillsUI : MonoBehaviour
    {
        [Serializable]
        private sealed class Slot
        {
            public Image icon;
            public Component timer;
            public GameObject locked;
            public Image cooldown;
            public GameObject selected;
            [NonSerialized] public Button button;
        }

        [SerializeField] private Slot[] slots = new Slot[4];
        private HeroSkillController controller;
        private bool bound;

        public static HeroSkillsUI Instance { get; private set; }

        public static void AttachToPreparedPanel()
        {
            if (Instance != null) return;
            var panel = FindNamedObject("SkillsPanel");
            if (panel != null && panel.GetComponent<HeroSkillsUI>() == null)
                panel.AddComponent<HeroSkillsUI>();
        }

        private void Awake()
        {
            Instance = this;
            BindPreparedHierarchy();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Present(HeroSkillController source)
        {
            controller = source;
            BindPreparedHierarchy();
            Refresh();
        }

        public void Refresh()
        {
            if (controller == null || !bound)
                return;
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                var definition = controller.GetSlot(index);
                var unlocked = controller.IsSlotUnlocked(index);
                if (slot.icon != null)
                {
                    slot.icon.sprite = definition != null ? definition.Icon64 : null;
                    slot.icon.enabled = definition != null && definition.Icon64 != null;
                }
                if (slot.locked != null) slot.locked.SetActive(!unlocked);
                if (slot.selected != null) slot.selected.SetActive(index == controller.SelectedSlot);
                if (slot.button != null) slot.button.interactable = unlocked && definition != null;

                var remaining = controller.GetRemainingCooldown(index);
                var cooldown = definition != null ? controller.GetEffectiveCooldown(definition) : 0f;
                if (slot.cooldown != null)
                {
                    slot.cooldown.type = Image.Type.Filled;
                    slot.cooldown.fillMethod = Image.FillMethod.Radial360;
                    slot.cooldown.fillAmount = remaining <= 0f || cooldown <= 0f
                        ? 1f
                        : Mathf.Clamp01(1f - remaining / cooldown);
                }
                var coolingDown = remaining > 0f;
                SetText(slot.timer, remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0"));
                if (slot.timer != null) slot.timer.gameObject.SetActive(coolingDown);
            }
        }

        public void BindPreparedHierarchy()
        {
            if (slots == null || slots.Length != 4) slots = new Slot[4];
            for (var index = 0; index < 4; index++)
            {
                slots[index] ??= new Slot();
                var number = index + 1;
                slots[index].icon ??= FindNamed<Image>($"Skill{number}Icon");
                slots[index].timer ??= FindText($"Skill{number}Timer");
                slots[index].locked ??= FindNamedObject($"Skill{number}Lock");
                slots[index].cooldown ??= FindNamed<Image>($"Skill{number}Cooldown");
                slots[index].selected ??= FindNamedObject($"Skill{number}Selected");
                var clickTarget = slots[index].icon != null ? slots[index].icon.gameObject : null;
                if (clickTarget == null) continue;
                slots[index].button = clickTarget.GetComponent<Button>() ?? clickTarget.AddComponent<Button>();
                slots[index].button.targetGraphic = slots[index].icon;
                slots[index].button.onClick.RemoveAllListeners();
                var captured = index;
                slots[index].button.onClick.AddListener(() => controller?.SelectSlot(captured));
            }
            bound = Array.TrueForAll(slots, slot => slot.icon != null && slot.cooldown != null &&
                slot.locked != null && slot.selected != null);
            if (!bound)
                Debug.LogError("Hero skills HUD could not bind all Skill1..4 prepared objects.", this);
        }

        private static T FindNamed<T>(string objectName) where T : Component
        {
            var target = FindNamedObject(objectName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static Component FindText(string objectName)
        {
            var target = FindNamedObject(objectName);
            if (target == null) return null;
            return (Component)target.GetComponent<TMP_Text>() ?? target.GetComponent<Text>();
        }

        private static GameObject FindNamedObject(string objectName)
        {
            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == objectName) return candidate.gameObject;
            return null;
        }

        internal static void SetText(Component target, string value)
        {
            if (target is TMP_Text tmp) tmp.text = value;
            else if (target is Text text) text.text = value;
        }
    }
}
