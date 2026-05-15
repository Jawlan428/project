using System.Collections.Generic;
using System.Reflection;
using SmartFarm.Irrigation;
using SmartFarm.Irrigation.UI;
using SmartFarm.Irrigation.Sustainability.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.Sustainability.Editor
{
    /// <summary>
    /// One-click installer for the Sustainability Monitor.
    ///
    /// What it does:
    ///   1. Ensures every sub-module (<see cref="WaterSavingTracker"/>,
    ///      <see cref="IrrigationEfficiencySystem"/>, <see cref="WeatherOptimizationSystem"/>,
    ///      <see cref="SustainabilityScoreSystem"/>, <see cref="EcoAlertManager"/>) plus the
    ///      <see cref="SustainabilityWaterManager"/> facade exists on the same hub as the
    ///      existing Smart Irrigation system.
    ///   2. Cross-wires those modules to the existing IrrigationZoneManager + WeatherManager.
    ///   3. Adds a new SUSTAINABILITY tab to the Smart Irrigation Tablet, builds the page
    ///      (animated counter, circular gauge, recommendation banner, score badge, eco
    ///      alert list, action buttons) and re-flows the existing 4-tab bar to fit 5 tabs.
    ///   4. Spawns the Eco Alert popup as an overlay on the tablet.
    ///
    /// Menu: <i>Tools &gt; Smart Farm &gt; Setup Sustainability Monitor</i>
    /// </summary>
    public static class SustainabilityMonitorSetupEditor
    {
        // ── Theme ────────────────────────────────────────────────────────────

        private static readonly Color BgDeep        = new Color(0.04f, 0.10f, 0.10f, 0.99f);
        private static readonly Color BgPanel       = new Color(0.06f, 0.14f, 0.16f, 0.97f);
        private static readonly Color BgCard        = new Color(0.07f, 0.16f, 0.18f, 0.96f);
        private static readonly Color BgBarTrack    = new Color(0.10f, 0.22f, 0.24f, 1.00f);
        private static readonly Color AccentGreen   = new Color(0.30f, 0.85f, 0.55f, 1.00f);
        private static readonly Color AccentGreenSoft = new Color(0.18f, 0.42f, 0.30f, 1.00f);
        private static readonly Color AccentBlue    = new Color(0.40f, 0.75f, 1.00f, 1.00f);
        private static readonly Color AccentAmber   = new Color(0.95f, 0.78f, 0.25f, 1.00f);
        private static readonly Color AccentRed     = new Color(0.92f, 0.30f, 0.25f, 1.00f);
        private static readonly Color TabBg         = new Color(0.08f, 0.16f, 0.20f, 1.00f);
        private static readonly Color TextPrimary   = new Color(0.94f, 1.00f, 0.96f, 1.00f);
        private static readonly Color TextSecondary = new Color(0.65f, 0.85f, 0.78f, 1.00f);

        // ─────────────────────────────────────────────────────────────────────
        //  Menu
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Setup Sustainability Monitor", priority = 5)]
        public static void SetupSustainabilityMonitor()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SustainabilityMonitor] Stop Play mode before running setup.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SustainabilityMonitor] Open a scene first.");
                return;
            }

            GameObject hub = EnsureHub();
            try { BuildSustainabilityTab(hub); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SustainabilityMonitor] Tab build failed: {ex.Message}\n{ex.StackTrace}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[SustainabilityMonitor] Setup complete!\n" +
                      "• Sub-modules wired onto the irrigation hub.\n" +
                      "• Sustainability tab + page added to the Smart Irrigation Tablet.\n" +
                      "• Eco alert popup attached as an overlay.\n" +
                      "Press Play to test.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Hub
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject EnsureHub()
        {
            var hub = GameObject.Find("SmartIrrigationHub");
            if (hub == null) hub = GameObject.Find("FarmSimulationHub");
            if (hub == null)
            {
                hub = new GameObject("SmartIrrigationHub");
                Undo.RegisterCreatedObjectUndo(hub, "SmartIrrigationHub");
            }

            // Ensure the existing tablet manager is on the hub (it's required for cross-wiring).
            var tabletMgr = hub.GetComponent<SmartIrrigationTabletManager>();
            if (tabletMgr == null)
            {
                Debug.LogWarning("[SustainabilityMonitor] SmartIrrigationTabletManager not found on the hub. " +
                                 "Run 'Tools > Smart Farm > Setup Smart Irrigation Tablet' first for the full system.");
            }

            // Add every Sustainability sub-module to the hub if missing.
            var saver   = GetOrAdd<WaterSavingTracker>(hub);
            var eff     = GetOrAdd<IrrigationEfficiencySystem>(hub);
            var weather = GetOrAdd<WeatherOptimizationSystem>(hub);
            var score   = GetOrAdd<SustainabilityScoreSystem>(hub);
            var alerts  = GetOrAdd<EcoAlertManager>(hub);
            var manager = GetOrAdd<SustainabilityWaterManager>(hub);

            // Auto-wire references through reflection (private fields).
            SetField(manager, "waterSaver",         saver);
            SetField(manager, "efficiencySystem",   eff);
            SetField(manager, "weatherOptimization",weather);
            SetField(manager, "scoreSystem",        score);
            SetField(manager, "ecoAlerts",          alerts);
            SetField(manager, "tabletManager",      tabletMgr);

            return hub;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Tablet integration
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildSustainabilityTab(GameObject hub)
        {
            var tablet = GameObject.Find("SmartIrrigationTablet");
            if (tablet == null)
            {
                Debug.LogWarning("[SustainabilityMonitor] SmartIrrigationTablet not found — run " +
                                 "'Tools > Smart Farm > Setup Smart Irrigation Tablet' first.");
                return;
            }

            var ctrl = tablet.GetComponent<SmartIrrigationTabletAppController>();
            var tabBar = tablet.transform.Find("TabBar");
            var content = tablet.transform.Find("Content");
            if (ctrl == null || tabBar == null || content == null)
            {
                Debug.LogError("[SustainabilityMonitor] Smart Irrigation Tablet is malformed (missing TabBar/Content/controller).");
                return;
            }

            // Re-flow the tab bar to 5 tabs.
            var overviewTab  = (Button)GetFieldValue(ctrl, "overviewTabButton");
            var zonesTab     = (Button)GetFieldValue(ctrl, "zonesTabButton");
            var analyticsTab = (Button)GetFieldValue(ctrl, "analyticsTabButton");
            var alertsTab    = (Button)GetFieldValue(ctrl, "alertsTabButton");

            ReflowTabBar(overviewTab, zonesTab, analyticsTab, alertsTab);

            // Build or find the Sustainability tab button.
            var sustainabilityTab = tabBar.Find("SustainabilityTab")?.GetComponent<Button>();
            if (sustainabilityTab == null)
            {
                sustainabilityTab = BuildTabButton(tabBar, "SustainabilityTab", "SUSTAIN",
                    new Vector2(0.808f, 0.18f), new Vector2(0.985f, 0.82f));
            }

            // Remove any prior page from a previous run so we re-build clean.
            var existingPage = content.Find("SustainabilityPage");
            if (existingPage != null) Undo.DestroyObjectImmediate(existingPage.gameObject);

            // Create the new page.
            var pageGO = new GameObject("SustainabilityPage", typeof(RectTransform));
            pageGO.transform.SetParent(content, false);
            var pageRT = (RectTransform)pageGO.transform;
            pageRT.anchorMin = Vector2.zero; pageRT.anchorMax = Vector2.one;
            pageRT.offsetMin = pageRT.offsetMax = Vector2.zero;
            pageGO.SetActive(false);

            BuildSustainabilityPageContent(pageRT, hub, out var pageUI);

            // Wire the new tab + page into the existing controller via reflection.
            SetField(ctrl, "sustainabilityTabButton", sustainabilityTab);
            SetField(ctrl, "sustainabilityPage",     pageGO);

            // Hook tab click so the new button switches pages even though the controller
            // wired listeners during Start() of the FIRST setup run (no overwrite issue
            // since we re-create the button each rebuild).
            sustainabilityTab.onClick.RemoveAllListeners();
            // Capture by closure: SetActivePage is public on the controller
            var ctrlCapture = ctrl;
            var pageCapture = pageGO;
            sustainabilityTab.onClick.AddListener(() => ctrlCapture.SetActivePage(pageCapture));

            // Make sure the existing tabs ALSO deactivate the new page. Since the
            // controller's SetActivePage already checks 'page == sustainabilityPage'
            // after our SetField above, this works on subsequent clicks.

            // Tag layer for XR rays
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(pageGO, uiLayer);
            if (uiLayer >= 0) SetLayerRecursive(sustainabilityTab.gameObject, uiLayer);

            // Eco alert popup overlay
            BuildEcoAlertOverlay(tablet.transform, hub);

            Selection.activeGameObject = pageGO;
        }

        private static void ReflowTabBar(Button overview, Button zones, Button analytics, Button alerts)
        {
            // 5 columns equally spaced — keep the same gutters as the original 4-tab build.
            (Button btn, float min, float max)[] layout =
            {
                (overview,  0.020f, 0.196f),
                (zones,     0.205f, 0.381f),
                (analytics, 0.390f, 0.566f),
                (alerts,    0.575f, 0.751f),
            };

            for (int i = 0; i < layout.Length; i++)
            {
                var btn = layout[i].btn;
                if (btn == null) continue;
                var rt = btn.GetComponent<RectTransform>();
                if (rt == null) continue;
                rt.anchorMin = new Vector2(layout[i].min, rt.anchorMin.y);
                rt.anchorMax = new Vector2(layout[i].max, rt.anchorMax.y);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Page content
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildSustainabilityPageContent(RectTransform page, GameObject hub,
            out SustainabilityMonitorPageUI ui)
        {
            var manager  = hub.GetComponent<SustainabilityWaterManager>();
            var ecoAlerts = hub.GetComponent<EcoAlertManager>();

            // ─── Background card + animated droplets ────────────────────────
            var bgCard = MakePanel(page, "Background",
                new Vector2(0f, 0f), new Vector2(1f, 1f), BgDeep);
            bgCard.GetComponent<Image>().raycastTarget = false;

            var dropletsHost = MakePanel(page, "Droplets",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0f));
            dropletsHost.GetComponent<Image>().raycastTarget = false;
            var droplets = dropletsHost.AddComponent<WaterDropletAnimator>();
            droplets.SetReferences((RectTransform)dropletsHost.transform,
                AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                new Color(0.55f, 0.85f, 1f, 0.55f));

            // ─── Top row: Water Saved Today + Sustainability Score ──────────
            // Water-Saved card
            var savedCard = MakePanel(page, "WaterSavedCard",
                new Vector2(0.020f, 0.700f), new Vector2(0.485f, 0.965f), BgCard);
            MakeText(savedCard.transform, "Title", "WATER SAVED TODAY", 16,
                TextAlignmentOptions.Left, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.92f),
                AccentGreen, true);
            var counterText = MakeText(savedCard.transform, "CounterText", "0L", 60,
                TextAlignmentOptions.Left, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.72f),
                TextPrimary, true);
            var subtitleText = MakeText(savedCard.transform, "Subtitle",
                "Auto-saved by smart irrigation", 14,
                TextAlignmentOptions.Left, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.18f),
                TextSecondary);
            var counter = savedCard.AddComponent<AnimatedNumberCounter>();
            counter.SetReferences(counterText, "", "L");

            // Score badge card
            var scoreCard = MakePanel(page, "ScoreCard",
                new Vector2(0.500f, 0.700f), new Vector2(0.980f, 0.965f), BgCard);
            MakeText(scoreCard.transform, "Title", "SUSTAINABILITY SCORE", 16,
                TextAlignmentOptions.Left, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.92f),
                AccentGreen, true);
            var scoreText = MakeText(scoreCard.transform, "ScoreText", "0%", 56,
                TextAlignmentOptions.Left, new Vector2(0.05f, 0.18f), new Vector2(0.62f, 0.72f),
                TextPrimary, true);
            // Grade badge
            var badgeGO = MakePanel(scoreCard.transform, "GradeBadge",
                new Vector2(0.62f, 0.20f), new Vector2(0.95f, 0.70f), AccentGreen);
            var gradeText = MakeText(badgeGO.transform, "Grade", "A", 64,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Color.white, true);
            // Subtitle
            MakeText(scoreCard.transform, "Subtitle", "Eco-friendly performance", 14,
                TextAlignmentOptions.Left,
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.18f),
                TextSecondary);

            // ─── Middle row: Efficiency gauge + Recommendation + Weather ────
            var effCard = MakePanel(page, "EfficiencyCard",
                new Vector2(0.020f, 0.350f), new Vector2(0.350f, 0.685f), BgCard);
            MakeText(effCard.transform, "Title", "IRRIGATION EFFICIENCY", 14,
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f),
                AccentGreen, true);
            var efficiencyIndicator = BuildCircularIndicator(effCard.transform, "Indicator",
                new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.84f),
                "Efficiency", AccentGreen);

            var effBarCard = MakePanel(effCard.transform, "EfficiencyBar",
                new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.12f), BgBarTrack);
            effBarCard.GetComponent<Image>().raycastTarget = false;
            var effBarFill = MakePanel(effBarCard.transform, "Fill",
                Vector2.zero, Vector2.one, AccentGreen);
            var effBarFillImg = effBarFill.GetComponent<Image>();
            effBarFillImg.type = Image.Type.Filled;
            effBarFillImg.fillMethod = Image.FillMethod.Horizontal;
            effBarFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            effBarFillImg.fillAmount = 0.85f;
            effBarFillImg.raycastTarget = false;
            var shineGO = MakePanel(effBarCard.transform, "Shine",
                new Vector2(0f, 0f), new Vector2(0.18f, 1f), new Color(1f, 1f, 1f, 0.18f));
            shineGO.GetComponent<Image>().raycastTarget = false;
            var effBar = effBarCard.AddComponent<AnimatedFlowBar>();
            effBar.SetReferences(effBarFillImg, effBarCard.GetComponent<Image>(),
                shineGO.GetComponent<RectTransform>(), shineGO.GetComponent<Image>());

            // Recommendation banner
            var recCard = MakePanel(page, "RecommendationCard",
                new Vector2(0.365f, 0.530f), new Vector2(0.980f, 0.685f), BgCard);
            var recAccent = MakePanel(recCard.transform, "Accent",
                new Vector2(0f, 0f), new Vector2(0.012f, 1f), AccentBlue);
            recAccent.GetComponent<Image>().raycastTarget = false;
            MakeText(recCard.transform, "Title", "SMART WATER RECOMMENDATION", 14,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.72f), new Vector2(0.98f, 0.92f),
                AccentGreen, true);
            var recommendationText = MakeText(recCard.transform, "Message",
                "Rain expected tomorrow. Reduce irrigation.", 18,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.10f), new Vector2(0.98f, 0.72f),
                TextPrimary);
            recommendationText.textWrappingMode = TextWrappingModes.Normal;

            // Weather pill
            var weatherCard = MakePanel(page, "WeatherCard",
                new Vector2(0.365f, 0.350f), new Vector2(0.980f, 0.520f), BgCard);
            MakeText(weatherCard.transform, "Title", "WEATHER OPTIMIZATION", 14,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.65f), new Vector2(0.98f, 0.92f),
                AccentGreen, true);
            var weatherPill = MakePanel(weatherCard.transform, "Pill",
                new Vector2(0.04f, 0.18f), new Vector2(0.48f, 0.55f), AccentBlue);
            var weatherText = MakeText(weatherPill.transform, "Text", "Sunny  ·  82%", 18,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Color.white, true);
            MakeText(weatherCard.transform, "Note",
                "Auto-balanced based on real-time weather data.", 13,
                TextAlignmentOptions.Left,
                new Vector2(0.50f, 0.18f), new Vector2(0.98f, 0.58f),
                TextSecondary);

            // ─── Bottom row: Action buttons + Eco Alerts list ───────────────
            // Action buttons
            var autoBtn = BuildButton(page, "AutoIrrigationButton",
                new Vector2(0.020f, 0.245f), new Vector2(0.350f, 0.330f),
                AccentGreenSoft);
            var autoLabel = MakeText(autoBtn.transform, "Label", "AUTO IRRIGATION ON", 16,
                TextAlignmentOptions.Center,
                new Vector2(0.12f, 0.20f), new Vector2(0.98f, 0.80f),
                Color.white, true);
            var autoLed = MakeColoredCircle(autoBtn.transform, "Led",
                new Vector2(0.03f, 0.30f), new Vector2(0.10f, 0.70f),
                AccentGreen);
            AttachPokeFeedback(autoBtn);

            var resetBtn = BuildButton(page, "ResetButton",
                new Vector2(0.020f, 0.150f), new Vector2(0.350f, 0.235f),
                new Color(0.18f, 0.32f, 0.40f, 1f));
            MakeText(resetBtn.transform, "Label", "RESET ANALYTICS", 16,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Color.white, true);
            AttachPokeFeedback(resetBtn);

            var detailsBtn = BuildButton(page, "DetailsButton",
                new Vector2(0.020f, 0.055f), new Vector2(0.350f, 0.140f),
                new Color(0.10f, 0.22f, 0.30f, 1f));
            MakeText(detailsBtn.transform, "Label", "VIEW DETAILS", 16,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                AccentGreen, true);
            AttachPokeFeedback(detailsBtn);

            // Details panel (initially hidden)
            var detailsPanel = MakePanel(page, "DetailsPanel",
                new Vector2(0.020f, 0.055f), new Vector2(0.350f, 0.330f),
                new Color(0.04f, 0.10f, 0.13f, 0.97f));
            detailsPanel.SetActive(false);
            MakeText(detailsPanel.transform, "DetailsTitle", "SUSTAINABILITY DETAILS", 15,
                TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.97f),
                AccentGreen, true);
            MakeText(detailsPanel.transform, "DetailsBody",
                "<color=#9FE2C7>Score weights:</color>\n" +
                "Efficiency 45% · Weather 30% · Savings 25%\n\n" +
                "<color=#9FE2C7>Savings baseline:</color>\n" +
                "6 L / zone / second of always-on irrigation\n\n" +
                "<color=#9FE2C7>Auto irrigation:</color>\n" +
                "Drives every zone into Auto mode.", 13,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.82f),
                TextSecondary);

            // Eco alerts list (right column)
            var alertsCard = MakePanel(page, "EcoAlertsCard",
                new Vector2(0.365f, 0.055f), new Vector2(0.980f, 0.330f), BgCard);
            MakeText(alertsCard.transform, "Title", "ECO ALERTS", 14,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.83f), new Vector2(0.98f, 0.95f),
                AccentGreen, true);
            BuildScrollList(alertsCard.transform,
                new Vector2(0.04f, 0.04f), new Vector2(0.98f, 0.82f),
                out var listRoot, out var emptyState);

            // Eco alert template
            var template = BuildEcoAlertItemTemplate(page);

            // Wire the page UI
            ui = page.gameObject.AddComponent<SustainabilityMonitorPageUI>();
            ui.SetReferences(
                manager,
                scoreText, gradeText, badgeGO.GetComponent<Image>(),
                weatherText, weatherPill.GetComponent<Image>(),
                counter, subtitleText,
                efficiencyIndicator, effBar,
                recommendationText, recAccent.GetComponent<Image>(),
                autoBtn, autoLabel, autoLed,
                resetBtn, detailsBtn, detailsPanel,
                listRoot, template, emptyState);
        }

        private static EcoAlertItemUI BuildEcoAlertItemTemplate(Transform pageRoot)
        {
            var item = new GameObject("EcoAlertTemplate", typeof(RectTransform));
            item.transform.SetParent(pageRoot, false);
            var rt = (RectTransform)item.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 56f);

            var bg = item.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.16f, 0.18f, 0.96f);
            var le = item.AddComponent<LayoutElement>();
            le.preferredHeight = 56f;
            le.minHeight = 50f;

            var accent = MakePanel(item.transform, "Accent",
                new Vector2(0f, 0f), new Vector2(0.012f, 1f),
                AccentGreen);
            accent.GetComponent<Image>().raycastTarget = false;

            var title = MakeText(item.transform, "Title", "Eco alert", 15,
                TextAlignmentOptions.Left,
                new Vector2(0.030f, 0.50f), new Vector2(0.85f, 0.95f),
                TextPrimary, true);
            var msg = MakeText(item.transform, "Message", "Message", 12,
                TextAlignmentOptions.Left,
                new Vector2(0.030f, 0.06f), new Vector2(0.85f, 0.50f),
                TextSecondary);
            var ts = MakeText(item.transform, "Timestamp", "00:00", 11,
                TextAlignmentOptions.Right,
                new Vector2(0.85f, 0.55f), new Vector2(0.99f, 0.95f),
                TextSecondary);

            var ui = item.AddComponent<EcoAlertItemUI>();
            ui.SetReferences(accent.GetComponent<Image>(), bg, title, msg, ts);

            item.SetActive(false);
            return ui;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Eco alert popup overlay
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildEcoAlertOverlay(Transform tablet, GameObject hub)
        {
            var ecoAlerts = hub.GetComponent<EcoAlertManager>();

            var existing = tablet.Find("EcoAlertPopup");
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var popup = new GameObject("EcoAlertPopup", typeof(RectTransform));
            popup.transform.SetParent(tablet, false);
            var rt = (RectTransform)popup.transform;
            // Float the popup ABOVE the tablet so it never overlaps the
            // OVERVIEW / ZONES / ANALYTICS / ALERTS / SUSTAIN tab buttons.
            // Anchoring to the tablet's top edge with a bottom-pivoted popup
            // makes it sit just above the header, like a real toast banner.
            rt.anchorMin       = new Vector2(0.15f, 1f);
            rt.anchorMax       = new Vector2(0.85f, 1f);
            rt.pivot           = new Vector2(0.5f, 0f);
            rt.sizeDelta       = new Vector2(0f, 70f);
            rt.anchoredPosition = new Vector2(0f, 12f);

            var bg = popup.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.12f, 0.14f, 0.96f);
            bg.raycastTarget = false;
            var cg = popup.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            var accent = MakePanel(popup.transform, "Accent",
                new Vector2(0f, 0f), new Vector2(0.012f, 1f),
                AccentGreen);
            accent.GetComponent<Image>().raycastTarget = false;

            var title = MakeText(popup.transform, "Title", "Eco event", 16,
                TextAlignmentOptions.Left,
                new Vector2(0.030f, 0.50f), new Vector2(0.98f, 0.95f),
                Color.white, true);
            var msg = MakeText(popup.transform, "Message", "Eco message", 13,
                TextAlignmentOptions.Left,
                new Vector2(0.030f, 0.05f), new Vector2(0.98f, 0.50f),
                TextSecondary);

            var ui = popup.AddComponent<EcoAlertPopupUI>();
            ui.SetReferences(ecoAlerts, rt, cg,
                accent.GetComponent<Image>(), bg, title, msg);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Widget builders (mirrors SmartIrrigationTabletSetupEditor patterns)
        // ─────────────────────────────────────────────────────────────────────

        private static CircularWaterIndicator BuildCircularIndicator(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string label, Color tint)
        {
            var card = MakePanel(parent, name, anchorMin, anchorMax, new Color(0f, 0f, 0f, 0f));
            card.GetComponent<Image>().raycastTarget = false;

            var track = MakePanel(card.transform, "Track",
                new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f),
                new Color(0.12f, 0.22f, 0.26f, 1f));
            var trackImg = track.GetComponent<Image>();
            trackImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            trackImg.raycastTarget = false;

            var fill = MakePanel(card.transform, "Fill",
                new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f),
                tint);
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite     = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = true;
            fillImg.fillAmount = 0.85f;
            fillImg.raycastTarget = false;

            var value = MakeText(card.transform, "ValueText", "0%", 30,
                TextAlignmentOptions.Center,
                new Vector2(0.18f, 0.40f), new Vector2(0.82f, 0.62f),
                TextPrimary, true);
            var labelText = MakeText(card.transform, "LabelText", label, 12,
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.20f),
                TextSecondary, true);

            var indicator = card.AddComponent<CircularWaterIndicator>();
            indicator.SetReferences(trackImg, fillImg, value, labelText);
            return indicator;
        }

        private static RectTransform BuildScrollList(Transform parent,
            Vector2 anchorMin, Vector2 anchorMax,
            out RectTransform listRoot, out TMP_Text emptyState)
        {
            var scrollGO = new GameObject("Scroll", typeof(RectTransform));
            scrollGO.transform.SetParent(parent, false);
            var scrollRect = (RectTransform)scrollGO.transform;
            scrollRect.anchorMin = anchorMin; scrollRect.anchorMax = anchorMax;
            scrollRect.offsetMin = scrollRect.offsetMax = Vector2.zero;

            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical   = true;
            scrollGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpRect = (RectTransform)viewportGO.transform;
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            var listGO = new GameObject("ListRoot", typeof(RectTransform));
            listGO.transform.SetParent(viewportGO.transform, false);
            listRoot = (RectTransform)listGO.transform;
            listRoot.anchorMin = new Vector2(0f, 1f);
            listRoot.anchorMax = new Vector2(1f, 1f);
            listRoot.pivot     = new Vector2(0.5f, 1f);
            listRoot.offsetMin = listRoot.offsetMax = Vector2.zero;

            var vlg = listGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = listGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = vpRect;
            sr.content  = listRoot;

            emptyState = MakeText(parent, "EmptyState", "No eco alerts yet.", 14,
                TextAlignmentOptions.Center,
                anchorMin, anchorMax, TextSecondary);

            return scrollRect;
        }

        private static Button BuildButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        private static Button BuildTabButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var btn = BuildButton(parent, name, anchorMin, anchorMax, TabBg);

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(btn.transform, false);
            var tr = (RectTransform)textGO.transform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<TextMeshProUGUI>();
            t.text          = label;
            t.fontSize      = 16;
            t.fontStyle     = FontStyles.Bold;
            t.color         = Color.white;
            t.alignment     = TextAlignmentOptions.Center;
            t.raycastTarget = false;

            return btn;
        }

        private static GameObject MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
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
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
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

        private static Image MakeColoredCircle(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            img.sprite        = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            return img;
        }

        private static void AttachPokeFeedback(Button btn)
        {
            if (btn == null) return;
            var feedback = btn.GetComponent<SustainabilityPokeButton>();
            if (feedback == null) feedback = btn.gameObject.AddComponent<SustainabilityPokeButton>();
            var img = btn.GetComponent<Image>();
            feedback.SetReferences(btn, img, btn.transform as RectTransform,
                Object.FindFirstObjectByType<VRHapticsHelper>());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Misc helpers
        // ─────────────────────────────────────────────────────────────────────

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private static void SetField(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance);
            f?.SetValue(obj, value);
        }

        private static object GetFieldValue(object obj, string field)
        {
            if (obj == null) return null;
            var f = obj.GetType().GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(obj);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }
    }
}
