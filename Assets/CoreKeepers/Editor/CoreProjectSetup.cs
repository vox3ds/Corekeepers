#if UNITY_EDITOR
using System.IO;
using CoreKeepers;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CoreKeepers.Editor
{
    public static class CoreProjectSetup
    {
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string DebugScenePath = "Assets/Scenes/DebugScene.unity";
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const string WarriorModelPath = "Assets/Heros/Warrior/Warrior.fbx";
        private const string WarriorPrefabPath = "Assets/CoreKeepers/Resources/CoreWarrior.prefab";
        private const string MaterialDirectory = "Assets/CoreKeepers/Materials";
        private const string UiDirectory = "Assets/CoreKeepers/UI";
        private const string MenuBackgroundPath = "Assets/CoreKeepers/UI/MainMenuBackground.png";
        private const string BuildingPrefabDirectory = "Assets/CoreKeepers/Resources/Buildings";
        private const string CoreShardsPrefabPath = "Assets/CoreKeepers/Resources/CoreShards.prefab";
        private const string OrePrefabPath = "Assets/CoreKeepers/Resources/Ore.prefab";
        private const string ResourceVisualVersion = "Visual_v4";
        private const float ResourceMaximumFootprint = 1.1f;
        private const float ResourceMaximumHeight = 0.775f;

        [InitializeOnLoadMethod]
        private static void ConfigureAfterFirstImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                    File.Exists(WarriorPrefabPath))
                    return;
                ConfigureProject();
            };
        }

        [InitializeOnLoadMethod]
        private static void UpgradeDebugResourceNodesAfterImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                    !File.Exists(CoreShardsPrefabPath) || !File.Exists(OrePrefabPath))
                    return;
                RebuildDebugResourceNodes();
            };
        }

        [MenuItem("CoreKeepers/Configure First Network Slice")]
        public static void ConfigureProject()
        {
            Directory.CreateDirectory("Assets/CoreKeepers/Resources");
            Directory.CreateDirectory(MaterialDirectory);
            Directory.CreateDirectory(UiDirectory);
            Directory.CreateDirectory(BuildingPrefabDirectory);

            PrepareMenuBackground();
            var warriorPrefab = CreateWarriorPrefab();
            var buildingPrefabs = CreateBuildingPrefabs();
            ConfigureMenuScene(warriorPrefab, buildingPrefabs);
            ConfigureDebugScene(warriorPrefab);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CoreKeepers first network slice configured successfully.");
        }

        [MenuItem("CoreKeepers/Validate First Network Slice")]
        public static void ValidateProject()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorPrefabPath);
            Require(prefab != null, "CoreWarrior prefab is missing.");
            Require(prefab.GetComponent<NetworkObject>() != null, "CoreWarrior requires NetworkObject.");
            Require(prefab.GetComponent<OwnerNetworkTransform>() != null, "CoreWarrior requires OwnerNetworkTransform.");
            Require(prefab.GetComponent<NetworkWarrior>() != null, "CoreWarrior requires NetworkWarrior.");
            Require(prefab.GetComponent<NavMeshAgent>() != null, "CoreWarrior requires NavMeshAgent.");
            Require(FindDeepChild(prefab.transform, "Head") != null, "CoreWarrior Head is missing.");
            Require(FindDeepChild(prefab.transform, "LHand") != null, "CoreWarrior LHand is missing.");
            Require(FindDeepChild(prefab.transform, "RHand") != null, "CoreWarrior RHand is missing.");

            EditorSceneManager.OpenScene(MenuScenePath);
            Require(GameObject.Find("Core Online Systems") != null, "Menu network systems are missing.");
            Require(GameObject.Find("Core Main Menu Canvas") != null, "Scene-authored Menu Canvas is missing.");
            Require(GameObject.Find("Core Main Menu Canvas").GetComponent<CoreMainMenuCanvas>() != null, "Menu Canvas controller is missing.");

            EditorSceneManager.OpenScene(DebugScenePath);
            Require(GameObject.Find("Heros") != null, "DebugScene Heros group was not preserved.");
            Require(GameObject.Find("Enemies") != null, "DebugScene Enemies group was not preserved.");
            Require(GameObject.Find("Maps") != null, "DebugScene Maps group was not preserved.");
            Require(GameObject.Find("Buildings") != null, "DebugScene Buildings group was not preserved.");
            Require(GameObject.Find("Core Debug Systems") != null, "Debug systems are missing.");
            Require(GameObject.Find("DebugCombatDummy") != null, "Combat dummy is missing.");
            Require(GameObject.Find("DebugCoreShardsNode") != null, "Core Shards node is missing.");
            Require(GameObject.Find("DebugOreNode") != null, "Ore node is missing.");
            Require(GameObject.Find("DebugCoreDeposit") != null, "Deposit core is missing.");
            Require(GameObject.Find("Core Gameplay Canvas") != null, "Gameplay radial Canvas is missing.");
            Require(GameObject.Find("Core Navigation")?.GetComponent<NavMeshSurface>()?.navMeshData != null, "Baked NavMesh is missing.");
            foreach (CoreBuildingType type in System.Enum.GetValues(typeof(CoreBuildingType)))
                Require(AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildingPrefabDirectory}/{type}.prefab") != null,
                    $"Building prefab {type} is missing.");

            Require(EditorBuildSettings.scenes.Length >= 2 &&
                    EditorBuildSettings.scenes[0].path == MenuScenePath &&
                    EditorBuildSettings.scenes[1].path == DebugScenePath,
                "Build Settings do not start with Menu and DebugScene.");
            Debug.Log("CoreKeepers network slice validation passed.");
        }

        private static GameObject CreateWarriorPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorModelPath);
            if (model == null)
                throw new FileNotFoundException("Warrior model is missing.", WarriorModelPath);

            var root = new GameObject("CoreWarrior");
            root.AddComponent<NetworkObject>();
            var controller = root.AddComponent<CapsuleCollider>();
            controller.radius = 0.55f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.52f;
            agent.height = 1.8f;
            agent.speed = 6f;
            agent.acceleration = 28f;
            agent.angularSpeed = 720f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            root.AddComponent<OwnerNetworkTransform>();
            root.AddComponent<NetworkWarrior>();
            root.AddComponent<WarriorProceduralAnimator>();

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "WarriorVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            var rightHand = FindDeepChild(visual.transform, "RHand");
            var leftHand = FindDeepChild(visual.transform, "LHand");
            if (rightHand == null || leftHand == null || FindDeepChild(visual.transform, "Head") == null)
                throw new MissingReferenceException("Warrior.fbx must contain Head, LHand and RHand transforms.");

            var metal = CreateMaterial("ToolMetal.mat", new Color(0.62f, 0.68f, 0.74f), 0.75f, 0.7f);
            var wood = CreateMaterial("ToolWood.mat", new Color(0.38f, 0.16f, 0.06f), 0.05f, 0.3f);
            CreateSword(rightHand, metal, wood);
            CreateHammer(rightHand, metal, wood);
            CreatePickaxe(rightHand, metal, wood);
            CreateShield(leftHand, metal, wood);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, WarriorPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void PrepareMenuBackground()
        {
            AssetDatabase.ImportAsset(MenuBackgroundPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(MenuBackgroundPath) is not TextureImporter importer)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        private static GameObject[] CreateBuildingPrefabs()
        {
            var result = new GameObject[5];
            var foundationMaterial = CreateMaterial("BuildingFoundation.mat", new Color(0.55f, 0.48f, 0.38f), 0.05f, 0.25f);
            var stone = CreateMaterial("BuildingStone.mat", new Color(0.75f, 0.72f, 0.64f), 0.1f, 0.35f);
            var gold = CreateMaterial("BuildingGold.mat", new Color(0.9f, 0.58f, 0.1f), 0.7f, 0.7f);
            var ruby = CreateMaterial("BuildingRuby.mat", new Color(0.85f, 0.04f, 0.12f), 0.25f, 0.75f);

            for (var index = 0; index < result.Length; index++)
            {
                var type = (CoreBuildingType)index;
                var root = new GameObject(type.ToString());
                root.AddComponent<NetworkObject>();
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.5f, 0f);
                collider.size = type == CoreBuildingType.Barricade ? new Vector3(2.2f, 1f, 0.65f) : new Vector3(1.5f, 1.2f, 1.5f);
                var obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = collider.center;
                obstacle.size = collider.size;
                obstacle.carving = true;

                var foundation = new GameObject("Foundation");
                foundation.transform.SetParent(root.transform, false);
                AddPrimitive(foundation.transform, "FoundationBlock", PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f),
                    type == CoreBuildingType.Barricade ? new Vector3(2.1f, 0.35f, 0.6f) : new Vector3(1.45f, 0.35f, 1.45f), foundationMaterial);
                var completed = new GameObject("CompletedVisual");
                completed.transform.SetParent(root.transform, false);
                BuildCompletedVisual(type, completed.transform, stone, gold, ruby);
                var building = root.AddComponent<CoreBuilding>();
                building.Configure(type, foundation, completed);
                completed.SetActive(false);

                var path = $"{BuildingPrefabDirectory}/{type}.prefab";
                result[index] = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            return result;
        }

        private static void BuildCompletedVisual(CoreBuildingType type, Transform root, Material stone, Material gold, Material ruby)
        {
            switch (type)
            {
                case CoreBuildingType.SmallTower:
                    AddPrimitive(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 0f), new Vector3(0.7f, 0.35f, 0.7f), stone);
                    AddPrimitive(root, "Crystal", PrimitiveType.Sphere, new Vector3(0f, 1.15f, 0f), Vector3.one * 0.42f, ruby);
                    break;
                case CoreBuildingType.HeavyTower:
                    AddPrimitive(root, "HeavyBase", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.35f, 0.9f, 1.35f), stone);
                    AddPrimitive(root, "Cannon", PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0.35f), new Vector3(0.28f, 0.85f, 0.28f), gold).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case CoreBuildingType.Barricade:
                    AddPrimitive(root, "Wall", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 1.5f, 0.55f), stone);
                    AddPrimitive(root, "Brace", PrimitiveType.Cube, new Vector3(0f, 0.8f, -0.34f), new Vector3(1.6f, 0.16f, 0.16f), gold);
                    break;
                case CoreBuildingType.TrapPlate:
                    AddPrimitive(root, "Plate", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.12f, 0.9f), gold);
                    for (var i = -1; i <= 1; i++)
                        AddPrimitive(root, $"Spike{i}", PrimitiveType.Cylinder, new Vector3(i * 0.38f, 0.45f, 0f), new Vector3(0.1f, 0.38f, 0.1f), ruby);
                    break;
                case CoreBuildingType.SupportPylon:
                    AddPrimitive(root, "Pylon", PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.45f, 0.75f, 0.45f), stone);
                    AddPrimitive(root, "Orb", PrimitiveType.Sphere, new Vector3(0f, 1.65f, 0f), Vector3.one * 0.48f, ruby);
                    AddPrimitive(root, "Halo", PrimitiveType.Cylinder, new Vector3(0f, 1.65f, 0f), new Vector3(0.72f, 0.06f, 0.72f), gold).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
            }
        }

        private static void ConfigureMenuScene(GameObject warriorPrefab, GameObject[] buildingPrefabs)
        {
            var scene = EditorSceneManager.OpenScene(MenuScenePath);
            DestroyNamedRoot("Core Online Systems");
            DestroyNamedRoot("Core Main Menu UI");
            DestroyNamedRoot("Core Main Menu Canvas");
            DestroyNamedRoot("EventSystem");

            var systems = new GameObject("Core Online Systems");
            var transport = systems.AddComponent<UnityTransport>();
            var manager = systems.AddComponent<NetworkManager>();
            systems.AddComponent<CoreSessionManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = warriorPrefab;
            manager.NetworkConfig.EnableSceneManagement = true;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = warriorPrefab });
            foreach (var prefab in buildingPrefabs)
                manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });

            CreateMainMenuCanvas();
            CreateEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateMainMenuCanvas()
        {
            var canvasObject = new GameObject("Core Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreateUiImage(canvasObject.transform, "Background", Color.white);
            Stretch(background.rectTransform);
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuBackgroundPath);
            background.preserveAspect = false;

            var darkOverlay = CreateUiImage(canvasObject.transform, "Readability Overlay", new Color(0.01f, 0.015f, 0.02f, 0.18f));
            Stretch(darkOverlay.rectTransform);
            var parchment = new Color(0.92f, 0.84f, 0.68f, 0.96f);
            var dark = new Color(0.025f, 0.035f, 0.045f, 0.96f);
            var gold = new Color(0.88f, 0.57f, 0.16f, 1f);
            var red = new Color(0.55f, 0.055f, 0.055f, 1f);

            var leftPanel = CreateUiImage(canvasObject.transform, "Main Navigation Panel", parchment);
            SetRect(leftPanel.rectTransform, new Vector2(20f, -30f), new Vector2(500f, 1010f));
            CreateUiText(leftPanel.transform, "Game Title", "COREKEEPERS", 48, new Color(0.16f, 0.08f, 0.025f),
                new Vector2(250f, -115f), new Vector2(450f, 100f), FontStyle.Bold);
            CreateUiText(leftPanel.transform, "Subtitle", "DEFEND THE LIVING HEART", 18, new Color(0.45f, 0.18f, 0.05f),
                new Vector2(250f, -180f), new Vector2(430f, 42f), FontStyle.Bold);

            var campaign = CreateUiButton(leftPanel.transform, "Campaign Button", "Campaign", new Vector2(250f, -310f), new Vector2(400f, 78f), red, Color.white, 30);
            var pvp = CreateUiButton(leftPanel.transform, "PvP Button", "PvP Arena   • Coming Soon", new Vector2(250f, -410f), new Vector2(400f, 72f), parchment * 0.92f, new Color(0.16f, 0.08f, 0.025f), 25);
            var options = CreateUiButton(leftPanel.transform, "Options Button", "Options", new Vector2(250f, -505f), new Vector2(400f, 72f), parchment * 0.92f, new Color(0.16f, 0.08f, 0.025f), 25);
            var debug = CreateUiButton(leftPanel.transform, "Debug Button", "</>  Debug Scene", new Vector2(250f, -600f), new Vector2(400f, 72f), parchment * 0.92f, new Color(0.16f, 0.08f, 0.025f), 24);
            var quit = CreateUiButton(leftPanel.transform, "Quit Button", "Quit", new Vector2(250f, -695f), new Vector2(400f, 72f), parchment * 0.92f, new Color(0.16f, 0.08f, 0.025f), 25);
            CreateUiText(leftPanel.transform, "Gem Ornament", "◆", 72, red, new Vector2(250f, -870f), new Vector2(160f, 100f), FontStyle.Bold);

            var profile = CreateUiImage(canvasObject.transform, "Nickname Panel", dark);
            SetRect(profile.rectTransform, new Vector2(1320f, -30f), new Vector2(570f, 125f));
            CreateUiText(profile.transform, "Nickname Header", "NICKNAME", 16, gold, new Vector2(160f, -34f), new Vector2(260f, 30f), FontStyle.Bold);
            var nicknameText = CreateUiText(profile.transform, "Nickname Value", "Player", 30, Color.white, new Vector2(190f, -78f), new Vector2(320f, 45f), FontStyle.Normal);
            var editNickname = CreateUiButton(profile.transform, "Edit Nickname", "✎", new Vector2(515f, -64f), new Vector2(58f, 58f), new Color(0.2f, 0.16f, 0.1f, 1f), gold, 28);

            var together = CreateUiImage(canvasObject.transform, "Play Together Panel", dark);
            SetRect(together.rectTransform, new Vector2(1290f, -260f), new Vector2(600f, 520f));
            CreateUiText(together.transform, "Play Header", "PLAY TOGETHER", 21, gold, new Vector2(300f, -42f), new Vector2(500f, 40f), FontStyle.Bold);
            var host = CreateUiButton(together.transform, "Host Online", "◉  Host Online Game", new Vector2(300f, -112f), new Vector2(500f, 62f), new Color(0.16f, 0.13f, 0.09f, 1f), Color.white, 21);
            CreateUiText(together.transform, "Code Header", "ENTER CODE", 16, Color.white, new Vector2(120f, -175f), new Vector2(180f, 32f), FontStyle.Bold);
            var joinInput = CreateUiInput(together.transform, "Join Code Input", "Enter Code", new Vector2(190f, -225f), new Vector2(300f, 52f), dark, Color.white);
            var join = CreateUiButton(together.transform, "Join Button", "JOIN", new Vector2(455f, -225f), new Vector2(120f, 52f), new Color(0.22f, 0.16f, 0.08f, 1f), gold, 19);
            var openToggle = CreateUiToggle(together.transform, "Open Game Toggle", "Open Game — allow others to find and join", new Vector2(300f, -290f), new Vector2(500f, 48f), gold);
            var refresh = CreateUiButton(together.transform, "Refresh Games", "Refresh Open Games", new Vector2(300f, -350f), new Vector2(500f, 44f), new Color(0.12f, 0.12f, 0.11f, 1f), gold, 16);

            var rows = new GameObject[4];
            var rowLabels = new Text[4];
            var rowButtons = new Button[4];
            for (var index = 0; index < rows.Length; index++)
            {
                var row = CreateUiButton(together.transform, $"Open Game {index + 1}", "Open Game", new Vector2(300f, -405f - index * 45f), new Vector2(500f, 40f), new Color(0.08f, 0.09f, 0.09f, 1f), Color.white, 14);
                rows[index] = row.gameObject;
                rowButtons[index] = row;
                rowLabels[index] = row.GetComponentInChildren<Text>();
            }

            var status = CreateUiText(canvasObject.transform, "Status", "Starting...", 17, Color.white,
                new Vector2(550f, -1025f), new Vector2(820f, 38f), FontStyle.Bold);
            status.alignment = TextAnchor.MiddleCenter;

            var nicknameModal = CreateModal(canvasObject.transform, "Nickname Modal", new Vector2(520f, 300f), dark, out var nicknameModalContent);
            CreateUiText(nicknameModalContent, "Title", "CHOOSE A NICKNAME", 28, gold, new Vector2(260f, -55f), new Vector2(450f, 50f), FontStyle.Bold);
            var nicknameInput = CreateUiInput(nicknameModalContent, "Nickname Input", "Nickname", new Vector2(260f, -135f), new Vector2(410f, 58f), new Color(0.08f, 0.08f, 0.07f, 1f), Color.white);
            var nicknameConfirm = CreateUiButton(nicknameModalContent, "Confirm", "CONFIRM", new Vector2(260f, -220f), new Vector2(250f, 55f), red, Color.white, 20);

            var optionsPanel = CreateModal(canvasObject.transform, "Options Panel", new Vector2(620f, 540f), dark, out var optionsContent);
            CreateUiText(optionsContent, "Title", "OPTIONS", 30, gold, new Vector2(310f, -45f), new Vector2(500f, 50f), FontStyle.Bold);
            var master = CreateLabeledSlider(optionsContent, "Master", "Master Volume", -130f, gold);
            var music = CreateLabeledSlider(optionsContent, "Music", "Music Volume", -225f, gold);
            var sfx = CreateLabeledSlider(optionsContent, "SFX", "SFX Volume", -320f, gold);
            var optionsSave = CreateUiButton(optionsContent, "Save", "SAVE", new Vector2(220f, -445f), new Vector2(190f, 55f), red, Color.white, 20);
            var optionsBack = CreateUiButton(optionsContent, "Back", "BACK", new Vector2(430f, -445f), new Vector2(150f, 55f), new Color(0.18f, 0.16f, 0.12f, 1f), Color.white, 18);

            var comingPanel = CreateModal(canvasObject.transform, "Coming Soon Panel", new Vector2(500f, 260f), dark, out var comingContent);
            CreateUiText(comingContent, "Title", "PvP ARENA\nCOMING SOON", 34, gold, new Vector2(250f, -90f), new Vector2(430f, 120f), FontStyle.Bold);
            var comingBack = CreateUiButton(comingContent, "Back", "BACK", new Vector2(250f, -205f), new Vector2(180f, 52f), red, Color.white, 18);

            var lobbyPanel = CreateModal(canvasObject.transform, "Lobby Panel", new Vector2(620f, 600f), dark, out var lobbyContent);
            CreateUiText(lobbyContent, "Title", "CAMPAIGN LOBBY", 30, gold, new Vector2(310f, -42f), new Vector2(520f, 45f), FontStyle.Bold);
            var lobbyCode = CreateUiText(lobbyContent, "Join Code", "JOIN CODE", 34, Color.white, new Vector2(310f, -130f), new Vector2(500f, 100f), FontStyle.Bold);
            var lobbyPlayers = CreateUiText(lobbyContent, "Players", "PLAYERS", 19, Color.white, new Vector2(310f, -265f), new Vector2(500f, 170f), FontStyle.Normal);
            var lobbyMode = CreateUiText(lobbyContent, "Mode", "PRIVATE / CODE ONLY", 17, gold, new Vector2(310f, -375f), new Vector2(480f, 42f), FontStyle.Bold);
            var lobbyStart = CreateUiButton(lobbyContent, "Start", "START GAME", new Vector2(215f, -495f), new Vector2(240f, 62f), red, Color.white, 21);
            var lobbyBack = CreateUiButton(lobbyContent, "Back", "CLOSE LOBBY", new Vector2(445f, -495f), new Vector2(180f, 62f), new Color(0.16f, 0.14f, 0.11f, 1f), Color.white, 17);

            nicknameModal.SetActive(false); optionsPanel.SetActive(false); comingPanel.SetActive(false); lobbyPanel.SetActive(false);
            var controller = canvasObject.AddComponent<CoreMainMenuCanvas>();
            var serialized = new SerializedObject(controller);
            SetReference(serialized, "nicknameText", nicknameText); SetReference(serialized, "statusText", status);
            SetReference(serialized, "campaignButton", campaign); SetReference(serialized, "hostButton", host);
            SetReference(serialized, "pvpButton", pvp); SetReference(serialized, "optionsButton", options);
            SetReference(serialized, "debugButton", debug); SetReference(serialized, "quitButton", quit);
            SetReference(serialized, "editNicknameButton", editNickname); SetReference(serialized, "joinCodeInput", joinInput);
            SetReference(serialized, "joinButton", join); SetReference(serialized, "refreshButton", refresh);
            SetReference(serialized, "openGameToggle", openToggle);
            SetObjectArray(serialized, "openGameRows", rows); SetObjectArray(serialized, "openGameLabels", rowLabels); SetObjectArray(serialized, "openGameButtons", rowButtons);
            SetReference(serialized, "nicknameModal", nicknameModal); SetReference(serialized, "nicknameInput", nicknameInput); SetReference(serialized, "nicknameConfirmButton", nicknameConfirm);
            SetReference(serialized, "optionsPanel", optionsPanel); SetReference(serialized, "masterSlider", master); SetReference(serialized, "musicSlider", music); SetReference(serialized, "sfxSlider", sfx);
            SetReference(serialized, "optionsSaveButton", optionsSave); SetReference(serialized, "optionsBackButton", optionsBack);
            SetReference(serialized, "comingSoonPanel", comingPanel); SetReference(serialized, "comingSoonBackButton", comingBack);
            SetReference(serialized, "lobbyPanel", lobbyPanel); SetReference(serialized, "lobbyCodeText", lobbyCode); SetReference(serialized, "lobbyPlayersText", lobbyPlayers);
            SetReference(serialized, "lobbyModeText", lobbyMode); SetReference(serialized, "lobbyStartButton", lobbyStart); SetReference(serialized, "lobbyBackButton", lobbyBack);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDebugScene(GameObject warriorPrefab)
        {
            var scene = EditorSceneManager.OpenScene(DebugScenePath);
            RemoveDirectWarrior();
            DestroyNamedRoot("Core Debug Systems");
            DestroyNamedRoot("DebugCombatDummy");
            DestroyNamedRoot("DebugResourceNode");
            DestroyNamedRoot("DebugCoreShardsNode");
            DestroyNamedRoot("DebugOreNode");
            DestroyNamedRoot("DebugCoreDeposit");
            DestroyNamedRoot("Core Gameplay Canvas");
            DestroyNamedRoot("Core Navigation");
            DestroyNamedRoot("EventSystem");

            var systems = new GameObject("Core Debug Systems");
            var bootstrap = systems.AddComponent<CoreDebugSceneBootstrap>();
            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("playerPrefab").objectReferenceValue = warriorPrefab;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
            systems.AddComponent<CoreDebugHud>();

            var mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.GetComponent<CoreCameraFollow>() == null)
                mainCamera.gameObject.AddComponent<CoreCameraFollow>();

            CreateCombatDummy();
            CreateResourceNodes();
            CreateDepositCore();
            CreateGameplayCanvas();
            CreateEventSystem();
            CreateAndBakeNavigation();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void RemoveDirectWarrior()
        {
            var heroes = GameObject.Find("Heros");
            if (heroes == null)
                return;
            for (var index = heroes.transform.childCount - 1; index >= 0; index--)
            {
                var child = heroes.transform.GetChild(index).gameObject;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(child);
                var sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
                if (child.name == "Warrior" || sourcePath == WarriorModelPath)
                {
                    Object.DestroyImmediate(child);
                    Debug.Log("Removed direct scene Warrior; the network player prefab now owns Warrior spawning.");
                }
            }
        }

        private static void CreateCombatDummy()
        {
            var material = CreateMaterial("DebugDummy.mat", new Color(0.9f, 0.25f, 0.12f), 0.05f, 0.3f);
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "DebugCombatDummy";
            root.transform.position = new Vector3(4f, 1f, 0f);
            root.GetComponent<Renderer>().sharedMaterial = material;
            root.AddComponent<NetworkObject>();
            root.AddComponent<CoreDebugDummy>();
        }

        [MenuItem("CoreKeepers/Rebuild Debug Resource Nodes")]
        public static void RebuildDebugResourceNodes()
        {
            var scene = SceneManager.GetSceneByPath(DebugScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(DebugScenePath, OpenSceneMode.Additive);

            var shardsRoot = FindRoot(scene, "DebugCoreShardsNode");
            var oreRoot = FindRoot(scene, "DebugOreNode");
            if (shardsRoot != null && oreRoot != null &&
                shardsRoot.transform.Find($"CoreShards{ResourceVisualVersion}") != null &&
                oreRoot.transform.Find($"Ore{ResourceVisualVersion}") != null)
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var previousActiveScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            DestroySceneRoot(scene, "DebugResourceNode");
            DestroySceneRoot(scene, "DebugCoreShardsNode");
            DestroySceneRoot(scene, "DebugOreNode");
            CreateResourceNodes();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
            Debug.Log("Debug resource nodes rebuilt from CoreShards and Ore prefabs.");
        }

        private static void CreateResourceNodes()
        {
            CreateResourceNode("DebugCoreShardsNode", CoreShardsPrefabPath, new Vector3(-4f, 0f, -1.8f),
                MinedResourceKind.CoreShards);
            CreateResourceNode("DebugOreNode", OrePrefabPath, new Vector3(-4f, 0f, 2f), MinedResourceKind.Ore);
        }

        private static void CreateResourceNode(string name, string prefabPath, Vector3 position, MinedResourceKind kind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new FileNotFoundException($"Resource prefab is missing: {prefabPath}", prefabPath);

            var root = new GameObject(name);
            root.transform.position = position;
            root.AddComponent<NetworkObject>();
            var node = root.AddComponent<CoreDebugResourceNode>();
            var serializedNode = new SerializedObject(node);
            serializedNode.FindProperty("resourceKind").enumValueIndex = (int)kind;
            serializedNode.ApplyModifiedPropertiesWithoutUndo();

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            visual.name = $"{kind}{ResourceVisualVersion}";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = prefab.transform.localRotation;
            visual.transform.localScale = prefab.transform.localScale;

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(root.transform.position + Vector3.up * 0.5f, Vector3.one);
            if (renderers.Length > 0)
            {
                bounds = CalculateBounds(renderers);
                var horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
                var footprintScale = ResourceMaximumFootprint / Mathf.Max(horizontalSize, 0.01f);
                var heightScale = ResourceMaximumHeight / Mathf.Max(bounds.size.y, 0.01f);
                visual.transform.localScale *= Mathf.Min(footprintScale, heightScale);
                bounds = CalculateBounds(renderers);
                visual.transform.position += Vector3.up * (root.transform.position.y - bounds.min.y);
                bounds = CalculateBounds(renderers);
            }

            foreach (var visualCollider in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(visualCollider);
            var collider = root.AddComponent<SphereCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = collider.center;
            obstacle.radius = collider.radius;
            obstacle.height = Mathf.Max(collider.radius * 2f, bounds.size.y);
            obstacle.carving = true;
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name)
                    return root;
            return null;
        }

        private static void DestroySceneRoot(Scene scene, string name)
        {
            var root = FindRoot(scene, name);
            if (root != null)
                Object.DestroyImmediate(root);
        }

        private static void CreateDepositCore()
        {
            var ruby = CreateMaterial("DebugCore.mat", new Color(0.95f, 0.08f, 0.22f), 0.2f, 0.8f);
            ruby.EnableKeyword("_EMISSION");
            ruby.SetColor("_EmissionColor", new Color(1.5f, 0.08f, 0.25f));
            var gold = CreateMaterial("DebugCoreGold.mat", new Color(0.9f, 0.62f, 0.12f), 0.7f, 0.7f);
            var root = new GameObject("DebugCoreDeposit");
            root.transform.position = new Vector3(0f, 0.8f, 4f);
            root.AddComponent<NetworkObject>();
            root.AddComponent<CoreDebugDeposit>();
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 1.45f;
            AddPrimitive(root.transform, "HeartPlaceholder", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.85f, 1f, 0.7f), ruby);
            var ring = AddPrimitive(root.transform, "ContainmentRing", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.35f, 0.08f, 1.35f), gold);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void CreateGameplayCanvas()
        {
            var canvasObject = new GameObject("Core Gameplay Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var dark = new Color(0.025f, 0.035f, 0.04f, 0.96f);
            var gold = new Color(0.9f, 0.58f, 0.14f, 1f);
            var red = new Color(0.58f, 0.06f, 0.06f, 1f);

            var buildImage = CreateUiImage(canvasObject.transform, "Build Radial", new Color(0.015f, 0.02f, 0.025f, 0.9f));
            SetCenteredRect(buildImage.rectTransform, new Vector2(440f, 440f));
            var buildHint = CreateUiText(buildImage.transform, "Resource", "TEAM RESOURCE", 18, gold,
                new Vector2(70f, -188f), new Vector2(300f, 45f), FontStyle.Bold);
            var positions = new[] { new Vector2(145f, -28f), new Vector2(285f, -82f), new Vector2(310f, -240f),
                new Vector2(60f, -240f), new Vector2(35f, -82f) };
            var buildButtons = new Button[5];
            var buildLabels = new Text[5];
            for (var index = 0; index < 5; index++)
            {
                buildButtons[index] = CreateUiButton(buildImage.transform, $"Build {(CoreBuildingType)index}",
                    CoreBuildingCatalog.Name((CoreBuildingType)index), positions[index], new Vector2(150f, 74f),
                    index == 0 ? red : dark, Color.white, 15);
                buildLabels[index] = buildButtons[index].GetComponentInChildren<Text>();
            }

            var upgradeImage = CreateUiImage(canvasObject.transform, "Upgrade Radial", new Color(0.015f, 0.02f, 0.025f, 0.94f));
            SetCenteredRect(upgradeImage.rectTransform, new Vector2(400f, 330f));
            var upgradeTitle = CreateUiText(upgradeImage.transform, "Title", "BUILDING UPGRADE", 21, gold,
                new Vector2(45f, -35f), new Vector2(310f, 45f), FontStyle.Bold);
            var upgradeA = CreateUiButton(upgradeImage.transform, "Upgrade A", "Branch A", new Vector2(30f, -125f), new Vector2(160f, 95f), red, Color.white, 16);
            var upgradeB = CreateUiButton(upgradeImage.transform, "Upgrade B", "Branch B", new Vector2(210f, -125f), new Vector2(160f, 95f), dark, Color.white, 16);

            var coreImage = CreateUiImage(canvasObject.transform, "Core Upgrade Popup", new Color(0.015f, 0.02f, 0.025f, 0.97f));
            SetCenteredRect(coreImage.rectTransform, new Vector2(520f, 360f));
            var coreTitle = CreateUiText(coreImage.transform, "Title", "HEART CORE", 24, gold,
                new Vector2(40f, -35f), new Vector2(440f, 80f), FontStyle.Bold);
            var guardian = CreateUiButton(coreImage.transform, "Guardian", "Guardian Heart", new Vector2(40f, -150f), new Vector2(205f, 120f), red, Color.white, 17);
            var pulse = CreateUiButton(coreImage.transform, "Pulse", "Pulse Heart", new Vector2(275f, -150f), new Vector2(205f, 120f), dark, Color.white, 17);

            var menu = canvasObject.AddComponent<CoreRadialMenu>();
            var serialized = new SerializedObject(menu);
            SetReference(serialized, "buildRoot", buildImage.rectTransform); SetObjectArray(serialized, "buildButtons", buildButtons);
            SetObjectArray(serialized, "buildLabels", buildLabels); SetReference(serialized, "buildHint", buildHint);
            SetReference(serialized, "upgradeRoot", upgradeImage.rectTransform); SetReference(serialized, "upgradeTitle", upgradeTitle);
            SetReference(serialized, "upgradeAButton", upgradeA); SetReference(serialized, "upgradeBButton", upgradeB);
            SetReference(serialized, "upgradeALabel", upgradeA.GetComponentInChildren<Text>()); SetReference(serialized, "upgradeBLabel", upgradeB.GetComponentInChildren<Text>());
            SetReference(serialized, "corePopup", coreImage.rectTransform); SetReference(serialized, "coreTitle", coreTitle);
            SetReference(serialized, "coreGuardianButton", guardian); SetReference(serialized, "corePulseButton", pulse);
            SetReference(serialized, "coreGuardianLabel", guardian.GetComponentInChildren<Text>()); SetReference(serialized, "corePulseLabel", pulse.GetComponentInChildren<Text>());
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateAndBakeNavigation()
        {
            var navigation = new GameObject("Core Navigation");
            var surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
        }

        private static void CreateSword(Transform hand, Material metal, Material wood)
        {
            var root = new GameObject("SwordPlaceholder");
            root.transform.SetParent(hand, false);
            AddPrimitive(root.transform, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(0.11f, 1.25f, 0.05f), metal);
            AddPrimitive(root.transform, "Grip", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f), new Vector3(0.1f, 0.35f, 0.1f), wood);
            var tip = new GameObject("Tip");
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            root.SetActive(false);
        }

        private static void CreateHammer(Transform hand, Material metal, Material wood)
        {
            var root = new GameObject("HammerPlaceholder");
            root.transform.SetParent(hand, false);
            AddPrimitive(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.08f, 0.65f, 0.08f), wood);
            AddPrimitive(root.transform, "HammerHead", PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(0.55f, 0.22f, 0.28f), metal);
            root.SetActive(false);
        }

        private static void CreatePickaxe(Transform hand, Material metal, Material wood)
        {
            var root = new GameObject("PickaxePlaceholder");
            root.transform.SetParent(hand, false);
            AddPrimitive(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.07f, 0.72f, 0.07f), wood);
            var head = AddPrimitive(root.transform, "PickHead", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.78f, 0.12f, 0.12f), metal);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            root.SetActive(false);
        }

        private static void CreateShield(Transform hand, Material metal, Material wood)
        {
            var shield = AddPrimitive(hand, "ShieldPlaceholder", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.12f, 0.55f), metal);
            shield.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            AddPrimitive(shield.transform, "ShieldBoss", PrimitiveType.Sphere, Vector3.up * 0.5f, Vector3.one * 0.22f, wood);
        }

        private static GameObject AddPrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            var collider = primitive.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            return primitive;
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == childName)
                    return child;
            return null;
        }

        private static Material CreateMaterial(string fileName, Color color, float metallic, float smoothness)
        {
            var path = $"{MaterialDirectory}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                existing.SetFloat("_Metallic", metallic);
                existing.SetFloat("_Smoothness", smoothness);
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Image CreateUiImage(Transform parent, string name, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateUiText(Transform parent, string name, string value, int fontSize, Color color,
            Vector2 position, Vector2 size, FontStyle style)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(text.rectTransform, position, size);
            return text;
        }

        private static Button CreateUiButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
            Color background, Color foreground, int fontSize)
        {
            var image = CreateUiImage(parent, name, background);
            SetRect(image.rectTransform, position, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.22f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
            colors.disabledColor = new Color(background.r * 0.45f, background.g * 0.45f, background.b * 0.45f, 0.7f);
            button.colors = colors;
            var text = CreateUiText(image.transform, "Label", label, fontSize, foreground, Vector2.zero, size, FontStyle.Bold);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(8f, 4f);
            text.rectTransform.offsetMax = new Vector2(-8f, -4f);
            text.raycastTarget = false;
            return button;
        }

        private static InputField CreateUiInput(Transform parent, string name, string placeholderValue, Vector2 position,
            Vector2 size, Color background, Color foreground)
        {
            var image = CreateUiImage(parent, name, background);
            SetRect(image.rectTransform, position, size);
            var input = image.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            var text = CreateUiText(image.transform, "Text", string.Empty, 19, foreground, Vector2.zero, size, FontStyle.Normal);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(14f, 4f);
            text.rectTransform.offsetMax = new Vector2(-14f, -4f);
            text.alignment = TextAnchor.MiddleLeft;
            var placeholder = CreateUiText(image.transform, "Placeholder", placeholderValue, 18, new Color(foreground.r, foreground.g, foreground.b, 0.45f),
                Vector2.zero, size, FontStyle.Italic);
            Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(14f, 4f);
            placeholder.rectTransform.offsetMax = new Vector2(-14f, -4f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 24;
            return input;
        }

        private static Toggle CreateUiToggle(Transform parent, string name, string label, Vector2 position, Vector2 size, Color accent)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), position, size);
            var background = CreateUiImage(root.transform, "Box", new Color(0.12f, 0.11f, 0.08f, 1f));
            SetRect(background.rectTransform, new Vector2(8f, -7f), new Vector2(34f, 34f));
            var check = CreateUiImage(background.transform, "Checkmark", accent);
            SetRect(check.rectTransform, new Vector2(17f, 17f), new Vector2(22f, 22f));
            var text = CreateUiText(root.transform, "Label", label, 16, Color.white, new Vector2(54f, 0f),
                new Vector2(size.x - 60f, size.y), FontStyle.Normal);
            text.alignment = TextAnchor.MiddleLeft;
            var toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            return toggle;
        }

        private static Slider CreateLabeledSlider(Transform parent, string name, string label, float y, Color accent)
        {
            CreateUiText(parent, $"{name} Label", label, 18, Color.white, new Vector2(310f, y), new Vector2(480f, 35f), FontStyle.Bold);
            var root = new GameObject($"{name} Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(310f, y - 42f), new Vector2(460f, 28f));
            var background = CreateUiImage(root.transform, "Background", new Color(0.12f, 0.12f, 0.11f, 1f));
            Stretch(background.rectTransform);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform)); fillArea.transform.SetParent(root.transform, false); Stretch(fillArea.GetComponent<RectTransform>());
            var fill = CreateUiImage(fillArea.transform, "Fill", accent); Stretch(fill.rectTransform);
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)); handleArea.transform.SetParent(root.transform, false); Stretch(handleArea.GetComponent<RectTransform>());
            var handle = CreateUiImage(handleArea.transform, "Handle", Color.white); SetRect(handle.rectTransform, new Vector2(14f, 14f), new Vector2(28f, 36f));
            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f; slider.maxValue = 1f;
            return slider;
        }

        private static GameObject CreateModal(Transform parent, string name, Vector2 size, Color color, out Transform content)
        {
            var overlay = CreateUiImage(parent, name, new Color(0f, 0f, 0f, 0.62f));
            Stretch(overlay.rectTransform);
            var panel = CreateUiImage(overlay.transform, "Panel", color);
            SetRect(panel.rectTransform, new Vector2(960f - size.x * 0.5f, -(540f - size.y * 0.5f)), size);
            content = panel.transform;
            return overlay.gameObject;
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().actionsAsset =
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetCenteredRect(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetReference(SerializedObject serialized, string propertyName, Object value)
        {
            serialized.FindProperty(propertyName).objectReferenceValue = value;
        }

        private static void SetObjectArray<T>(SerializedObject serialized, string propertyName, T[] values) where T : Object
        {
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void DestroyNamedRoot(string name)
        {
            var found = GameObject.Find(name);
            if (found != null && found.transform.parent == null)
                Object.DestroyImmediate(found);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(DebugScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new System.InvalidOperationException(message);
        }
    }
}
#endif
