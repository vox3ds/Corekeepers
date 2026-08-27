using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreKeepers
{
    public sealed class SkillUpgradePopupUI : MonoBehaviour
    {
        [SerializeField] private Component waveText;
        [SerializeField] private Component choiceText;
        [SerializeField] private Button option1;
        [SerializeField] private Button option2;
        [SerializeField] private Component option1Label;
        [SerializeField] private Component option2Label;
        [SerializeField] private Image option1Icon;
        [SerializeField] private Image option2Icon;

        private HeroSkillController controller;
        private HeroSkillDefinition[] choices;
        private bool choosing;

        public static SkillUpgradePopupUI Instance { get; private set; }
        public bool IsOpen => gameObject.activeInHierarchy;

        public static void AttachToPreparedPopup()
        {
            var popup = FindNamed("Skill Upgrade Popup");
            if (popup != null && popup.GetComponent<SkillUpgradePopupUI>() == null)
                popup.AddComponent<SkillUpgradePopupUI>();
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

        public void Show(HeroSkillController source, int wave, HeroSkillDefinition[] offered)
        {
            if (source == null || offered == null || offered.Length != 2)
            {
                Debug.LogError($"Wave {wave} skill popup needs exactly two choices.", this);
                return;
            }
            BindPreparedHierarchy();
            controller = source;
            choices = offered;
            choosing = false;
            HeroSkillsUI.SetText(waveText, $"Wave {wave} defeted");
            HeroSkillsUI.SetText(choiceText, wave % 2 == 1 ? "Choose your active skill" : "Choose your passive skill");
            ApplyOption(option1Label, option1Icon, offered[0]);
            ApplyOption(option2Label, option2Icon, offered[1]);
            option1.interactable = true;
            option2.interactable = true;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            controller = null;
            choices = null;
            choosing = false;
        }

        public void BindPreparedHierarchy()
        {
            waveText ??= FindText("WaveXDefetedText");
            choiceText ??= FindText("ChooseYourSkillText");
            option1 ??= FindNamed("Option1")?.GetComponent<Button>();
            option2 ??= FindNamed("Option2")?.GetComponent<Button>();
            if (option1 == null && FindNamed("Option1") != null) option1 = FindNamed("Option1").AddComponent<Button>();
            if (option2 == null && FindNamed("Option2") != null) option2 = FindNamed("Option2").AddComponent<Button>();
            option1Label ??= FindLabel(option1);
            option2Label ??= FindLabel(option2);
            option1Icon ??= EnsureIcon(option1);
            option2Icon ??= EnsureIcon(option2);
            if (option1 != null)
            {
                option1.onClick.RemoveAllListeners();
                option1.onClick.AddListener(() => Choose(0));
            }
            if (option2 != null)
            {
                option2.onClick.RemoveAllListeners();
                option2.onClick.AddListener(() => Choose(1));
            }
        }

        private void Choose(int index)
        {
            if (choosing || controller == null || choices == null || index < 0 || index >= choices.Length)
                return;
            choosing = true;
            option1.interactable = false;
            option2.interactable = false;
            controller.ChooseUpgrade(choices[index]);
            Close();
        }

        private static void ApplyOption(Component label, Image icon, HeroSkillDefinition definition)
        {
            HeroSkillsUI.SetText(label, definition.DisplayName);
            if (icon == null) return;
            icon.sprite = definition.Icon256;
            icon.enabled = definition.Icon256 != null;
            icon.preserveAspect = true;
        }

        private static Image EnsureIcon(Button button)
        {
            if (button == null) return null;
            var existing = button.transform.Find("Icon");
            if (existing != null) return existing.GetComponent<Image>();
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)iconObject.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var side = Mathf.Max(48f, Mathf.Min(button.GetComponent<RectTransform>().rect.width,
                button.GetComponent<RectTransform>().rect.height) * 0.62f);
            rect.sizeDelta = Vector2.one * side;
            rect.anchoredPosition = new Vector2(0f, side * 0.12f);
            var image = iconObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            iconObject.transform.SetAsFirstSibling();
            return image;
        }

        private static Component FindLabel(Button button)
        {
            if (button == null) return null;
            foreach (var child in button.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Label") continue;
                return (Component)child.GetComponent<TMP_Text>() ?? child.GetComponent<Text>();
            }
            return null;
        }

        private static Component FindText(string objectName)
        {
            var target = FindNamed(objectName);
            return target == null ? null : (Component)target.GetComponent<TMP_Text>() ?? target.GetComponent<Text>();
        }

        private static GameObject FindNamed(string objectName)
        {
            foreach (var candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == objectName) return candidate.gameObject;
            return null;
        }
    }
}
