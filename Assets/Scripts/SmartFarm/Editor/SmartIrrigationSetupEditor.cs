using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace SmartFarm.Editor
{
    /// <summary>
    /// One-click setup for the Smart Irrigation System.
    ///
    /// What this does:
    ///   1. Adds SmartIrrigationManager + IrrigationScheduler to FarmSimulationHub
    ///   2. Wires simulationManager, weatherManager, scheduler references
    ///   3. Rebuilds the Irrigation tab with the 3-mode Smart Irrigation UI
    ///      (Manual / Scheduled / AI) and wires all UI references
    ///
    /// Prerequisite: Run "Tools > Smart Farm > Full Platform Setup" first so the
    /// FarmSimulationHub and SmartFarmTablet already exist in the scene.
    ///
    /// Safe to re-run — existing components are kept; page is always rebuilt fresh.
    ///
    /// Menu: Tools > Smart Farm > Setup Smart Irrigation
    /// </summary>
    public static class SmartIrrigationSetupEditor
    {
        [MenuItem("Tools/Smart Farm/Setup Smart Irrigation")]
        public static void SetupSmartIrrigation()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SmartIrrigation] Stop Play mode first!");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SmartIrrigation] Open a Unity scene first.");
                return;
            }

            // ── 1. Find FarmSimulationHub ─────────────────────────────────────────
            var hub = GameObject.Find("FarmSimulationHub");
            if (hub == null)
            {
                Debug.LogError("[SmartIrrigation] FarmSimulationHub not found in scene.\n" +
                               "Run  Tools > Smart Farm > Full Platform Setup  first, then re-run this.");
                return;
            }

            // ── 2. Add SmartIrrigationManager + IrrigationScheduler to hub ────────
            var irrigMgr = hub.GetComponent<SmartIrrigationManager>();
            if (irrigMgr == null)
            {
                irrigMgr = hub.AddComponent<SmartIrrigationManager>();
                Debug.Log("[SmartIrrigation] Added SmartIrrigationManager to FarmSimulationHub.");
            }

            var scheduler = hub.GetComponent<IrrigationScheduler>();
            if (scheduler == null)
            {
                scheduler = hub.AddComponent<IrrigationScheduler>();
                Debug.Log("[SmartIrrigation] Added IrrigationScheduler to FarmSimulationHub.");
            }

            // ── 3. Wire SmartIrrigationManager references ─────────────────────────
            var simMgr     = hub.GetComponent<FarmSimulationManager>();
            // WeatherManager may be on hub or found elsewhere in the scene
            var weatherMgr = hub.GetComponent<WeatherManager>()
                             ?? Object.FindFirstObjectByType<WeatherManager>();

            SetField(irrigMgr, "simulationManager", simMgr);
            if (weatherMgr != null) SetField(irrigMgr, "weatherManager", weatherMgr);
            SetField(irrigMgr, "scheduler", scheduler);

            // ── 4. Locate the Irrigation page GameObject ─────────────────────────
            //   Strategy (in priority order):
            //   a) IrrigationUI component is already on the page  → use its GameObject
            //   b) A GameObject named "IrrigationPage" exists     → use it directly
            //   c) SmartFarmTablet > ContentRoot > IrrigationPage → search inside tablet
            //   d) Nothing found                                  → abort with clear message

            var dataMgr = Object.FindFirstObjectByType<FarmDataManager>();
            GameObject page = null;

            var oldUI = Object.FindFirstObjectByType<IrrigationUI>();
            if (oldUI != null)
            {
                page = oldUI.gameObject;
            }
            else
            {
                // Try direct name search (works even with no IrrigationUI component)
                page = GameObject.Find("IrrigationPage");

                if (page == null)
                {
                    // Try inside the SmartFarmTablet hierarchy
                    var tablet = GameObject.Find("SmartFarmTablet");
                    if (tablet != null)
                        page = FindChildByName(tablet.transform, "IrrigationPage");
                }
            }

            if (page == null)
            {
                Debug.LogError("[SmartIrrigation] Could not find IrrigationPage in the scene.\n" +
                               "Run  Tools > Smart Farm > Full Platform Setup  first, then re-run this.\n" +
                               "If the tablet already exists but has a different page name, rename it to 'IrrigationPage'.");
                return;
            }

            // ── 5. Destroy all children + old IrrigationUI component (if any) ────
            while (page.transform.childCount > 0)
                Object.DestroyImmediate(page.transform.GetChild(0).gameObject);

            if (oldUI != null)
                Object.DestroyImmediate(oldUI);

            // ── 6. Build new 3-mode Smart Irrigation page ─────────────────────────
            BuildSmartIrrigationPage(page.transform, out var newUI);

            // ── 7. Wire manager references on new IrrigationUI ────────────────────
            if (dataMgr != null)  SetField(newUI, "dataManager",       dataMgr);
            SetField(newUI, "irrigationManager", irrigMgr);

            // ── 8. Set UI layer + mark dirty ──────────────────────────────────────
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(page, uiLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = page;

            Debug.Log("[SmartIrrigation] Setup complete!\n" +
                      "• SmartIrrigationManager + IrrigationScheduler added to FarmSimulationHub\n" +
                      "• Irrigation tab rebuilt with Manual / Scheduled / AI modes\n" +
                      "• All references wired. Press Play to test.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Page builder — creates the full 3-mode UI hierarchy
        // ─────────────────────────────────────────────────────────────────────────

        internal static void BuildSmartIrrigationPage(Transform page, out IrrigationUI irrigationUI)
        {
            // ── Title ─────────────────────────────────────────────────────────────
            CreateText(page, "TitleText", "Smart Irrigation Control", 22,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));

            // ── Mode selector row ──────────────────────────────────────────────────
            var modeRow = CreatePanel(page, "ModeSelectorRow", Vector2.zero,
                new Vector2(0.03f, 0.76f), new Vector2(0.97f, 0.87f),
                new Color(0.08f, 0.14f, 0.22f, 0.8f));

            var manualModeBtn    = CreateButton(modeRow.transform, "Manual",    new Vector2(-210, -20));
            var scheduledModeBtn = CreateButton(modeRow.transform, "Scheduled", new Vector2(   0, -20));
            var aiModeBtn        = CreateButton(modeRow.transform, "AIMode",    new Vector2( 210, -20));

            ResizeButton(manualModeBtn,    160, 40);
            ResizeButton(scheduledModeBtn, 160, 40);
            ResizeButton(aiModeBtn,        160, 40);

            manualModeBtn.GetComponentInChildren<TMP_Text>().text    = "Manual";
            scheduledModeBtn.GetComponentInChildren<TMP_Text>().text = "Scheduled";
            aiModeBtn.GetComponentInChildren<TMP_Text>().text        = "AI Mode";

            SetButtonColor(manualModeBtn,    new Color(0.10f, 0.45f, 0.90f, 1f));
            SetButtonColor(scheduledModeBtn, new Color(0.22f, 0.22f, 0.30f, 1f));
            SetButtonColor(aiModeBtn,        new Color(0.22f, 0.22f, 0.30f, 1f));

            // ── Manual panel (visible by default) ─────────────────────────────────
            var manualPanel = CreatePanel(page, "ManualPanel", Vector2.zero,
                new Vector2(0.03f, 0.28f), new Vector2(0.97f, 0.74f),
                new Color(0.08f, 0.14f, 0.22f, 0.55f));

            var manualStatus = CreateText(manualPanel.transform, "ManualStatusText",
                "Manual Irrigation: OFF", 20,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.95f));

            var manualOn  = CreateButton(manualPanel.transform, "ON",  new Vector2(-120, -130));
            var manualOff = CreateButton(manualPanel.transform, "OFF", new Vector2( 120, -130));
            ResizeButton(manualOn,  170, 54);
            ResizeButton(manualOff, 170, 54);
            manualOn.GetComponentInChildren<TMP_Text>().text  = "ON";
            manualOff.GetComponentInChildren<TMP_Text>().text = "OFF";
            SetButtonColor(manualOn,  new Color(0.10f, 0.72f, 0.20f, 0.45f));
            SetButtonColor(manualOff, new Color(0.82f, 0.18f, 0.10f, 1.00f));

            // ── Scheduled panel (hidden by default) ───────────────────────────────
            var scheduledPanel = CreatePanel(page, "ScheduledPanel", Vector2.zero,
                new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.74f),
                new Color(0.08f, 0.14f, 0.22f, 0.55f));
            scheduledPanel.SetActive(false);

            var scheduleStatus = CreateText(scheduledPanel.transform, "ScheduleStatusText",
                "No schedule set", 18,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.96f));

            var morningBtn = CreateButton(scheduledPanel.transform, "Morning", new Vector2(-220, -150));
            var noonBtn    = CreateButton(scheduledPanel.transform, "Noon",    new Vector2(   0, -150));
            var eveningBtn = CreateButton(scheduledPanel.transform, "Evening", new Vector2( 220, -150));
            ResizeButton(morningBtn, 165, 54);
            ResizeButton(noonBtn,    165, 54);
            ResizeButton(eveningBtn, 165, 54);
            morningBtn.GetComponentInChildren<TMP_Text>().text = "Morning";
            noonBtn.GetComponentInChildren<TMP_Text>().text    = "Noon";
            eveningBtn.GetComponentInChildren<TMP_Text>().text = "Evening";
            SetButtonColor(morningBtn, new Color(0.22f, 0.22f, 0.30f, 1f));
            SetButtonColor(noonBtn,    new Color(0.22f, 0.22f, 0.30f, 1f));
            SetButtonColor(eveningBtn, new Color(0.22f, 0.22f, 0.30f, 1f));

            var scheduleSubText = CreateText(scheduledPanel.transform, "ScheduleHintText",
                "Select a time slot to activate scheduled irrigation", 14,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.30f));
            scheduleSubText.GetComponent<TMP_Text>().color = new Color(0.7f, 0.8f, 0.9f, 0.85f);

            // ── AI panel (hidden by default) ──────────────────────────────────────
            var aiPanel = CreatePanel(page, "AIPanel", Vector2.zero,
                new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.74f),
                new Color(0.08f, 0.14f, 0.22f, 0.55f));
            aiPanel.SetActive(false);

            var aiStatus = CreateText(aiPanel.transform, "AIStatusText",
                "AI Irrigation Standby", 22,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.05f, 0.64f), new Vector2(0.95f, 0.94f));

            var aiReason = CreateText(aiPanel.transform, "AIReasonText",
                "Evaluating conditions...", 16,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.60f));
            aiReason.GetComponent<TMP_Text>().color = new Color(0.75f, 0.88f, 0.98f, 1f);

            var aiHint = CreateText(aiPanel.transform, "AIHintText",
                "AI monitors moisture, weather and crop health automatically", 13,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.30f));
            aiHint.GetComponent<TMP_Text>().color = new Color(0.6f, 0.7f, 0.8f, 0.8f);

            // ── Wire IrrigationUI component ───────────────────────────────────────
            irrigationUI = page.gameObject.AddComponent<IrrigationUI>();

            SetField(irrigationUI, "manualModeButton",    manualModeBtn.GetComponent<Button>());
            SetField(irrigationUI, "scheduledModeButton", scheduledModeBtn.GetComponent<Button>());
            SetField(irrigationUI, "aiModeButton",        aiModeBtn.GetComponent<Button>());

            SetField(irrigationUI, "manualPanel",    manualPanel);
            SetField(irrigationUI, "scheduledPanel", scheduledPanel);
            SetField(irrigationUI, "aiPanel",        aiPanel);

            SetField(irrigationUI, "manualStatusText", manualStatus.GetComponent<TMP_Text>());
            SetField(irrigationUI, "manualOnButton",   manualOn.GetComponent<Button>());
            SetField(irrigationUI, "manualOffButton",  manualOff.GetComponent<Button>());

            SetField(irrigationUI, "scheduleStatusText", scheduleStatus.GetComponent<TMP_Text>());
            SetField(irrigationUI, "morningButton",      morningBtn.GetComponent<Button>());
            SetField(irrigationUI, "noonButton",         noonBtn.GetComponent<Button>());
            SetField(irrigationUI, "eveningButton",      eveningBtn.GetComponent<Button>());

            SetField(irrigationUI, "aiStatusText", aiStatus.GetComponent<TMP_Text>());
            SetField(irrigationUI, "aiReasonText", aiReason.GetComponent<TMP_Text>());
        }

        // ─────────────────────────────────────────────────────────────────────────
        // UI creation helpers (mirrors SmartFarmSetupEditor helpers)
        // ─────────────────────────────────────────────────────────────────────────

        private static GameObject CreatePanel(Transform parent, string name, Vector2 yOffsets,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(0, yOffsets.x);
            rt.offsetMax = new Vector2(0, yOffsets.y);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static GameObject CreateText(Transform parent, string name, string value,
            float fontSize, TextAlignmentOptions align, Vector2 offset,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = offset;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text          = value;
            t.fontSize      = fontSize;
            t.alignment     = align;
            t.color         = Color.white;
            t.raycastTarget = false;
            return go;
        }

        private static GameObject CreateButton(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject("Button_" + label.Replace(" ", ""), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin         = new Vector2(0.5f, 1f);
            rt.anchorMax         = new Vector2(0.5f, 1f);
            rt.pivot             = new Vector2(0.5f, 1f);
            rt.anchoredPosition  = pos;
            rt.sizeDelta         = new Vector2(140, 40);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.78f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var t = textGO.AddComponent<TextMeshProUGUI>();
            t.text          = label;
            t.fontSize      = 15;
            t.color         = Color.white;
            t.alignment     = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return go;
        }

        private static void ResizeButton(GameObject btn, float w, float h)
        {
            var rt = btn.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(w, h);
        }

        private static void SetButtonColor(GameObject btn, Color color)
        {
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = color;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        internal static void SetField(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            f?.SetValue(obj, value);
        }

        /// <summary>
        /// Depth-first search for a child GameObject with a given name
        /// (case-insensitive), including inactive objects.
        /// </summary>
        private static GameObject FindChildByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return child.gameObject;

                var found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
