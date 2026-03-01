using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

namespace SmartFarm.Editor
{
    /// <summary>
    /// One-click full setup for Smart Collaborative VR Agriculture Platform.
    /// Menu: Tools > Smart Farm > Full Setup
    /// </summary>
    public static class SmartFarmSetupEditor
    {
        private const string PrefabsPath = "Assets/SmartFarm/Prefabs";

        public static void FullSetup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SmartFarm] Stop Play mode first!");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SmartFarm] Open a scene first.");
                return;
            }

            EnsureDirectories();

            // 0. Create plant assets if missing
            PlantGrowth.Editor.PlantGrowthSetupWizard.CreatePlantAssetsIfMissing();

            // 1. Create or find PlantGrowthManager
            var plantManager = Object.FindFirstObjectByType<PlantGrowth.PlantGrowthManager>();
            if (plantManager == null)
            {
                var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantGrowthManager.prefab");
                if (managerPrefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
                    go.name = "PlantGrowthManager";
                    plantManager = go.GetComponent<PlantGrowth.PlantGrowthManager>();
                }
                else
                {
                    var go = new GameObject("PlantGrowthManager");
                    plantManager = go.AddComponent<PlantGrowth.PlantGrowthManager>();
                }
            }

            // 2. Create FarmSimulationHub
            var hub = CreateOrFindFarmSimulationHub(plantManager);
            if (hub == null)
            {
                Debug.LogError("[SmartFarm] Failed to create FarmSimulationHub.");
                return;
            }

            // 3. Create FarmDashboard with PollVotePanel
            GameObject dashboard;
            try
            {
                dashboard = CreateOrFindFarmDashboard(hub);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogError("[SmartFarm] Failed to create FarmDashboard. Try Tools > Farm > Create Farm Dashboard.");
                return;
            }
            if (dashboard == null)
            {
                Debug.LogError("[SmartFarm] Failed to create FarmDashboard.");
                return;
            }

            // 4. Ensure EventSystem has XR UI Input Module
            EnsureXRUIEventSystem();

            // 4b. Enable UI Interaction on XR Poke/Ray Interactors (required for buttons)
            EnableXRUIControllers();

            // 5. Add plants if none exist
            if (Object.FindObjectsByType<PlantGrowth.PlantController>(FindObjectsSortMode.None).Length == 0)
            {
                AddPlantInstances();
            }

            // 6. Create tablet app + page system
            CreateOrFindTabletApp(hub);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hub;

            if (!IsSceneInBuildSettings(scene.path))
            {
                AddSceneToBuildSettings(scene.path);
                Debug.Log("[SmartFarm] Scene added to Build Settings (required for networked scene objects).");
            }

            Debug.Log("[SmartFarm] Full setup complete! Hub, Dashboard, and Tablet App created. Press Play to test.");
        }

        public static void FullSetupWithTablet()
        {
            FullSetup();
        }

        public static void ApplyTabletThemeAuto()
        {
            var tablet = GameObject.Find("SmartFarmTablet");
            if (tablet == null)
            {
                Debug.LogWarning("[SmartFarm] SmartFarmTablet not found. Run Full Platform Setup first.");
                return;
            }
            var applier = tablet.GetComponent<TabletThemeAutoApplier>();
            if (applier == null) applier = tablet.AddComponent<TabletThemeAutoApplier>();
            SetPrivateField(applier, "themeProfile", EnsureDefaultTabletThemeAsset());
            applier.ApplyTheme();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SmartFarm] Tablet theme applied automatically.");
        }

        private static Camera FindCameraForDashboard()
        {
            if (Camera.main != null) return Camera.main;
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null) return cam;
            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null)
            {
                var c = mainCam.GetComponent<Camera>();
                if (c != null) return c;
            }
            var camOffset = GameObject.Find("Camera Offset");
            if (camOffset != null)
            {
                var c = camOffset.GetComponentInChildren<Camera>();
                if (c != null) return c;
            }
            return null;
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SmartFarm"))
                AssetDatabase.CreateFolder("Assets", "SmartFarm");
            if (!AssetDatabase.IsValidFolder("Assets/SmartFarm/Prefabs"))
                AssetDatabase.CreateFolder("Assets/SmartFarm", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/SmartFarmTablet"))
                AssetDatabase.CreateFolder("Assets/Resources", "SmartFarmTablet");
        }

        private static TabletThemeProfile EnsureDefaultTabletThemeAsset()
        {
            EnsureDirectories();
            const string path = "Assets/Resources/SmartFarmTablet/DefaultTabletTheme.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TabletThemeProfile>(path);
            if (existing != null) return existing;

            var theme = ScriptableObject.CreateInstance<TabletThemeProfile>();
            theme.primaryTextColor = Color.white;
            theme.secondaryTextColor = new Color(0.8f, 0.87f, 0.95f);
            theme.imageTint = Color.white;
            theme.buttonNormalColor = new Color(0.2f, 0.7f, 0.3f, 1f);
            theme.buttonHoverColor = new Color(0.3f, 0.85f, 0.45f, 1f);
            AssetDatabase.CreateAsset(theme, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return theme;
        }

        private static GameObject CreateOrFindFarmSimulationHub(PlantGrowth.PlantGrowthManager plantManager)
        {
            var existing = GameObject.Find("FarmSimulationHub");
            if (existing != null)
            {
                var mgr = existing.GetComponent<FarmSimulationManager>();
                if (mgr != null) SetPrivateField(mgr, "plantGrowthManager", plantManager);
                var existingTemp = existing.GetComponent<RealTemperatureService>();
                if (existingTemp == null) existingTemp = existing.AddComponent<RealTemperatureService>();
                if (mgr != null) SetPrivateField(existingTemp, "simulationManager", mgr);
                return existing;
            }

            var hub = new GameObject("FarmSimulationHub");
            hub.transform.position = Vector3.zero;

            // NetworkObject (required for Netcode)
            var netObj = hub.AddComponent<NetworkObject>();
            netObj.DontDestroyWithOwner = true;

            // FarmSimulationManager
            var simMgr = hub.AddComponent<FarmSimulationManager>();
            SetPrivateField(simMgr, "plantGrowthManager", plantManager);

            // FarmSimulationNetworkSync
            var netSync = hub.AddComponent<FarmSimulationNetworkSync>();
            SetPrivateField(simMgr, "networkSync", netSync);

            // PollVoteManager (on same hub for network)
            var pollMgr = hub.AddComponent<PollVoteManager>();

            // Real temperature service (disabled by default; configure API key in Inspector)
            var realTemp = hub.AddComponent<RealTemperatureService>();
            SetPrivateField(realTemp, "simulationManager", simMgr);

            return hub;
        }

        private static GameObject CreateOrFindFarmDashboard(GameObject hub)
        {
            var existing = GameObject.Find("FarmDashboard");
            if (existing != null)
            {
                LinkDashboardToHub(existing, hub);
                return existing;
            }

            // Canvas – World Space (3D object in scene, movable, XR-friendly)
            var canvasGO = new GameObject("FarmDashboard");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Farm Setup");

            var cam = FindCameraForDashboard();
            // World Space: position in 3D world, NOT parented to camera
            canvasGO.transform.position = new Vector3(0, 1.5f, 3f);
            canvasGO.transform.rotation = Quaternion.Euler(0, 180f, 0);  // face camera
            canvasGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);  // readable size in world

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;  // needed for raycasting/events
            canvas.sortingOrder = 50;

            // Constant Pixel Size – does NOT drive root; we control position/size
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            // Canvas root RectTransform – we control this (not driven)
            var canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(380, 480);
            canvasRect.anchoredPosition = Vector2.zero;

            // Container for content – drag moves canvas root (both work in World Space)
            var container = new GameObject("DashboardContainer", typeof(RectTransform));
            container.transform.SetParent(canvasGO.transform, false);
            var containerRect = (RectTransform)container.transform;
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = Vector2.zero;
            containerRect.offsetMax = Vector2.zero;
            containerRect.localScale = Vector3.one;

            // Both raycasters: GraphicRaycaster (mouse/fallback) + TrackedDeviceGraphicRaycaster (XR)
            canvasGO.AddComponent<GraphicRaycaster>();
            var raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycasterType != null)
                canvasGO.AddComponent(raycasterType);

            var canvasGroup = canvasGO.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;  // must be true so header/buttons receive pointer events

            // Panel background (child of Container – not driven by Canvas)
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(container.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.12f, 0.18f, 0.98f);  // solid dark panel
            panelImage.raycastTarget = false;  // let ray pass through to buttons

            // Header (draggable – moves canvas root in World Space)
            var header = CreateLabel(panel.transform, "Header", "Farm Dashboard", 0);
            var headerText = header.GetComponent<TMP_Text>();
            headerText.fontSize = 20;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.raycastTarget = true;  // allow drag
            var headerDrag = header.AddComponent<FarmDashboardDrag>();
            SetPrivateField(headerDrag, "_targetToMove", canvasRect);

            // Text labels
            var simMgr = hub.GetComponent<FarmSimulationManager>();
            var netSync = hub.GetComponent<FarmSimulationNetworkSync>();
            var pollMgr = hub.GetComponent<PollVoteManager>();

            var soilText = CreateLabel(panel.transform, "SoilMoistureText", "Soil Moisture: --%", 1);
            var healthText = CreateLabel(panel.transform, "CropHealthText", "Crop Health: --%", 2);
            var waterText = CreateLabel(panel.transform, "WaterUsageText", "Water Usage Today: --", 3);
            var tempText = CreateLabel(panel.transform, "TemperatureText", "Temperature: --°C", 4);
            var yieldText = CreateLabel(panel.transform, "PredictedYieldText", "Predicted Yield: --", 5);
            var alertsText = CreateLabel(panel.transform, "AlertsText", "No active alerts", 6);

            // FarmDashboardUI
            var dashboardUI = canvasGO.AddComponent<FarmDashboardUI>();
            SetPrivateField(dashboardUI, "simulationManager", simMgr);
            SetPrivateField(dashboardUI, "networkSync", netSync);
            SetPrivateField(dashboardUI, "soilMoistureText", soilText.GetComponent<TMP_Text>());
            SetPrivateField(dashboardUI, "cropHealthText", healthText.GetComponent<TMP_Text>());
            SetPrivateField(dashboardUI, "waterUsageText", waterText.GetComponent<TMP_Text>());
            SetPrivateField(dashboardUI, "temperatureText", tempText.GetComponent<TMP_Text>());
            SetPrivateField(dashboardUI, "predictedYieldText", yieldText.GetComponent<TMP_Text>());
            SetPrivateField(dashboardUI, "alertsText", alertsText.GetComponent<TMP_Text>());

            // PollVotePanel
            var pollPanel = CreatePollVotePanel(panel.transform, pollMgr);

            // UI layer so XR Ray Interactor can hit buttons (check controller's Raycast Mask includes UI)
            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                SetLayerRecursive(canvasGO, uiLayer);

            return canvasGO;
        }

        private static GameObject CreateOrFindFarmDataHub(GameObject hub)
        {
            var dataHub = GameObject.Find("FarmDataHub");
            if (dataHub == null)
            {
                dataHub = new GameObject("FarmDataHub");
                Undo.RegisterCreatedObjectUndo(dataHub, "Farm Setup");
            }

            var dataMgr = dataHub.GetComponent<FarmDataManager>();
            if (dataMgr == null) dataMgr = dataHub.AddComponent<FarmDataManager>();

            SetPrivateField(dataMgr, "simulationManager", hub.GetComponent<FarmSimulationManager>());
            SetPrivateField(dataMgr, "networkSync", hub.GetComponent<FarmSimulationNetworkSync>());
            SetPrivateField(dataMgr, "pollVoteManager", hub.GetComponent<PollVoteManager>());
            return dataHub;
        }

        private static GameObject CreateOrFindTabletApp(GameObject hub)
        {
            var existing = GameObject.Find("SmartFarmTablet");
            var dataHub = CreateOrFindFarmDataHub(hub);
            var dataMgr = dataHub.GetComponent<FarmDataManager>();
            if (existing != null)
            {
                LinkTabletToHub(existing, dataMgr);
                return existing;
            }

            var cam = FindCameraForDashboard();
            var tablet = new GameObject("SmartFarmTablet", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(tablet, "Farm Setup");
            tablet.transform.position = new Vector3(0.5f, 1.35f, 1.4f);
            tablet.transform.rotation = Quaternion.Euler(12f, 160f, 0f);
            tablet.transform.localScale = new Vector3(0.0014f, 0.0014f, 0.0014f);

            var canvas = tablet.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 60;
            tablet.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            tablet.AddComponent<GraphicRaycaster>();
            var raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycasterType != null) tablet.AddComponent(raycasterType);

            var rootRect = tablet.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(900, 620);

            var bg = CreatePanel(tablet.transform, "AppBackground", new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 1), new Color(0.07f, 0.1f, 0.16f, 0.97f));
            bg.GetComponent<Image>().raycastTarget = false;

            var header = CreatePanel(tablet.transform, "Header", new Vector2(0, -70), new Vector2(0, 1), new Vector2(1, 1), new Color(0.1f, 0.18f, 0.26f, 0.98f));
            var titleText = CreateText(header.transform, "AppTitleText", "Smart Farm Tablet", 26, TextAlignmentOptions.Left, new Vector2(14, 0), new Vector2(0, 0), new Vector2(0.55f, 1));
            var statusIconGO = CreatePanel(header.transform, "StatusIcon", new Vector2(0, 0), new Vector2(0.78f, 0.5f), new Vector2(0.78f, 0.5f), new Color(0.9f, 0.3f, 0.3f, 1f));
            statusIconGO.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 20);
            var statusText = CreateText(header.transform, "ConnectionStatusText", "Local", 18, TextAlignmentOptions.Left, new Vector2(10, 0), new Vector2(0.8f, 0), new Vector2(1f, 1f));

            var tabBar = CreatePanel(tablet.transform, "TabBar", new Vector2(0, -120), new Vector2(0, 1), new Vector2(1, 1), new Color(0.08f, 0.14f, 0.22f, 0.98f));
            var overviewTab = CreateButton(tabBar.transform, "Overview", new Vector2(-330, -8));
            var irrigationTab = CreateButton(tabBar.transform, "Irrigation", new Vector2(-165, -8));
            var alertsTab = CreateButton(tabBar.transform, "Alerts", new Vector2(0, -8));
            var pollsTab = CreateButton(tabBar.transform, "Polls", new Vector2(165, -8));
            var historyTab = CreateButton(tabBar.transform, "History", new Vector2(330, -8));
            ResizeButton(overviewTab, 150, 40);
            ResizeButton(irrigationTab, 150, 40);
            ResizeButton(alertsTab, 150, 40);
            ResizeButton(pollsTab, 150, 40);
            ResizeButton(historyTab, 150, 40);

            var pinBar = CreatePanel(tablet.transform, "PinBar", new Vector2(0, 48), new Vector2(0, 0), new Vector2(1, 0), new Color(0.08f, 0.14f, 0.22f, 0.98f));
            var pinBtn = CreateButton(pinBar.transform, "Pin", new Vector2(-180, -8));
            var wristBtn = CreateButton(pinBar.transform, "Wrist", new Vector2(0, -8));
            var deskBtn = CreateButton(pinBar.transform, "Desk", new Vector2(180, -8));

            var content = new GameObject("ContentRoot", typeof(RectTransform));
            content.transform.SetParent(tablet.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(20, 80);
            contentRect.offsetMax = new Vector2(-20, -130);

            var overviewPage = new GameObject("OverviewPage", typeof(RectTransform));
            overviewPage.transform.SetParent(content.transform, false);
            Stretch((RectTransform)overviewPage.transform);
            var irrigationPage = new GameObject("IrrigationPage", typeof(RectTransform));
            irrigationPage.transform.SetParent(content.transform, false);
            Stretch((RectTransform)irrigationPage.transform);
            var alertsPage = new GameObject("AlertsPage", typeof(RectTransform));
            alertsPage.transform.SetParent(content.transform, false);
            Stretch((RectTransform)alertsPage.transform);
            var pollsPage = new GameObject("PollsPage", typeof(RectTransform));
            pollsPage.transform.SetParent(content.transform, false);
            Stretch((RectTransform)pollsPage.transform);
            var historyPage = new GameObject("HistoryPage", typeof(RectTransform));
            historyPage.transform.SetParent(content.transform, false);
            Stretch((RectTransform)historyPage.transform);
            irrigationPage.SetActive(false);
            alertsPage.SetActive(false);
            pollsPage.SetActive(false);
            historyPage.SetActive(false);

            BuildOverviewPage(overviewPage.transform, out var overviewUI);
            BuildIrrigationPage(irrigationPage.transform, out var irrigationUI);
            BuildAlertsPage(alertsPage.transform, out var alertsUI);
            BuildPollsPage(pollsPage.transform, out var pollPageUI);
            BuildHistoryPage(historyPage.transform, out var historyUI);

            var anim = tablet.AddComponent<SimpleUIAnimationHelper>();
            var app = tablet.AddComponent<TabletAppController>();
            SetPrivateField(app, "dataManager", dataMgr);
            SetPrivateField(app, "appTitleText", titleText.GetComponent<TMP_Text>());
            SetPrivateField(app, "connectionStatusText", statusText.GetComponent<TMP_Text>());
            SetPrivateField(app, "connectionStatusIcon", statusIconGO.GetComponent<Image>());
            SetPrivateField(app, "overviewTabButton", overviewTab.GetComponent<Button>());
            SetPrivateField(app, "irrigationTabButton", irrigationTab.GetComponent<Button>());
            SetPrivateField(app, "alertsTabButton", alertsTab.GetComponent<Button>());
            SetPrivateField(app, "pollsTabButton", pollsTab.GetComponent<Button>());
            SetPrivateField(app, "historyTabButton", historyTab.GetComponent<Button>());
            SetPrivateField(app, "overviewPage", overviewPage);
            SetPrivateField(app, "irrigationPage", irrigationPage);
            SetPrivateField(app, "alertsPage", alertsPage);
            SetPrivateField(app, "pollsPage", pollsPage);
            SetPrivateField(app, "historyPage", historyPage);
            SetPrivateField(app, "animationHelper", anim);
            SetPrivateField(app, "pinToggleButton", pinBtn.GetComponent<Button>());
            SetPrivateField(app, "wristModeButton", wristBtn.GetComponent<Button>());
            SetPrivateField(app, "deskModeButton", deskBtn.GetComponent<Button>());
            SetPrivateField(app, "pinButtonLabel", pinBtn.GetComponentInChildren<TMP_Text>());
            SetPrivateField(app, "deskAnchor", EnsureDeskAnchor().transform);
            var leftWrist = FindLeftWristAnchor();
            if (leftWrist != null) SetPrivateField(app, "leftWristAnchor", leftWrist);

            SetPrivateField(overviewUI, "dataManager", dataMgr);
            SetPrivateField(irrigationUI, "dataManager", dataMgr);
            SetPrivateField(alertsUI, "dataManager", dataMgr);
            SetPrivateField(pollPageUI, "dataManager", dataMgr);
            SetPrivateField(pollPageUI, "pollManager", hub.GetComponent<PollVoteManager>());
            SetPrivateField(pollPageUI, "animationHelper", anim);
            SetPrivateField(historyUI, "dataManager", dataMgr);

            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(tablet, uiLayer);
            LinkTabletToHub(tablet, dataMgr);
            var themeApplier = tablet.AddComponent<TabletThemeAutoApplier>();
            SetPrivateField(themeApplier, "themeProfile", EnsureDefaultTabletThemeAsset());
            themeApplier.ApplyTheme();
            return tablet;
        }

        private static void LinkTabletToHub(GameObject tablet, FarmDataManager dataMgr)
        {
            if (tablet == null || dataMgr == null) return;
            var overview = tablet.GetComponentInChildren<OverviewUI>(true);
            var alerts = tablet.GetComponentInChildren<AlertsUI>(true);
            var polls = tablet.GetComponentInChildren<PollPageUI>(true);
            var irrigation = tablet.GetComponentInChildren<IrrigationUI>(true);
            var history = tablet.GetComponentInChildren<HistoryUI>(true);
            if (overview != null) SetPrivateField(overview, "dataManager", dataMgr);
            if (alerts != null) SetPrivateField(alerts, "dataManager", dataMgr);
            if (polls != null) SetPrivateField(polls, "dataManager", dataMgr);
            if (irrigation != null) SetPrivateField(irrigation, "dataManager", dataMgr);
            if (history != null) SetPrivateField(history, "dataManager", dataMgr);
            var theme = tablet.GetComponent<TabletThemeAutoApplier>();
            if (theme == null) theme = tablet.AddComponent<TabletThemeAutoApplier>();
            SetPrivateField(theme, "themeProfile", EnsureDefaultTabletThemeAsset());
            theme.ApplyTheme();
        }

        private static void BuildOverviewPage(Transform page, out OverviewUI overviewUI)
        {
            var soilValue = CreateText(page, "SoilValueText", "50%", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.03f, 0.72f), new Vector2(0.3f, 0.95f));
            var soilTrend = CreateText(page, "SoilTrendText", "—", 18, TextAlignmentOptions.TopRight, new Vector2(-12, -8), new Vector2(0.03f, 0.72f), new Vector2(0.3f, 0.95f));
            var soilBar = CreateProgress(page, "SoilProgress", new Vector2(0.03f, 0.66f), new Vector2(0.3f, 0.7f));

            var healthValue = CreateText(page, "HealthValueText", "100%", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.35f, 0.72f), new Vector2(0.62f, 0.95f));
            var healthTrend = CreateText(page, "HealthTrendText", "—", 18, TextAlignmentOptions.TopRight, new Vector2(-12, -8), new Vector2(0.35f, 0.72f), new Vector2(0.62f, 0.95f));
            var healthBar = CreateProgress(page, "HealthProgress", new Vector2(0.35f, 0.66f), new Vector2(0.62f, 0.7f));

            var tempValue = CreateText(page, "TemperatureValueText", "24.0°C", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.67f, 0.72f), new Vector2(0.97f, 0.95f));
            var tempTrend = CreateText(page, "TemperatureTrendText", "—", 18, TextAlignmentOptions.TopRight, new Vector2(-12, -8), new Vector2(0.67f, 0.72f), new Vector2(0.97f, 0.95f));
            var yieldValue = CreateText(page, "PredictedYieldText", "0", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.03f, 0.38f), new Vector2(0.48f, 0.6f));
            var irrigationStatus = CreateText(page, "IrrigationStatusText", "OFF", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.52f, 0.38f), new Vector2(0.97f, 0.6f));

            overviewUI = page.gameObject.AddComponent<OverviewUI>();
            SetPrivateField(overviewUI, "soilValueText", soilValue.GetComponent<TMP_Text>());
            SetPrivateField(overviewUI, "soilProgress", soilBar.GetComponent<Image>());
            SetPrivateField(overviewUI, "soilTrendText", soilTrend.GetComponent<TMP_Text>());
            SetPrivateField(overviewUI, "healthValueText", healthValue.GetComponent<TMP_Text>());
            SetPrivateField(overviewUI, "healthProgress", healthBar.GetComponent<Image>());
            SetPrivateField(overviewUI, "healthTrendText", healthTrend.GetComponent<TMP_Text>());
            SetPrivateField(overviewUI, "temperatureValueText", tempValue.GetComponent<TMP_Text>());
            SetPrivateField(overviewUI, "temperatureTrendText", tempTrend.GetComponent<TMP_Text>());
            SetPrivateField(overviewUI, "predictedYieldText", yieldValue.GetComponent<TMP_Text>());
            SetPrivateField(overviewUI, "irrigationStatusText", irrigationStatus.GetComponent<TMP_Text>());
        }

        private static void BuildIrrigationPage(Transform page, out IrrigationUI irrigationUI)
        {
            var status = CreateText(page, "IrrigationStatusText", "Irrigation: OFF", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.92f));
            var toggle = CreateButton(page, "Toggle", new Vector2(-220, -220));
            ResizeButton(toggle, 180, 50);
            var toggleLabel = toggle.GetComponentInChildren<TMP_Text>();
            toggleLabel.text = "Turn ON";
            var boost = CreateButton(page, "Boost30", new Vector2(0, -220));
            ResizeButton(boost, 220, 50);
            boost.GetComponentInChildren<TMP_Text>().text = "Boost 30 seconds";
            var morning = CreateButton(page, "Morning", new Vector2(-220, -290));
            var noon = CreateButton(page, "Noon", new Vector2(0, -290));
            var evening = CreateButton(page, "Evening", new Vector2(220, -290));
            ResizeButton(morning, 160, 42); ResizeButton(noon, 160, 42); ResizeButton(evening, 160, 42);

            irrigationUI = page.gameObject.AddComponent<IrrigationUI>();
            SetPrivateField(irrigationUI, "irrigationStatusText", status.GetComponent<TMP_Text>());
            SetPrivateField(irrigationUI, "toggleButton", toggle.GetComponent<Button>());
            SetPrivateField(irrigationUI, "toggleButtonText", toggleLabel);
            SetPrivateField(irrigationUI, "boost30Button", boost.GetComponent<Button>());
            SetPrivateField(irrigationUI, "morningPresetButton", morning.GetComponent<Button>());
            SetPrivateField(irrigationUI, "noonPresetButton", noon.GetComponent<Button>());
            SetPrivateField(irrigationUI, "eveningPresetButton", evening.GetComponent<Button>());
        }

        private static void BuildAlertsPage(Transform page, out AlertsUI alertsUI)
        {
            var badgeRoot = CreatePanel(page, "BadgeRoot", new Vector2(0, 0), new Vector2(0.92f, 0.9f), new Vector2(0.98f, 0.98f), new Color(0.85f, 0.2f, 0.2f, 0.95f));
            var badgeText = CreateText(badgeRoot.transform, "BadgeCountText", "0", 16, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, Vector2.one);
            var listRoot = new GameObject("ListRoot", typeof(RectTransform)).transform;
            listRoot.SetParent(page, false);
            var lr = (RectTransform)listRoot;
            lr.anchorMin = new Vector2(0.03f, 0.08f);
            lr.anchorMax = new Vector2(0.97f, 0.86f);
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var emptyState = CreateText(page, "EmptyState", "No alerts right now", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.03f, 0.2f), new Vector2(0.97f, 0.8f));

            var template = CreatePanel(page, "AlertItemTemplate", new Vector2(0, 0), new Vector2(0.03f, 0.75f), new Vector2(0.97f, 0.9f), new Color(0.2f, 0.35f, 0.55f, 0.95f));
            var severity = CreateText(template.transform, "SeverityText", "INFO", 16, TextAlignmentOptions.Left, new Vector2(8, 0), new Vector2(0f, 0f), new Vector2(0.2f, 1f));
            var timestamp = CreateText(template.transform, "TimestampText", "00:00:00", 16, TextAlignmentOptions.Left, new Vector2(8, 0), new Vector2(0.2f, 0f), new Vector2(0.45f, 1f));
            var message = CreateText(template.transform, "MessageText", "message", 16, TextAlignmentOptions.Left, new Vector2(8, 0), new Vector2(0.45f, 0f), new Vector2(0.8f, 1f));
            var ackBtn = CreateButton(template.transform, "Ack", new Vector2(0, -8));
            var ackRt = ackBtn.GetComponent<RectTransform>();
            ackRt.anchorMin = new Vector2(0.82f, 0.15f);
            ackRt.anchorMax = new Vector2(0.98f, 0.85f);
            ackRt.sizeDelta = Vector2.zero;
            ackRt.anchoredPosition = Vector2.zero;
            ackBtn.GetComponentInChildren<TMP_Text>().text = "Acknowledge";
            var itemUI = template.AddComponent<AlertListItemUI>();
            SetPrivateField(itemUI, "severityText", severity.GetComponent<TMP_Text>());
            SetPrivateField(itemUI, "timestampText", timestamp.GetComponent<TMP_Text>());
            SetPrivateField(itemUI, "messageText", message.GetComponent<TMP_Text>());
            SetPrivateField(itemUI, "acknowledgeButton", ackBtn.GetComponent<Button>());
            SetPrivateField(itemUI, "background", template.GetComponent<Image>());
            template.SetActive(false);

            alertsUI = page.gameObject.AddComponent<AlertsUI>();
            SetPrivateField(alertsUI, "badgeCountText", badgeText.GetComponent<TMP_Text>());
            SetPrivateField(alertsUI, "badgeRoot", badgeRoot);
            SetPrivateField(alertsUI, "listRoot", listRoot);
            SetPrivateField(alertsUI, "itemPrefab", itemUI);
            SetPrivateField(alertsUI, "emptyStateRoot", emptyState);
        }

        private static void BuildPollsPage(Transform page, out PollPageUI pollPageUI)
        {
            var question = CreateText(page, "QuestionText", "Enable Irrigation?", 28, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.94f));
            var results = CreateText(page, "ResultsText", "Yes: 0 (0%)\nNo: 0 (0%)", 22, TextAlignmentOptions.TopLeft, new Vector2(0, -8), new Vector2(0.05f, 0.48f), new Vector2(0.6f, 0.75f));
            var votersA = CreateText(page, "VotersAText", "Voters A: -", 18, TextAlignmentOptions.Left, Vector2.zero, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.46f));
            var votersB = CreateText(page, "VotersBText", "Voters B: -", 18, TextAlignmentOptions.Left, Vector2.zero, new Vector2(0.05f, 0.33f), new Vector2(0.95f, 0.39f));
            var openPoll = CreateButton(page, "OpenPollMain", new Vector2(0, -255));
            ResizeButton(openPoll, 220, 54);
            openPoll.GetComponentInChildren<TMP_Text>().text = "Open Poll";

            var modal = CreatePanel(page, "PollModalRoot", Vector2.zero, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.86f), new Color(0.05f, 0.09f, 0.14f, 0.98f));
            var modalQ = CreateText(modal.transform, "ModalQuestionText", "Enable Irrigation?", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.92f));
            var countdown = CreateText(modal.transform, "CountdownText", "Time: 15s", 20, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.7f));
            var submitted = CreateText(modal.transform, "VoteSubmittedText", "Vote submitted", 18, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.58f));
            var optionA = CreateButton(modal.transform, "OptionA", new Vector2(-120, -180));
            var optionB = CreateButton(modal.transform, "OptionB", new Vector2(120, -180));
            ResizeButton(optionA, 170, 52);
            ResizeButton(optionB, 170, 52);
            optionA.GetComponentInChildren<TMP_Text>().text = "Option A";
            optionB.GetComponentInChildren<TMP_Text>().text = "Option B";
            var closeApply = CreateButton(modal.transform, "CloseApply", new Vector2(0, -250));
            ResizeButton(closeApply, 240, 52);
            closeApply.GetComponentInChildren<TMP_Text>().text = "Close & Apply";
            submitted.SetActive(false);
            modal.SetActive(false);

            pollPageUI = page.gameObject.AddComponent<PollPageUI>();
            SetPrivateField(pollPageUI, "questionText", question.GetComponent<TMP_Text>());
            SetPrivateField(pollPageUI, "resultsText", results.GetComponent<TMP_Text>());
            SetPrivateField(pollPageUI, "votersAText", votersA.GetComponent<TMP_Text>());
            SetPrivateField(pollPageUI, "votersBText", votersB.GetComponent<TMP_Text>());
            SetPrivateField(pollPageUI, "openPollButton", openPoll.GetComponent<Button>());
            SetPrivateField(pollPageUI, "pollModalRoot", modal);
            SetPrivateField(pollPageUI, "modalQuestionText", modalQ.GetComponent<TMP_Text>());
            SetPrivateField(pollPageUI, "countdownText", countdown.GetComponent<TMP_Text>());
            SetPrivateField(pollPageUI, "voteSubmittedText", submitted.GetComponent<TMP_Text>());
            SetPrivateField(pollPageUI, "optionAButton", optionA.GetComponent<Button>());
            SetPrivateField(pollPageUI, "optionBButton", optionB.GetComponent<Button>());
            SetPrivateField(pollPageUI, "closeModalButton", closeApply.GetComponent<Button>());
        }

        private static void BuildHistoryPage(Transform page, out HistoryUI historyUI)
        {
            var listRoot = new GameObject("ListRoot", typeof(RectTransform)).transform;
            listRoot.SetParent(page, false);
            var lr = (RectTransform)listRoot;
            lr.anchorMin = new Vector2(0.03f, 0.08f);
            lr.anchorMax = new Vector2(0.97f, 0.92f);
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var emptyState = CreateText(page, "EmptyState", "No history yet", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.03f, 0.2f), new Vector2(0.97f, 0.8f));

            var template = CreatePanel(page, "HistoryItemTemplate", Vector2.zero, new Vector2(0.03f, 0.8f), new Vector2(0.97f, 0.9f), new Color(0.12f, 0.2f, 0.3f, 0.95f));
            var ts = CreateText(template.transform, "TimestampText", "00:00:00", 16, TextAlignmentOptions.Left, new Vector2(8, 0), new Vector2(0f, 0f), new Vector2(0.22f, 1f));
            var msg = CreateText(template.transform, "MessageText", "History event message", 16, TextAlignmentOptions.Left, new Vector2(8, 0), new Vector2(0.22f, 0f), new Vector2(1f, 1f));
            var item = template.AddComponent<HistoryListItemUI>();
            SetPrivateField(item, "timestampText", ts.GetComponent<TMP_Text>());
            SetPrivateField(item, "messageText", msg.GetComponent<TMP_Text>());
            template.SetActive(false);

            historyUI = page.gameObject.AddComponent<HistoryUI>();
            SetPrivateField(historyUI, "listRoot", listRoot);
            SetPrivateField(historyUI, "itemPrefab", item);
            SetPrivateField(historyUI, "emptyStateRoot", emptyState);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 yOffsets, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(0, yOffsets.x);
            rt.offsetMax = new Vector2(0, yOffsets.y);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static GameObject CreateText(Transform parent, string name, string value, float fontSize, TextAlignmentOptions align, Vector2 offset, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = offset;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            return go;
        }

        private static GameObject CreateProgress(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var bg = CreatePanel(parent, name + "_BG", Vector2.zero, anchorMin, anchorMax, new Color(0.16f, 0.24f, 0.34f, 1f));
            var fill = CreatePanel(bg.transform, name, Vector2.zero, Vector2.zero, Vector2.one, new Color(0.2f, 0.8f, 0.35f, 1f));
            var img = fill.GetComponent<Image>();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = 0.5f;
            return fill;
        }

        private static void ResizeButton(GameObject button, float width, float height)
        {
            var rt = button.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject EnsureDeskAnchor()
        {
            var desk = GameObject.Find("TabletDeskAnchor");
            if (desk != null) return desk;
            desk = new GameObject("TabletDeskAnchor");
            desk.transform.position = new Vector3(0.55f, 1.1f, 1.35f);
            desk.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
            return desk;
        }

        private static Transform FindLeftWristAnchor()
        {
            string[] candidates =
            {
                "LeftHand Controller",
                "Left Controller",
                "Left Hand",
                "LeftHand",
                "Left Wrist"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                var go = GameObject.Find(candidates[i]);
                if (go != null) return go.transform;
            }
            return null;
        }

        private static GameObject CreateLabel(Transform parent, string name, string defaultText, int index)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -20 - index * 35);
            rect.sizeDelta = new Vector2(-20, 30);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = defaultText;
            text.fontSize = 18;
            text.color = Color.white;
            text.raycastTarget = false;  // labels don't need raycasts

            return go;
        }

        private static GameObject CreatePollVotePanel(Transform parent, PollVoteManager pollMgr)
        {
            var panel = new GameObject("PollVotePanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot = new Vector2(0.5f, 0);
            panelRect.anchoredPosition = new Vector2(0, 20);
            panelRect.sizeDelta = new Vector2(-20, 180);

            var questionText = CreateLabel(panel.transform, "QuestionText", "Enable Irrigation?", 0);
            var resultsText = CreateLabel(panel.transform, "ResultsText", "Option A: 0 (0%)\nOption B: 0 (0%)", 1);
            resultsText.GetComponent<RectTransform>().sizeDelta = new Vector2(-20, 60);

            // Buttons (labels match Option A/B from PollVoteManager)
            var voteA = CreateButton(panel.transform, "Yes", new Vector2(-105, -100));
            var voteB = CreateButton(panel.transform, "No", new Vector2(105, -100));
            var openBtn = CreateButton(panel.transform, "Open Poll", new Vector2(-105, -145));
            var closeBtn = CreateButton(panel.transform, "Close & Apply", new Vector2(105, -145));

            var pollUI = panel.AddComponent<PollVoteUI>();
            SetPrivateField(pollUI, "pollManager", pollMgr);
            SetPrivateField(pollUI, "questionText", questionText.GetComponent<TMP_Text>());
            SetPrivateField(pollUI, "resultsText", resultsText.GetComponent<TMP_Text>());
            SetPrivateField(pollUI, "voteAButton", voteA.GetComponent<Button>());
            SetPrivateField(pollUI, "voteBButton", voteB.GetComponent<Button>());
            SetPrivateField(pollUI, "openPollButton", openBtn.GetComponent<Button>());
            SetPrivateField(pollUI, "closePollButton", closeBtn.GetComponent<Button>());

            return panel;
        }

        private static GameObject CreateButton(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject(nameof(Button) + "_" + label.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(120, 35);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.7f, 0.3f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = image;

            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textObj.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;  // let ray hit button Image, not text

            return go;
        }

        private static void LinkDashboardToHub(GameObject dashboard, GameObject hub)
        {
            var dashboardUI = dashboard.GetComponent<FarmDashboardUI>();
            var pollUI = dashboard.GetComponentInChildren<PollVoteUI>();
            if (dashboardUI != null)
            {
                SetPrivateField(dashboardUI, "simulationManager", hub.GetComponent<FarmSimulationManager>());
                SetPrivateField(dashboardUI, "networkSync", hub.GetComponent<FarmSimulationNetworkSync>());
            }
            if (pollUI != null)
                SetPrivateField(pollUI, "pollManager", hub.GetComponent<PollVoteManager>());
        }

        public static void EnableXRUIControllers()
        {
            int enabled = 0;
            foreach (var comp in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var uiProp = so.FindProperty("m_EnableUIInteraction");
                if (uiProp != null && !uiProp.boolValue)
                {
                    uiProp.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    enabled++;
                }
                var rayProp = so.FindProperty("m_RaycastMask");
                if (rayProp != null)
                {
                    var bits = rayProp.FindPropertyRelative("m_Bits");
                    if (bits != null)
                    {
                        int val = bits.intValue;
                        int uiLayer = LayerMask.NameToLayer("UI");
                        int uiBit = uiLayer >= 0 ? (1 << uiLayer) : 0;
                        if (uiBit > 0 && (val & uiBit) == 0)
                        {
                            bits.intValue = val | uiBit | 1;  // add UI + Default
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }
            if (enabled > 0)
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[SmartFarm] Enabled UI Interaction on {enabled} XR interactor(s).");
            }
        }

        private static void EnsureXRUIEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var xrInputType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit");
            if (xrInputType != null)
            {
                eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                if (eventSystem != null && eventSystem.GetComponent(xrInputType) == null)
                {
                    var standalone = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    if (standalone != null) Object.DestroyImmediate(standalone);
                    eventSystem.gameObject.AddComponent(xrInputType);
                }
            }
        }

        private static void AddPlantInstances()
        {
            var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/Prefabs/PlantInstance.prefab");
            if (plantPrefab == null) return;

            var positions = new[] { new Vector3(0, 0, 0), new Vector3(1.5f, 0, 0), new Vector3(3f, 0, 0) };
            for (int i = 0; i < 3; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab);
                go.name = "Plant_" + (i + 1);
                go.transform.position = positions[i];
            }
            Debug.Log("[SmartFarm] Added 3 PlantInstance prefabs. If PlantInstance prefab doesn't exist, add plants manually via Tools > Plant Growth.");
        }

        private static bool IsSceneInBuildSettings(string scenePath)
        {
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path == scenePath) return true;
            return false;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }

        /// <summary>
        /// Creates only the FarmDashboard if missing. Use when Farm Setup completed but dashboard wasn't created.
        /// </summary>
        public static void CreateFarmDashboardOnly()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SmartFarm] Stop Play mode first!");
                return;
            }
            var hub = GameObject.Find("FarmSimulationHub");
            if (hub == null)
            {
                Debug.LogError("[SmartFarm] FarmSimulationHub not found. Run Tools > Farm > Farm Setup first.");
                return;
            }
            var plantMgr = hub.GetComponent<FarmSimulationManager>();
            if (plantMgr != null)
            {
                var pm = Object.FindFirstObjectByType<PlantGrowth.PlantGrowthManager>();
                if (pm != null) SetPrivateField(plantMgr, "plantGrowthManager", pm);
            }
            try
            {
                var dashboard = CreateOrFindFarmDashboard(hub);
                if (dashboard != null)
                {
                    EnableXRUIControllers();
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    Selection.activeGameObject = dashboard;
                    Debug.Log("[SmartFarm] FarmDashboard created. Press Play to see it.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public static void SavePrefabs()
        {
            EnsureDirectories();
            var hub = GameObject.Find("FarmSimulationHub");
            var dashboard = GameObject.Find("FarmDashboard");
            if (hub != null)
            {
                var path = PrefabsPath + "/FarmSimulationHub.prefab";
                PrefabUtility.SaveAsPrefabAsset(hub, path);
                Debug.Log("[SmartFarm] Saved FarmSimulationHub prefab to " + path);
            }
            if (dashboard != null)
            {
                var path = PrefabsPath + "/FarmDashboard.prefab";
                PrefabUtility.SaveAsPrefabAsset(dashboard, path);
                Debug.Log("[SmartFarm] Saved FarmDashboard prefab to " + path);
            }
            if (hub == null && dashboard == null)
                Debug.LogWarning("[SmartFarm] Run Full Setup first to create objects.");
        }

        public static void RegisterWithNetworkManager()
        {
            var hubPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/FarmSimulationHub.prefab");
            if (hubPrefab == null)
            {
                Debug.LogWarning("[SmartFarm] FarmSimulationHub prefab not found. Run Full Setup, then Save Prefabs first.");
                return;
            }

            var listAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/DefaultNetworkPrefabs.asset");
            if (listAsset == null)
            {
                listAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/VRMPAssets/DefaultNetworkPrefabs.asset");
            }
            if (listAsset != null)
            {
                var so = new SerializedObject(listAsset);
                var listProp = so.FindProperty("List");
                if (listProp != null)
                {
                    listProp.InsertArrayElementAtIndex(listProp.arraySize);
                    var newEntry = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                    newEntry.FindPropertyRelative("Prefab").objectReferenceValue = hubPrefab;
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log("[SmartFarm] FarmSimulationHub added to NetworkPrefabsList.");
                    return;
                }
            }
            Debug.LogWarning("[SmartFarm] Could not find NetworkPrefabsList. Manually add FarmSimulationHub prefab to your NetworkManager's Prefabs list. For scene objects, ensure your scene is in Build Settings.");
        }
    }
}
