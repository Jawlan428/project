using System.IO;
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

            // 5b. Setup Wild Harvest crop assets/prefabs and spawn demo field if missing
            PlantGrowth.Editor.PlantGrowthSetupWizard.SetupWildHarvestCrops();
            CreateOrFindWildHarvestField();

            // 5c. Setup CropGrowthController system (Wheat + Corn with weather + yield integration)
            CropGrowthSetupEditor.RunSetup();

            // 6. Create tablet app + page system
            CreateOrFindTabletApp(hub);

            // 7. Create Weather Control Panel + full weather visuals (rain, lightning, audio)
            CreateOrFindWeatherPanel(hub);
            CreateFullWeatherSetup(hub);

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
            if (!AssetDatabase.IsValidFolder("Assets/SmartFarm/WeatherSkyboxes"))
                AssetDatabase.CreateFolder("Assets/SmartFarm", "WeatherSkyboxes");
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
            // Status label
            var status = CreateText(page, "IrrigationStatusText", "Irrigation: OFF", 24,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.92f));

            // Row 1 — Turn ON (green) and Turn OFF (red), side by side
            var turnOn  = CreateButton(page, "TurnOn",  new Vector2(-120, -200));
            var turnOff = CreateButton(page, "TurnOff", new Vector2( 120, -200));
            ResizeButton(turnOn,  200, 54);
            ResizeButton(turnOff, 200, 54);
            turnOn.GetComponentInChildren<TMP_Text>().text  = "Turn ON";
            turnOff.GetComponentInChildren<TMP_Text>().text = "Turn OFF";

            // Row 2 — Boost centred
            var boost = CreateButton(page, "Boost30", new Vector2(0, -280));
            ResizeButton(boost, 240, 54);
            boost.GetComponentInChildren<TMP_Text>().text = "Boost 30 seconds";

            // Colour Turn OFF red to distinguish it
            var turnOffImg = turnOff.GetComponent<Image>();
            if (turnOffImg != null) turnOffImg.color = new Color(0.82f, 0.18f, 0.10f, 1f);

            irrigationUI = page.gameObject.AddComponent<IrrigationUI>();
            SetPrivateField(irrigationUI, "irrigationStatusText", status.GetComponent<TMP_Text>());
            SetPrivateField(irrigationUI, "turnOnButton",  turnOn.GetComponent<Button>());
            SetPrivateField(irrigationUI, "turnOffButton", turnOff.GetComponent<Button>());
            SetPrivateField(irrigationUI, "boost30Button", boost.GetComponent<Button>());
        }

        /// <summary>
        /// Rebuilds the Irrigation page on the existing SmartFarmTablet in the scene.
        /// Removes the old 5-button layout and replaces it with Turn ON / Turn OFF / Boost.
        /// Menu: Tools > Smart Farm > Rebuild Irrigation Page
        /// </summary>
        public static void RebuildIrrigationPage()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SmartFarm] Stop Play mode before rebuilding.");
                return;
            }

            var existing = Object.FindFirstObjectByType<IrrigationUI>();
            if (existing == null)
            {
                Debug.LogWarning("[SmartFarm] No IrrigationUI found. Run Full Platform Setup first.");
                return;
            }

            var page    = existing.gameObject;
            var dataMgr = Object.FindFirstObjectByType<FarmDataManager>();

            // Remove all children (old buttons / labels)
            while (page.transform.childCount > 0)
                Object.DestroyImmediate(page.transform.GetChild(0).gameObject);

            // Remove old component
            Object.DestroyImmediate(existing);

            // Rebuild with new 3-button layout
            BuildIrrigationPage(page.transform, out var newUI);

            if (dataMgr != null)
                SetPrivateField(newUI, "dataManager", dataMgr);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SmartFarm] Irrigation page rebuilt — Turn ON / Turn OFF / Boost 30s.");
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

        private static void CreateOrFindWeatherPanel(GameObject hub)
        {
            var existing = GameObject.Find("WeatherControlPanel");
            if (existing != null)
            {
                LinkWeatherPanelToHub(existing, hub);
                return;
            }

            var cam = FindCameraForDashboard();
            var panelGO = new GameObject("WeatherControlPanel");
            Undo.RegisterCreatedObjectUndo(panelGO, "Weather Panel Setup");
            panelGO.transform.position = new Vector3(-1.5f, 1.5f, 3f);
            panelGO.transform.rotation = Quaternion.Euler(0, 180f, 0);
            panelGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var canvas = panelGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 55;
            panelGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            panelGO.AddComponent<GraphicRaycaster>();
            var raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (raycasterType != null) panelGO.AddComponent(raycasterType);

            var rootRect = panelGO.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(400, 380);

            var bg = CreatePanel(panelGO.transform, "Background", new Vector2(0, 0), Vector2.zero, Vector2.one, new Color(0.08f, 0.12f, 0.18f, 0.98f));
            bg.GetComponent<Image>().raycastTarget = true;

            var title = CreateText(bg.transform, "TitleText", "Weather Control", 22, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));
            var currentWeather = CreateText(bg.transform, "CurrentWeatherText", "Current: Sunny", 18, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.84f));
            var description = CreateText(bg.transform, "DescriptionText", "Sunny: Increases temperature and plant growth rate. Soil moisture decreases gradually.", 14, TextAlignmentOptions.TopLeft, new Vector2(8, -8), new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.68f));
            description.GetComponent<TMP_Text>().enableWordWrapping = true;

            var sunnyBtn = CreateButton(bg.transform, "Sunny", new Vector2(-110, -180));
            var rainyBtn = CreateButton(bg.transform, "Rainy", new Vector2(0, -180));
            var stormBtn = CreateButton(bg.transform, "Storm", new Vector2(110, -180));
            ResizeButton(sunnyBtn, 100, 42);
            ResizeButton(rainyBtn, 100, 42);
            ResizeButton(stormBtn, 100, 42);
            sunnyBtn.GetComponentInChildren<TMP_Text>().text = "Sunny";
            rainyBtn.GetComponentInChildren<TMP_Text>().text = "Rainy";
            stormBtn.GetComponentInChildren<TMP_Text>().text = "Storm";

            var weatherMgr = CreateOrFindWeatherManager(hub);
            var uiCtrl = panelGO.AddComponent<WeatherUIController>();
            SetPrivateField(uiCtrl, "weatherManager", weatherMgr);
            SetPrivateField(uiCtrl, "titleText", title.GetComponent<TMP_Text>());
            SetPrivateField(uiCtrl, "currentWeatherText", currentWeather.GetComponent<TMP_Text>());
            SetPrivateField(uiCtrl, "descriptionText", description.GetComponent<TMP_Text>());
            SetPrivateField(uiCtrl, "sunnyButton", sunnyBtn.GetComponent<Button>());
            SetPrivateField(uiCtrl, "rainyButton", rainyBtn.GetComponent<Button>());
            SetPrivateField(uiCtrl, "stormButton", stormBtn.GetComponent<Button>());

            var uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(panelGO, uiLayer);
        }

        private static WeatherManager CreateOrFindWeatherManager(GameObject hub)
        {
            var mgr = hub.GetComponent<WeatherManager>();
            if (mgr == null) mgr = hub.AddComponent<WeatherManager>();
            var simMgr = hub.GetComponent<FarmSimulationManager>();
            var plantMgr = Object.FindFirstObjectByType<PlantGrowth.PlantGrowthManager>();
            SetPrivateField(mgr, "simulationManager", simMgr);
            SetPrivateField(mgr, "plantGrowthManager", plantMgr);
            return mgr;
        }

        private static void CreateFullWeatherSetup(GameObject hub)
        {
            var weatherMgr = hub.GetComponent<WeatherManager>();
            if (weatherMgr == null) return;

            // 1. Directional Light + LightningEffect
            var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light dirLight = null;
            foreach (var l in allLights)
            {
                if (l != null && l.type == LightType.Directional)
                {
                    dirLight = l;
                    break;
                }
            }
            if (dirLight == null)
            {
                var lightGO = new GameObject("Directional Light");
                Undo.RegisterCreatedObjectUndo(lightGO, "Weather Setup");
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.intensity = 1.2f;
                dirLight.color = new Color(1f, 0.98f, 0.95f);
            }
            SetPrivateField(weatherMgr, "directionalLight", dirLight);

            var lightning = dirLight.GetComponent<LightningEffect>();
            if (lightning == null) lightning = dirLight.gameObject.AddComponent<LightningEffect>();
            SetPrivateField(weatherMgr, "lightningEffect", lightning);

            // 2. Skybox materials (Sunny, Rainy, Storm)
            var (sunnySky, rainySky, stormSky) = CreateOrFindWeatherSkyboxes();

            // 2b. Sky overlay (keeps your sky, darkens for Rainy/Storm)
            var overlayGO = GameObject.Find("WeatherSkyOverlay");
            if (overlayGO == null)
            {
                overlayGO = CreateSkyOverlay();
                Undo.RegisterCreatedObjectUndo(overlayGO, "Weather Setup");
            }
            var skyOverlay = overlayGO.GetComponent<SkyOverlay>();
            if (skyOverlay != null)
                SetPrivateField(weatherMgr, "skyOverlay", skyOverlay);
            SetPrivateField(weatherMgr, "keepOriginalSky", true);

            // 2c. Cloud layer
            var cloudGO = GameObject.Find("WeatherCloudLayer");
            if (cloudGO == null)
            {
                cloudGO = CreateCloudLayer();
                Undo.RegisterCreatedObjectUndo(cloudGO, "Weather Setup");
            }
            var cloudLayer = cloudGO.GetComponent<CloudLayer>();
            if (cloudLayer != null)
                SetPrivateField(weatherMgr, "cloudLayer", cloudLayer);
            if (sunnySky != null) SetPrivateField(weatherMgr, "sunnySkybox", sunnySky);
            if (rainySky != null) SetPrivateField(weatherMgr, "rainySkybox", rainySky);
            if (stormSky != null) SetPrivateField(weatherMgr, "stormSkybox", stormSky);

            // 3. Rain Particle System
            var rainGO = GameObject.Find("WeatherRainParticles");
            if (rainGO == null)
            {
                rainGO = CreateRainParticleSystem();
                Undo.RegisterCreatedObjectUndo(rainGO, "Weather Setup");
            }
            rainGO.SetActive(false);
            var rainPS = rainGO.GetComponent<ParticleSystem>();
            if (rainPS != null)
                SetPrivateField(weatherMgr, "rainParticleSystem", rainPS);

            // 4. Weather Audio Sources (empty - user assigns clips)
            var audioRoot = GameObject.Find("WeatherAudio");
            if (audioRoot == null)
            {
                audioRoot = new GameObject("WeatherAudio");
                Undo.RegisterCreatedObjectUndo(audioRoot, "Weather Setup");
                audioRoot.transform.SetParent(hub.transform);
                audioRoot.transform.localPosition = Vector3.zero;
            }

            var sunny = audioRoot.transform.Find("SunnyAmbient")?.GetComponent<AudioSource>();
            var rainy = audioRoot.transform.Find("RainAmbient")?.GetComponent<AudioSource>();
            var storm = audioRoot.transform.Find("StormAmbient")?.GetComponent<AudioSource>();
            if (sunny == null) sunny = CreateWeatherAudioSource(audioRoot.transform, "SunnyAmbient");
            if (rainy == null) { rainy = CreateWeatherAudioSource(audioRoot.transform, "RainAmbient"); rainy.loop = true; }
            if (storm == null) { storm = CreateWeatherAudioSource(audioRoot.transform, "StormAmbient"); storm.loop = true; }
            SetPrivateField(weatherMgr, "sunnyAmbientSource", sunny);
            SetPrivateField(weatherMgr, "rainyAmbientSource", rainy);
            SetPrivateField(weatherMgr, "stormAmbientSource", storm);

            Debug.Log("[SmartFarm] Full weather setup complete: Light, Lightning, Sky, Rain, Audio.");
        }

        private static (Material sunny, Material rainy, Material storm) CreateOrFindWeatherSkyboxes()
        {
            EnsureDirectories();
            var shader = Shader.Find("Skybox/Procedural") ?? Shader.Find("Universal Render Pipeline/Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[SmartFarm] Procedural Skybox shader not found. Sky will not change with weather.");
                return (null, null, null);
            }

            var sunny = LoadOrCreateSkyboxMaterial("Assets/SmartFarm/WeatherSkyboxes/Skybox_Sunny.mat", shader, (mat) =>
            {
                mat.SetColor("_SkyTint", new Color(0.32f, 0.45f, 0.78f));  // Vibrant clear blue
                mat.SetColor("_GroundColor", new Color(0.62f, 0.55f, 0.42f));  // Warm golden horizon
                mat.SetFloat("_SunSize", 0.05f);
                mat.SetFloat("_SunSizeConvergence", 4f);
                mat.SetFloat("_AtmosphereThickness", 0.25f);  // Very clear
                mat.SetFloat("_Exposure", 1.6f);
            });

            var rainy = LoadOrCreateSkyboxMaterial("Assets/SmartFarm/WeatherSkyboxes/Skybox_Rainy.mat", shader, (mat) =>
            {
                mat.SetColor("_SkyTint", new Color(0.30f, 0.35f, 0.45f));  // Cool gray-blue
                mat.SetColor("_GroundColor", new Color(0.14f, 0.16f, 0.20f));  // No warm horizon
                mat.SetFloat("_SunSize", 0.001f);
                mat.SetFloat("_SunSizeConvergence", 20f);
                mat.SetFloat("_SunDisk", 0f); // hide sun disk
                mat.SetFloat("_AtmosphereThickness", 2.2f);
                mat.SetFloat("_Exposure", 0.38f);
            });

            var storm = LoadOrCreateSkyboxMaterial("Assets/SmartFarm/WeatherSkyboxes/Skybox_Storm.mat", shader, (mat) =>
            {
                mat.SetColor("_SkyTint", new Color(0.18f, 0.22f, 0.30f));  // Dark cold storm
                mat.SetColor("_GroundColor", new Color(0.06f, 0.07f, 0.10f));  // Almost black horizon
                mat.SetFloat("_SunSize", 0.001f);
                mat.SetFloat("_SunSizeConvergence", 20f);
                mat.SetFloat("_SunDisk", 0f); // hide sun disk
                mat.SetFloat("_AtmosphereThickness", 2.8f);
                mat.SetFloat("_Exposure", 0.24f);
            });

            return (sunny, rainy, storm);
        }

        private static GameObject CreateSkyOverlay()
        {
            var go = new GameObject("WeatherSkyOverlay");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = false;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();

            var root = new GameObject("OverlayRoot", typeof(RectTransform));
            root.transform.SetParent(go.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var img = root.AddComponent<Image>();
            img.color = new Color(0.35f, 0.4f, 0.5f, 0.5f);
            img.raycastTarget = false;

            var overlay = go.AddComponent<SkyOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("overlayImage").objectReferenceValue = img;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        private static GameObject CreateCloudLayer()
        {
            EnsureDirectories();
            var tex = CreateOrLoadCloudTexture("Assets/SmartFarm/WeatherSkyboxes/CloudTexture.png");
            var mat = CreateOrLoadCloudMaterial("Assets/SmartFarm/WeatherSkyboxes/CloudMaterial.mat", tex);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WeatherCloudLayer";
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one * 450f;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);

            var cloudLayer = go.AddComponent<CloudLayer>();
            cloudLayer.SetWeather(WeatherManager.WeatherType.Sunny);

            return go;
        }

        private static Texture2D CreateOrLoadCloudTexture(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            int size = 512;
            var tex = new Texture2D(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)size * 4f;
                float ny = y / (float)size * 4f;
                float n = Mathf.PerlinNoise(nx, ny) * 0.7f + Mathf.PerlinNoise(nx * 2f, ny * 2f) * 0.3f;
                float a = Mathf.Clamp01(n * 1.2f - 0.3f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            var png = tex.EncodeToPNG();
            var relPath = path.StartsWith("Assets/") ? path.Substring(7) : path;
            var fullPath = Path.Combine(Application.dataPath, relPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(fullPath, png);
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateOrLoadCloudMaterial(string path, Texture2D tex)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (tex != null && existing.HasProperty("_MainTex"))
                    existing.SetTexture("_MainTex", tex);
                return existing;
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) { Debug.LogWarning("[SmartFarm] No transparent shader for clouds."); return null; }

            var mat = new Material(shader);
            mat.name = "CloudMaterial";
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", new Color(1f, 1f, 1f, 0.25f));
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            mat.renderQueue = 2999;

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Material LoadOrCreateSkyboxMaterial(string path, Shader shader, System.Action<Material> configure)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                configure?.Invoke(existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            var mat = new Material(shader);
            mat.name = Path.GetFileNameWithoutExtension(path);
            configure?.Invoke(mat);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static GameObject CreateRainParticleSystem()
        {
            var go = new GameObject("WeatherRainParticles");
            go.transform.position = new Vector3(0, 18f, 0);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();

            var renderMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            if (renderMat == null) renderMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            if (renderMat == null)
            {
                var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null) renderMat = new Material(shader);
            }
            if (renderMat != null) renderer.material = renderMat;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.1f;

            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 26f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8000;
            main.startRotation = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(35f, 1f, 35f);
            shape.rotation = new Vector3(0f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 1200f;
            emission.enabled = true;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.85f, 0.9f, 0.95f), 0f), new GradientColorKey(new Color(0.7f, 0.78f, 0.88f), 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            // Wind-driven rain: sideways + downward for immediate visual direction.
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = 5f;
            velocityOverLifetime.y = -10f;
            velocityOverLifetime.z = 0f;

            return go;
        }

        private static AudioSource CreateWeatherAudioSource(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        private static void LinkWeatherPanelToHub(GameObject panel, GameObject hub)
        {
            var uiCtrl = panel.GetComponent<WeatherUIController>();
            var weatherMgr = hub.GetComponent<WeatherManager>();
            if (weatherMgr == null) weatherMgr = hub.AddComponent<WeatherManager>();
            var simMgr = hub.GetComponent<FarmSimulationManager>();
            var plantMgr = Object.FindFirstObjectByType<PlantGrowth.PlantGrowthManager>();
            SetPrivateField(weatherMgr, "simulationManager", simMgr);
            SetPrivateField(weatherMgr, "plantGrowthManager", plantMgr);
            if (uiCtrl != null) SetPrivateField(uiCtrl, "weatherManager", weatherMgr);
            CreateFullWeatherSetup(hub);
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

        private static void CreateOrFindWildHarvestField()
        {
            if (GameObject.Find("WildHarvestField") != null) return;

            var wheatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/WildHarvest/Prefabs/PlantInstance_Wheat.prefab");
            var cornPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PlantGrowth/WildHarvest/Prefabs/PlantInstance_Corn.prefab");
            if (wheatPrefab == null || cornPrefab == null)
            {
                Debug.LogWarning("[SmartFarm] Wild Harvest prefabs not found. Run Tools > Farm > Setup Wild Harvest Crops first.");
                return;
            }

            var root = new GameObject("WildHarvestField");
            Undo.RegisterCreatedObjectUndo(root, "Wild Harvest Field");
            root.transform.position = new Vector3(6f, 0f, 0f);

            const int cols = 3;
            const float spacing = 1.4f;

            for (int i = 0; i < cols; i++)
            {
                var wheat = (GameObject)PrefabUtility.InstantiatePrefab(wheatPrefab);
                if (wheat == null) continue;
                wheat.name = $"Wheat_{i + 1}";
                wheat.transform.SetParent(root.transform);
                wheat.transform.position = root.transform.position + new Vector3(i * spacing, 0f, 0f);
            }

            for (int i = 0; i < cols; i++)
            {
                var corn = (GameObject)PrefabUtility.InstantiatePrefab(cornPrefab);
                if (corn == null) continue;
                corn.name = $"Corn_{i + 1}";
                corn.transform.SetParent(root.transform);
                corn.transform.position = root.transform.position + new Vector3(i * spacing, 0f, spacing);
            }

            Debug.Log("[SmartFarm] Wild Harvest field spawned (3 Wheat + 3 Corn).");
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
        /// Creates rain, lightning, audio and wires all references to WeatherManager.
        /// Use when you have the panel but need visual/audio setup.
        /// </summary>
        public static void CreateFullWeatherSetupOnly()
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
            var weatherMgr = hub.GetComponent<WeatherManager>();
            if (weatherMgr == null)
            {
                Debug.LogWarning("[SmartFarm] WeatherManager not found on hub. Adding it. Run Create Weather Control Panel if you need the UI.");
                weatherMgr = hub.AddComponent<WeatherManager>();
                var simMgr = hub.GetComponent<FarmSimulationManager>();
                var plantMgr = Object.FindFirstObjectByType<PlantGrowth.PlantGrowthManager>();
                SetPrivateField(weatherMgr, "simulationManager", simMgr);
                SetPrivateField(weatherMgr, "plantGrowthManager", plantMgr);
            }
            CreateFullWeatherSetup(hub);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SmartFarm] Full weather setup (rain, lightning, audio) created and wired.");
        }

        /// <summary>
        /// Creates only the Weather Control Panel if missing.
        /// </summary>
        public static void CreateWeatherPanelOnly()
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
            CreateOrFindWeatherPanel(hub);
            CreateFullWeatherSetup(hub);
            EnableXRUIControllers();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SmartFarm] Weather Control Panel + full weather setup created.");
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
