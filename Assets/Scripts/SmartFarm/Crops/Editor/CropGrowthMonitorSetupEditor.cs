using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Editor
{
    /// <summary>
    /// One-click editor that builds the full <b>Crop Growth Monitor</b> system in
    /// the active scene:
    ///
    ///   1. Adds <see cref="CropGrowthMonitorManager"/> + <see cref="CropMonitorAlertSystem"/>
    ///      to the FarmSimulationHub (or a dedicated GameObject).
    ///   2. Spawns a futuristic world-space terminal (Canvas + dark green-neon UI)
    ///      next to the FarmDashboard.
    ///   3. Wires every UI element to <see cref="CropGrowthMonitorUI"/> /
    ///      <see cref="CropMonitorAlertPopupUI"/> via reflection.
    ///   4. Tags everything with the UI layer so XR ray interactors can press the buttons.
    ///
    /// Menu: Tools &gt; Smart Farm &gt; Crops &gt; Setup Crop Growth Monitor
    /// Also called automatically from <c>SmartFarmSetupEditor.FullSetup()</c>.
    /// </summary>
    public static class CropGrowthMonitorSetupEditor
    {
        private const string MonitorObjectName = "CropGrowthMonitor";

        // ── Theme ─────────────────────────────────────────────────────────────

        private static readonly Color BgDeep        = new Color(0.04f, 0.10f, 0.12f, 0.99f);
        private static readonly Color BgPanel       = new Color(0.06f, 0.14f, 0.17f, 0.97f);
        private static readonly Color BgCard        = new Color(0.09f, 0.18f, 0.22f, 0.95f);
        private static readonly Color BgBarTrack    = new Color(0.10f, 0.22f, 0.28f, 1.00f);
        private static readonly Color HeaderTint    = new Color(0.06f, 0.20f, 0.18f, 1.00f);
        private static readonly Color NeonGreen     = new Color(0.30f, 1.00f, 0.66f, 1.00f);
        private static readonly Color NeonGreenSoft = new Color(0.30f, 1.00f, 0.66f, 0.35f);
        private static readonly Color TextPrimary   = new Color(0.92f, 1.00f, 0.96f, 1.00f);
        private static readonly Color TextSecondary = new Color(0.65f, 0.85f, 0.78f, 1.00f);
        private static readonly Color BorderColor   = new Color(0.10f, 0.85f, 0.55f, 0.65f);

        // ─────────────────────────────────────────────────────────────────────
        //  Menu items
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Crops/Setup Crop Growth Monitor")]
        public static void SetupCropGrowthMonitor()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[CropMonitor] Stop Play mode before running setup.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[CropMonitor] Open a scene first.");
                return;
            }

            RunSetup();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[CropMonitor] Crop Growth Monitor setup complete!");
        }

        [MenuItem("Tools/Smart Farm/Crops/Rebuild Crop Growth Monitor")]
        public static void RebuildCropGrowthMonitor()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[CropMonitor] Stop Play mode first.");
                return;
            }

            var existing = GameObject.Find(MonitorObjectName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            RunSetup();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[CropMonitor] Monitor UI rebuilt.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public entry — also called from SmartFarmSetupEditor.FullSetup()
        // ─────────────────────────────────────────────────────────────────────

        public static void RunSetup()
        {
            // 1. Manager + Alert system live on the hub (next to GrowthManager / WeatherManager)
            var hub      = GameObject.Find("FarmSimulationHub");
            var managerHost = hub != null ? hub : EnsureStandaloneHost();

            var monitorMgr = managerHost.GetComponent<CropGrowthMonitorManager>();
            if (monitorMgr == null) monitorMgr = managerHost.AddComponent<CropGrowthMonitorManager>();

            var alertSys = managerHost.GetComponent<CropMonitorAlertSystem>();
            if (alertSys == null) alertSys = managerHost.AddComponent<CropMonitorAlertSystem>();

            WireMonitorManager(monitorMgr);
            WireAlertSystem(alertSys, monitorMgr);

            // 2. World-space monitor UI
            var monitorRoot = GameObject.Find(MonitorObjectName);
            if (monitorRoot == null)
                monitorRoot = BuildMonitorCanvas();

            BuildMonitorUI(monitorRoot, monitorMgr, alertSys);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject EnsureStandaloneHost()
        {
            var go = GameObject.Find("CropGrowthMonitorHost");
            if (go != null) return go;
            go = new GameObject("CropGrowthMonitorHost");
            Undo.RegisterCreatedObjectUndo(go, "Crop Monitor Host");
            return go;
        }

        private static void WireMonitorManager(CropGrowthMonitorManager mgr)
        {
            var growth  = Object.FindFirstObjectByType<GrowthManager>();
            var weather = Object.FindFirstObjectByType<WeatherManager>();
            SetField(mgr, "growthManager",  growth);
            SetField(mgr, "weatherManager", weather);
            EditorUtility.SetDirty(mgr);
        }

        private static void WireAlertSystem(CropMonitorAlertSystem sys, CropGrowthMonitorManager mgr)
        {
            SetField(sys, "monitor", mgr);
            EditorUtility.SetDirty(sys);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Canvas
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject BuildMonitorCanvas()
        {
            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            var go = new GameObject(MonitorObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Crop Growth Monitor");
            go.transform.position   = new Vector3(2.2f, 1.65f, 3f);
            go.transform.rotation   = Quaternion.Euler(0, 180f, 0);
            go.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 65;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            go.AddComponent<GraphicRaycaster>();
            var trackedType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedType != null) go.AddComponent(trackedType);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1100f, 720f);

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(go, uiLayer);

            return go;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Build UI
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildMonitorUI(GameObject root, CropGrowthMonitorManager monitorMgr, CropMonitorAlertSystem alertSys)
        {
            // Strip any previous content so re-running cleanly rebuilds the UI
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            // Background (deep dark)
            var bg = MakePanel(root.transform, "Background", Vector2.zero, Vector2.one, BgDeep);
            bg.GetComponent<Image>().raycastTarget = true;

            // Border ring (filled image, not raycast)
            var border = MakePanel(root.transform, "BorderGlow", Vector2.zero, Vector2.one, BorderColor);
            var borderImage = border.GetComponent<Image>();
            borderImage.raycastTarget = false;
            // Use a thin outline by inset: we approximate with 4 strips
            BuildBorderStrips(border.transform, BorderColor);

            // Header bar (top of monitor)
            var header = MakePanel(root.transform, "Header",
                new Vector2(0, 0.93f), new Vector2(1, 1f), HeaderTint);

            // Alert banner area sits between the header (0.93) and the data cards (0.85)
            // It's invisible by default; CropMonitorAlertPopupUI fades + slides it in.
            var titleText    = MakeText(header.transform, "TitleText", "CROP GROWTH MONITOR",
                26, TextAlignmentOptions.Left,  new Vector2(0.025f, 0.12f), new Vector2(0.55f, 0.88f), NeonGreen, true);
            var subtitleText = MakeText(header.transform, "SubtitleText", "Smart Agriculture Dashboard",
                15, TextAlignmentOptions.Right, new Vector2(0.55f, 0.12f), new Vector2(0.93f, 0.88f), TextSecondary);
            var statusLed    = MakeColoredCircle(header.transform, "StatusLed",
                new Vector2(0.95f, 0.4f), new Vector2(0.99f, 0.6f), NeonGreen);

            // ── Crop Card (left column) ────────────────────────────────────────
            var cropCard = MakePanel(root.transform, "CropCard",
                new Vector2(0.025f, 0.46f), new Vector2(0.32f, 0.85f), BgCard);

            var stageBadge = MakePanel(cropCard.transform, "StageBadge",
                new Vector2(0.10f, 0.65f), new Vector2(0.90f, 0.85f), NeonGreenSoft);
            var stageBadgeBg   = stageBadge.GetComponent<Image>();
            var stageText      = MakeText(stageBadge.transform, "StageText", "GROWING",
                20, TextAlignmentOptions.Center, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f), TextPrimary, true);

            var stageIcon      = MakeColoredCircle(cropCard.transform, "StageIcon",
                new Vector2(0.34f, 0.28f), new Vector2(0.66f, 0.6f), NeonGreen);

            var cropNameText   = MakeText(cropCard.transform, "CropNameText", "All Crops",
                30, TextAlignmentOptions.Center, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.26f), TextPrimary, true);
            var sampleText     = MakeText(cropCard.transform, "SampleText", "—",
                14, TextAlignmentOptions.Center, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.12f), TextSecondary);

            // ── Progress Meters (center column) ────────────────────────────────
            var meters = MakePanel(root.transform, "Meters",
                new Vector2(0.34f, 0.46f), new Vector2(0.67f, 0.85f), BgPanel);

            (Image growthFill,  TMP_Text growthValue) = BuildLabeledBar(meters.transform, "Growth",
                "GROWTH PROGRESS", 0.74f, 0.96f, NeonGreen);
            (Image healthFill,  TMP_Text healthValue) = BuildLabeledBar(meters.transform, "Health",
                "CROP HEALTH",     0.42f, 0.64f, NeonGreen);
            (Image waterFill,   TMP_Text waterValue)  = BuildLabeledBar(meters.transform, "Water",
                "WATER LEVEL",     0.10f, 0.32f, new Color(0.35f, 0.70f, 1.00f, 1f));

            // ── Right column: Weather card + Harvest timer ─────────────────────
            var rightCol = MakePanel(root.transform, "RightColumn",
                new Vector2(0.69f, 0.46f), new Vector2(0.975f, 0.85f), BgPanel);

            var weatherCardBg = MakePanel(rightCol.transform, "WeatherCard",
                new Vector2(0.06f, 0.50f), new Vector2(0.94f, 0.97f), BgCard);
            var weatherIcon   = MakeColoredCircle(weatherCardBg.transform, "WeatherIcon",
                new Vector2(0.10f, 0.45f), new Vector2(0.32f, 0.92f), NeonGreen);
            var weatherTitle  = MakeText(weatherCardBg.transform, "WeatherTitle", "SUNNY",
                26, TextAlignmentOptions.Left, new Vector2(0.36f, 0.55f), new Vector2(0.95f, 0.95f), TextPrimary, true);
            var weatherDesc   = MakeText(weatherCardBg.transform, "WeatherDesc",
                "Faster growth · soil moisture decreases.",
                14, TextAlignmentOptions.TopLeft, new Vector2(0.06f, 0.05f), new Vector2(0.95f, 0.50f), TextSecondary);
            weatherDesc.enableWordWrapping = true;

            var harvestCard   = MakePanel(rightCol.transform, "HarvestCard",
                new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.46f), BgCard);
            var harvestLabel  = MakeText(harvestCard.transform, "HarvestLabel", "HARVEST READY IN",
                14, TextAlignmentOptions.Center, new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.90f), TextSecondary, true);
            var harvestTimer  = MakeText(harvestCard.transform, "HarvestTimer", "00:00",
                52, TextAlignmentOptions.Center, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.65f), NeonGreen, true);

            // ── Bottom action bar ──────────────────────────────────────────────
            var actionBar = MakePanel(root.transform, "ActionBar",
                new Vector2(0.025f, 0.06f), new Vector2(0.975f, 0.42f), BgPanel);

            var prevBtn   = BuildButton(actionBar.transform, "PrevButton",  "◀  Previous",
                new Vector2(0.03f, 0.30f), new Vector2(0.24f, 0.70f),
                new Color(0.12f, 0.32f, 0.38f, 1f));
            var nextBtn   = BuildButton(actionBar.transform, "NextButton",  "Next  ▶",
                new Vector2(0.26f, 0.30f), new Vector2(0.47f, 0.70f),
                new Color(0.12f, 0.32f, 0.38f, 1f));
            var harvestBt = BuildButton(actionBar.transform, "HarvestButton", "HARVEST NOW",
                new Vector2(0.51f, 0.30f), new Vector2(0.78f, 0.70f),
                new Color(0.16f, 0.65f, 0.30f, 1f));
            var resetBtn  = BuildButton(actionBar.transform, "ResetViewButton", "Wheat",
                new Vector2(0.80f, 0.30f), new Vector2(0.97f, 0.70f),
                new Color(0.20f, 0.30f, 0.40f, 1f));

            // ── Alert popup (top-right overlay inside the canvas) ──────────────
            var popupRoot = BuildAlertPopup(root.transform);

            // ── Hook up the UI MonoBehaviour ───────────────────────────────────
            var ui = root.GetComponent<CropGrowthMonitorUI>();
            if (ui == null) ui = root.AddComponent<CropGrowthMonitorUI>();

            SetField(ui, "monitor",                monitorMgr);
            SetField(ui, "titleText",              titleText);
            SetField(ui, "subtitleText",           subtitleText);
            SetField(ui, "statusLed",              statusLed);
            SetField(ui, "borderImage",            borderImage);

            SetField(ui, "cropNameText",           cropNameText);
            SetField(ui, "stageText",              stageText);
            SetField(ui, "stageBadgeBg",           stageBadgeBg);
            SetField(ui, "stageIconImage",         stageIcon);
            SetField(ui, "sampleText",             sampleText);

            SetField(ui, "growthBarFill",          growthFill);
            SetField(ui, "growthValueText",        growthValue);
            SetField(ui, "healthBarFill",          healthFill);
            SetField(ui, "healthValueText",        healthValue);
            SetField(ui, "waterBarFill",           waterFill);
            SetField(ui, "waterValueText",         waterValue);

            SetField(ui, "weatherCardBg",          weatherCardBg.GetComponent<Image>());
            SetField(ui, "weatherIconImage",       weatherIcon);
            SetField(ui, "weatherTitleText",       weatherTitle);
            SetField(ui, "weatherDescriptionText", weatherDesc);

            SetField(ui, "harvestTimerText",       harvestTimer);
            SetField(ui, "harvestLabelText",       harvestLabel);

            SetField(ui, "previousButton",         prevBtn);
            SetField(ui, "nextButton",             nextBtn);
            SetField(ui, "harvestButton",          harvestBt);
            SetField(ui, "resetViewButton",        resetBtn);
            EditorUtility.SetDirty(ui);

            // Popup component on the popup root
            var popupComp = popupRoot.gameObject.GetComponent<CropMonitorAlertPopupUI>();
            if (popupComp == null) popupComp = popupRoot.gameObject.AddComponent<CropMonitorAlertPopupUI>();

            SetField(popupComp, "alertSystem",     alertSys);
            SetField(popupComp, "popupRoot",       popupRoot);
            SetField(popupComp, "canvasGroup",     popupRoot.GetComponent<CanvasGroup>());
            SetField(popupComp, "backgroundImage", popupRoot.Find("Background").GetComponent<Image>());
            SetField(popupComp, "leftAccentImage", popupRoot.Find("Accent").GetComponent<Image>());
            SetField(popupComp, "iconImage",       popupRoot.Find("Icon").GetComponent<Image>());
            SetField(popupComp, "titleText",       popupRoot.Find("TitleText").GetComponent<TMP_Text>());
            SetField(popupComp, "messageText",     popupRoot.Find("MessageText").GetComponent<TMP_Text>());
            EditorUtility.SetDirty(popupComp);

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(root, uiLayer);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Border strips (fake outline using 4 thin Image edges)
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildBorderStrips(Transform parent, Color color)
        {
            const float thickness = 0.006f;
            MakePanel(parent, "BorderTop",    new Vector2(0f, 1f - thickness), new Vector2(1f, 1f), color).GetComponent<Image>().raycastTarget = false;
            MakePanel(parent, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, thickness), color).GetComponent<Image>().raycastTarget = false;
            MakePanel(parent, "BorderLeft",   new Vector2(0f, 0f), new Vector2(thickness, 1f), color).GetComponent<Image>().raycastTarget = false;
            MakePanel(parent, "BorderRight",  new Vector2(1f - thickness, 0f), new Vector2(1f, 1f), color).GetComponent<Image>().raycastTarget = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Bar builder (track + fill + label + value)
        // ─────────────────────────────────────────────────────────────────────

        private static (Image fill, TMP_Text value) BuildLabeledBar(Transform parent, string id,
            string label, float minY, float maxY, Color fillColor)
        {
            // Title
            MakeText(parent, id + "Label", label,
                14, TextAlignmentOptions.Left,
                new Vector2(0.04f, maxY - 0.05f), new Vector2(0.55f, maxY),
                TextSecondary, true);

            // Value % (right side)
            var valueText = MakeText(parent, id + "Value", "0%",
                22, TextAlignmentOptions.Right,
                new Vector2(0.55f, maxY - 0.06f), new Vector2(0.96f, maxY),
                TextPrimary, true);

            // Track + fill
            var track = MakePanel(parent, id + "Track",
                new Vector2(0.04f, minY + 0.01f), new Vector2(0.96f, maxY - 0.06f),
                BgBarTrack);
            var trackImage = track.GetComponent<Image>();
            trackImage.raycastTarget = false;

            var fillContainer = MakePanel(track.transform, id + "Fill",
                Vector2.zero, Vector2.one, fillColor);
            var fillImage = fillContainer.GetComponent<Image>();
            fillImage.type        = Image.Type.Filled;
            fillImage.fillMethod  = Image.FillMethod.Horizontal;
            fillImage.fillOrigin  = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount  = 0.5f;
            fillImage.raycastTarget = false;

            return (fillImage, valueText);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Alert popup builder
        // ─────────────────────────────────────────────────────────────────────

        private static RectTransform BuildAlertPopup(Transform parent)
        {
            // Banner anchored to the strip BELOW the header bar (between y=0.86 and y=0.92).
            // It spans most of the dashboard width so the alert reads like a real-world
            // smart-farm notification ribbon and never overlaps the data cards.
            var popup = new GameObject("AlertPopup", typeof(RectTransform));
            popup.transform.SetParent(parent, false);
            var rt = (RectTransform)popup.transform;
            rt.anchorMin        = new Vector2(0.04f, 0.86f);
            rt.anchorMax        = new Vector2(0.96f, 0.925f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.offsetMin        = rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            popup.AddComponent<CanvasGroup>();

            // Background card
            var bg = MakePanel(rt, "Background", Vector2.zero, Vector2.one, new Color(0.04f, 0.10f, 0.13f, 0.95f));
            bg.GetComponent<Image>().raycastTarget = false;

            // Left vertical accent strip
            var accent = MakePanel(rt, "Accent",
                new Vector2(0f, 0f), new Vector2(0.012f, 1f),
                NeonGreen);
            accent.GetComponent<Image>().raycastTarget = false;

            // Small icon dot at the very left of the banner
            MakeColoredCircle(rt, "Icon",
                new Vector2(0.018f, 0.18f), new Vector2(0.05f, 0.82f),
                NeonGreen);

            // Wide ribbon: title fills the left section, message fills the right section
            MakeText(rt, "TitleText", "Alert",
                18, TextAlignmentOptions.Left,
                new Vector2(0.06f, 0.10f), new Vector2(0.30f, 0.92f),
                NeonGreen, true);
            var msg = MakeText(rt, "MessageText", string.Empty,
                14, TextAlignmentOptions.Left,
                new Vector2(0.30f, 0.10f), new Vector2(0.985f, 0.92f),
                TextSecondary);
            msg.enableWordWrapping = false;
            msg.overflowMode       = TextOverflowModes.Ellipsis;

            return rt;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Primitive builders
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject MakePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static TMP_Text MakeText(Transform parent, string name, string value, float size,
            TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Color color, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text          = value;
            t.fontSize      = size;
            t.alignment     = align;
            t.color         = color;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        private static Image MakeColoredCircle(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            // Use built-in UI knob sprite as a circle
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            return img;
        }

        private static Button BuildButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.AddComponent<TextMeshProUGUI>();
            t.text          = label;
            t.fontSize      = 18;
            t.fontStyle     = FontStyles.Bold;
            t.color         = TextPrimary;
            t.alignment     = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return btn;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Utilities
        // ─────────────────────────────────────────────────────────────────────

        private static void SetField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(obj, value);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
