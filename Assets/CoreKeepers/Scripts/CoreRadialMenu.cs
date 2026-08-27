using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CoreKeepers
{
    public sealed class CoreRadialMenu : MonoBehaviour
    {
        [Header("Build Radial")]
        [SerializeField] private RectTransform buildRoot;
        [SerializeField] private Button[] buildButtons;
        [SerializeField] private Text[] buildLabels;
        [SerializeField] private Text buildHint;

        [Header("Upgrade Radial")]
        [SerializeField] private RectTransform upgradeRoot;
        [SerializeField] private Text upgradeTitle;
        [SerializeField] private Button upgradeAButton;
        [SerializeField] private Button upgradeBButton;
        [SerializeField] private Text upgradeALabel;
        [SerializeField] private Text upgradeBLabel;

        [Header("Core Popup")]
        [SerializeField] private RectTransform corePopup;
        [SerializeField] private Text coreTitle;
        [SerializeField] private Button coreGuardianButton;
        [SerializeField] private Button corePulseButton;
        [SerializeField] private Text coreGuardianLabel;
        [SerializeField] private Text corePulseLabel;

        [Header("Currency Icons")]
        [SerializeField] private Sprite orePriceIcon;
        [SerializeField] private Sprite coreShardsPriceIcon;

        private Vector3 buildPosition;
        private CoreBuilding upgradeTarget;
        private CoreDebugDeposit coreTarget;
        private Image[] buildPriceIcons;
        private Image upgradeAPriceIcon;
        private Image upgradeBPriceIcon;
        private Image coreGuardianPriceIcon;
        private Image corePulsePriceIcon;

        public static CoreRadialMenu Instance { get; private set; }
        public bool IsOpen => buildRoot.gameObject.activeSelf || upgradeRoot.gameObject.activeSelf || corePopup.gameObject.activeSelf;

        private void Awake()
        {
            Instance = this;
            for (var index = 0; index < buildButtons.Length; index++)
            {
                var type = (CoreBuildingType)index;
                buildButtons[index].onClick.AddListener(() => SelectBuilding(type));
            }
            upgradeAButton.onClick.AddListener(() => SelectUpgrade(1));
            upgradeBButton.onClick.AddListener(() => SelectUpgrade(2));
            coreGuardianButton.onClick.AddListener(() => SelectCoreUpgrade(1));
            corePulseButton.onClick.AddListener(() => SelectCoreUpgrade(2));
            ResolveCurrencyIconsFromScene();
            CreatePriceIcons();
            CloseAll();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (orePriceIcon == null)
                orePriceIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/OreIcon.png");
            if (coreShardsPriceIcon == null)
                coreShardsPriceIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/CoreShardsIcon.png");
        }
#endif

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var popupBlocksInput = SkillUpgradePopupUI.Instance != null && SkillUpgradePopupUI.Instance.IsOpen;
            if (mouse.rightButton.wasPressedThisFrame && !GameplayInputGate.IsPointerOverUi && !popupBlocksInput)
                HandleRightClick(mouse.position.ReadValue());
            else if (mouse.leftButton.wasPressedThisFrame && IsOpen && !GameplayInputGate.IsPointerOverUi)
                CloseAll();

            var local = NetworkWarrior.Local;
            if (local != null && upgradeTarget != null && Vector3.Distance(local.transform.position, upgradeTarget.transform.position) > 5f)
                CloseAll();
            if (local != null && coreTarget != null && Vector3.Distance(local.transform.position, coreTarget.transform.position) > 5f)
                CloseAll();

            RefreshCosts();
        }

        private void HandleRightClick(Vector2 screenPosition)
        {
            var camera = Camera.main;
            if (camera == null || !Physics.Raycast(camera.ScreenPointToRay(screenPosition), out var hit, 500f))
            {
                CloseAll();
                return;
            }

            var core = hit.collider.GetComponentInParent<CoreDebugDeposit>();
            if (core != null)
            {
                OpenCore(core, screenPosition);
                return;
            }

            var building = hit.collider.GetComponentInParent<CoreBuilding>();
            if (building != null)
            {
                if (building.CanUpgrade)
                    OpenUpgrade(building, screenPosition);
                else
                    CloseAll();
                return;
            }

            if (CoreBuilding.CanPlace(hit.point, out var validPosition))
                OpenBuild(validPosition, screenPosition);
            else
                CloseAll();
        }

        private void OpenBuild(Vector3 position, Vector2 screenPosition)
        {
            CloseAll();
            buildPosition = position;
            buildRoot.position = ClampToScreen(screenPosition, 165f);
            buildRoot.gameObject.SetActive(true);
            RefreshCosts();
        }

        private void OpenUpgrade(CoreBuilding building, Vector2 screenPosition)
        {
            CloseAll();
            upgradeTarget = building;
            upgradeRoot.position = ClampToScreen(screenPosition, 150f);
            upgradeRoot.gameObject.SetActive(true);
            RefreshCosts();
        }

        private void OpenCore(CoreDebugDeposit core, Vector2 screenPosition)
        {
            CloseAll();
            coreTarget = core;
            corePopup.position = ClampToScreen(screenPosition, 190f);
            corePopup.gameObject.SetActive(true);
            RefreshCosts();
        }

        private void SelectBuilding(CoreBuildingType type)
        {
            if (NetworkWarrior.Local != null)
                NetworkWarrior.Local.RequestPlaceBuilding(type, buildPosition);
            CloseAll();
        }

        private void SelectUpgrade(byte branch)
        {
            if (NetworkWarrior.Local != null && upgradeTarget != null)
                NetworkWarrior.Local.RequestBuildingUpgrade(upgradeTarget, branch);
            CloseAll();
        }

        private void SelectCoreUpgrade(byte branch)
        {
            if (NetworkWarrior.Local != null && coreTarget != null)
                NetworkWarrior.Local.RequestCoreUpgrade(coreTarget, branch);
            CloseAll();
        }

        private void RefreshCosts()
        {
            var deposit = CoreDebugDeposit.Instance;
            var ore = deposit != null ? deposit.DepositedOre : 0;
            var shards = deposit != null ? deposit.DepositedCoreShards : 0;
            for (var index = 0; index < buildButtons.Length; index++)
            {
                var type = (CoreBuildingType)index;
                var cost = CoreBuildingCatalog.Cost(type);
                var currency = CoreBuildingCatalog.BuildCurrency(type);
                buildLabels[index].text = $"{CoreBuildingCatalog.Icon(type)}  {CoreBuildingCatalog.Name(type)}\n    {cost}";
                SetPriceIcon(buildPriceIcons[index], currency);
                buildButtons[index].interactable = deposit != null && deposit.CanAfford(currency, cost);
            }
            buildHint.text = $"CORE MATERIALS  Ore {ore}  |  Core Shards {shards}";

            if (upgradeTarget != null)
            {
                var cost = CoreBuildingCatalog.UpgradeCost(upgradeTarget.BuildingType, upgradeTarget.Level);
                var currency = CoreBuildingCatalog.UpgradeCurrency(upgradeTarget.BuildingType);
                upgradeTitle.text = $"{CoreBuildingCatalog.Name(upgradeTarget.BuildingType)}  LV.{upgradeTarget.Level}";
                upgradeALabel.text = $"Branch A\n    {cost}";
                upgradeBLabel.text = $"Branch B\n    {cost}";
                SetPriceIcon(upgradeAPriceIcon, currency);
                SetPriceIcon(upgradeBPriceIcon, currency);
                var canAfford = deposit != null && deposit.CanAfford(currency, cost);
                upgradeAButton.interactable = canAfford;
                upgradeBButton.interactable = canAfford;
            }

            if (coreTarget != null)
            {
                var cost = coreTarget.UpgradeCost;
                var currency = coreTarget.UpgradeCurrency;
                coreTitle.text = $"HEART CORE  LV.{coreTarget.Level}\nOre {ore}  |  Core Shards {shards}";
                coreGuardianLabel.text = $"Guardian Heart\n    {cost}";
                corePulseLabel.text = $"Pulse Heart\n    {cost}";
                SetPriceIcon(coreGuardianPriceIcon, currency);
                SetPriceIcon(corePulsePriceIcon, currency);
                var canAfford = coreTarget.CanUpgrade && coreTarget.CanAfford(currency, cost);
                coreGuardianButton.interactable = canAfford;
                corePulseButton.interactable = canAfford;
            }
        }

        private void CreatePriceIcons()
        {
            buildPriceIcons = new Image[buildLabels.Length];
            for (var index = 0; index < buildLabels.Length; index++)
                buildPriceIcons[index] = CreatePriceIcon(buildLabels[index]);
            upgradeAPriceIcon = CreatePriceIcon(upgradeALabel);
            upgradeBPriceIcon = CreatePriceIcon(upgradeBLabel);
            coreGuardianPriceIcon = CreatePriceIcon(coreGuardianLabel);
            corePulsePriceIcon = CreatePriceIcon(corePulseLabel);
        }

        private void ResolveCurrencyIconsFromScene()
        {
            if (orePriceIcon != null && coreShardsPriceIcon != null)
                return;
            foreach (var image in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (orePriceIcon == null && image.name == "OreIcon")
                    orePriceIcon = image.sprite;
                else if (coreShardsPriceIcon == null && image.name == "CoreShardsIcon")
                    coreShardsPriceIcon = image.sprite;
                if (orePriceIcon != null && coreShardsPriceIcon != null)
                    return;
            }
        }

        private static Image CreatePriceIcon(Text label)
        {
            if (label == null)
                return null;
            var existing = label.transform.Find("PriceCurrencyIcon");
            if (existing != null)
                return existing.GetComponent<Image>();

            var iconObject = new GameObject("PriceCurrencyIcon", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)iconObject.transform;
            rect.SetParent(label.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(18f, 18f);
            rect.anchoredPosition = new Vector2(-20f, -label.rectTransform.rect.height * 0.2f);
            var image = iconObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private void SetPriceIcon(Image target, MinedResourceKind currency)
        {
            if (target == null)
                return;
            target.sprite = currency == MinedResourceKind.Ore ? orePriceIcon : coreShardsPriceIcon;
            target.enabled = target.sprite != null;
        }

        private void CloseAll()
        {
            buildRoot.gameObject.SetActive(false);
            upgradeRoot.gameObject.SetActive(false);
            corePopup.gameObject.SetActive(false);
            upgradeTarget = null;
            coreTarget = null;
        }

        private static Vector2 ClampToScreen(Vector2 position, float margin)
        {
            return new Vector2(Mathf.Clamp(position.x, margin, Screen.width - margin),
                Mathf.Clamp(position.y, margin, Screen.height - margin));
        }
    }
}
