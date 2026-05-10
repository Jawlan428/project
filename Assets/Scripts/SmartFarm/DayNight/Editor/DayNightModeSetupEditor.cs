using System.Collections.Generic;
using SmartFarm.DayNight.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.DayNight.Editor
{
    /// <summary>
    /// One-click editor that scaffolds the entire Day &amp; Night system in the
    /// active scene:
    ///
    ///   1. Creates a <c>DayNightHub</c> GameObject hosting the manager and
    ///      every subsystem (lighting, lamps, glow, atmosphere, weather bridge).
    ///   2. Auto-discovers any <c>SpaceZeta_StreetLamps</c> prefab instance
    ///      already placed in the scene, attaches a <see cref="StreetLamp"/>
    ///      component, and creates a warm Point Light at the lamp head if the
    ///      prefab doesn't already provide one.
    ///   3. Tags the obvious "smart screens" (Crop Growth Monitor canvas,
    ///      Smart Irrigation Tablet borders, dashboard accents, analytics
    ///      titles) with <see cref="SmartScreenGlowTarget"/>.
    ///   4. Builds a world-space VR control panel "EnvironmentControlPanel"
    ///      with Day / Night buttons, status indicator and animated progress bar.
    ///   5. Wires every reference via reflection so the system runs immediately
    ///      after a single click.
    ///
    /// Menu: Tools &gt; Smart Farm &gt; Setup Day &amp; Night System
    /// </summary>
    public static class DayNightModeSetupEditor
    {
        // ── Theme palette (matches the irrigation tablet for visual cohesion) ─

        private static readonly Color BgDeep      = new Color(0.04f, 0.10f, 0.10f, 0.99f);
        private static readonly Color BgPanel     = new Color(0.06f, 0.14f, 0.16f, 0.97f);
        private static readonly Color BgCard      = new Color(0.09f, 0.18f, 0.20f, 0.96f);
        private static readonly Color BgBarTrack  = new Color(0.10f, 0.22f, 0.24f, 1.00f);
        private static readonly Color HeaderTint  = new Color(0.06f, 0.20f, 0.16f, 1.00f);
        private static readonly Color AccentGreen = new Color(0.30f, 0.85f, 0.55f, 1.00f);
        private static readonly Color AccentSun   = new Color(1.00f, 0.80f, 0.30f, 1.00f);
        private static readonly Color AccentMoon  = new Color(0.45f, 0.75f, 1.40f, 1.00f);
        private static readonly Color TextPrimary = new Color(0.94f, 1.00f, 0.96f, 1.00f);
        private static readonly Color TextDim     = new Color(0.65f, 0.85f, 0.78f, 1.00f);
        private static readonly Color BorderColor = new Color(0.10f, 0.85f, 0.55f, 0.55f);

        private const float PanelScale = 0.0035f;

        // ─────────────────────────────────────────────────────────────────────
        //  Menu items
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Smart Farm/Setup Day && Night System", priority = 30)]
        public static void Setup()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[DayNight] Stop Play mode before running setup.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[DayNight] Open a scene first.");
                return;
            }

            GameObject hub = null;
            try { hub = EnsureHub(); }
            catch (System.Exception ex)
            { Debug.LogError($"[DayNight] Hub setup failed: {ex.Message}\n{ex.StackTrace}"); }

            int lampCount = 0;
            try { if (hub != null) lampCount = AttachLampsInScene(hub.GetComponent<StreetLampManager>()); }
            catch (System.Exception ex)
            { Debug.LogError($"[DayNight] Street lamp setup failed (continuing): {ex.Message}\n{ex.StackTrace}"); }

            int glowCount = 0;
            try { if (hub != null) glowCount = TagSmartScreensInScene(hub.GetComponent<ScreenGlowController>()); }
            catch (System.Exception ex)
            { Debug.LogError($"[DayNight] Smart screen tagging failed (continuing): {ex.Message}\n{ex.StackTrace}"); }

            try { if (hub != null) BuildControlPanel(hub); }
            catch (System.Exception ex)
            { Debug.LogError($"[DayNight] UI panel build failed: {ex.Message}\n{ex.StackTrace}"); }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[DayNight] Setup complete!\n" +
                      $" • Hub: DayNightHub (manager + 5 modules).\n" +
                      $" • Street lamps wired: {lampCount}.\n" +
                      $" • Smart screens tagged: {glowCount}.\n" +
                      $" • Control panel: EnvironmentControlPanel (worldspace VR canvas).\n" +
                      $"Press Play and tap Day / Night.");
        }

        [MenuItem("Tools/Smart Farm/Rebuild Day && Night UI", priority = 31)]
        public static void RebuildUI()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[DayNight] Stop Play mode first.");
                return;
            }
            var existing = GameObject.Find("EnvironmentControlPanel");
            if (existing != null) Undo.DestroyObjectImmediate(existing);
            Setup();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Hub
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject EnsureHub()
        {
            var hub = GameObject.Find("DayNightHub");
            if (hub == null)
            {
                hub = new GameObject("DayNightHub");
                Undo.RegisterCreatedObjectUndo(hub, "DayNightHub");
            }

            var manager        = GetOrAdd<DayNightModeManager>(hub);
            var lighting       = GetOrAdd<EnvironmentLightingController>(hub);
            var lampMgr        = GetOrAdd<StreetLampManager>(hub);
            var glowMgr        = GetOrAdd<ScreenGlowController>(hub);
            var atmosphere     = GetOrAdd<AtmosphereController>(hub);
            var weatherBridge  = GetOrAdd<WeatherNightBridge>(hub);

            // Wire references (private fields)
            SetField(lighting,      "manager", manager);
            SetField(lampMgr,       "manager", manager);
            SetField(glowMgr,       "manager", manager);
            SetField(atmosphere,    "manager", manager);
            SetField(weatherBridge, "manager", manager);

            var weather = Object.FindFirstObjectByType<WeatherManager>();
            if (weather != null)
            {
                SetField(weatherBridge, "weatherManager", weather);
            }
            SetField(weatherBridge, "lampManager",         lampMgr);
            SetField(weatherBridge, "lightingController", lighting);

            // Hand the lighting controller a sun reference if we can find one.
            var sun = RenderSettings.sun;
            if (sun == null)
            {
                var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i].type == LightType.Directional) { sun = lights[i]; break; }
            }
            if (sun != null) SetField(lighting, "sunLight", sun);

            return hub;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Street lamp auto-detection
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds every prefab instance from <c>SpaceZeta_StreetLamps</c> (or any
        /// GameObject whose root name starts with "StreetLamp") and:
        ///   1. Adds a <see cref="StreetLamp"/> component if missing.
        ///   2. Adds a warm Point Light at the lamp head if missing.
        /// </summary>
        private static int AttachLampsInScene(StreetLampManager lampMgr)
        {
            if (lampMgr == null) return 0;

            // Collect candidate root GameObjects whose name implies a street lamp.
            var roots = new List<GameObject>();
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null) continue;
                if (go.parent != null) continue; // we only want top-level objects
                string n = go.name ?? string.Empty;
                if (LooksLikeLampName(n)) roots.Add(go.gameObject);
            }

            // Also pick up nested instances (sometimes prefabs end up as children
            // of an "Environment" or "Lamps" container). Scan one level deeper.
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null) continue;
                if (LooksLikeLampName(go.name) && !roots.Contains(go.gameObject))
                    roots.Add(go.gameObject);
            }

            int wired = 0;
            for (int i = 0; i < roots.Count; i++)
            {
                var lamp = roots[i];
                if (lamp == null) continue;

                var lampComp = lamp.GetComponent<StreetLamp>();
                if (lampComp == null) lampComp = Undo.AddComponent<StreetLamp>(lamp);

                // Make sure a Light exists somewhere under this lamp; if not,
                // build a sensible warm Point Light at the top of the bounds.
                var existingLight = lamp.GetComponentInChildren<Light>(true);
                if (existingLight == null) existingLight = CreateLampLight(lamp);
                else                       TuneLampLight(existingLight);

                // Try to find a "bulb-ish" emissive renderer to drive too.
                var bulbRenderer = FindBulbRenderer(lamp);

                SetField(lampComp, "lampLight",    existingLight);
                SetField(lampComp, "bulbRenderer", bulbRenderer);

                wired++;
            }

            // Push the discovered list into the manager.
            SetField(lampMgr, "lamps", new List<StreetLamp>(CollectLamps()));
            return wired;
        }

        private static bool LooksLikeLampName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return n.StartsWith("streetlamp")
                || n.StartsWith("street lamp")
                || n.StartsWith("street_lamp")
                || n.Contains("lamp1_")
                || n.Contains("lamp2_");
        }

        private static IEnumerable<StreetLamp> CollectLamps()
        {
            var found = Object.FindObjectsByType<StreetLamp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++) yield return found[i];
        }

        private static Light CreateLampLight(GameObject lampRoot)
        {
            // Find the highest renderer on the lamp — that's almost certainly
            // the head / bulb area. Place a Point Light just below it.
            Vector3 placement = lampRoot.transform.position + Vector3.up * 3f;
            var renderers = lampRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                placement = new Vector3(b.center.x, b.max.y - 0.25f, b.center.z);
            }

            var lightGO = new GameObject("LampLight");
            Undo.RegisterCreatedObjectUndo(lightGO, "Lamp Light");
            lightGO.transform.SetParent(lampRoot.transform, true);
            lightGO.transform.position = placement;

            var light = lightGO.AddComponent<Light>();
            light.type        = LightType.Point;
            light.color       = new Color(1.00f, 0.78f, 0.42f);
            light.intensity   = 0f; // manager turns this on at night
            light.range       = 9f;
            light.shadows     = LightShadows.Soft;
            light.shadowStrength = 0.5f;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            light.renderMode  = LightRenderMode.Auto;
            light.bounceIntensity = 0f; // realtime only — Quest VR friendly
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        private static void TuneLampLight(Light light)
        {
            if (light == null) return;
            // Don't overwrite a designer-tuned colour, but make sure VR-friendly defaults are set.
            if (light.range < 1f) light.range = 9f;
            if (light.shadows == LightShadows.Hard) light.shadows = LightShadows.Soft;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            light.bounceIntensity = 0f;
            light.lightmapBakeType = LightmapBakeType.Realtime;
        }

        private static Renderer FindBulbRenderer(GameObject lampRoot)
        {
            // First pass: prefer renderers whose name hints "bulb / glass / light / head".
            var renderers = lampRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                string n = renderers[i].gameObject.name.ToLowerInvariant();
                if (n.Contains("bulb") || n.Contains("glass") || n.Contains("light") || n.Contains("head") || n.Contains("emit"))
                    return renderers[i];
            }
            // Second pass: take the highest renderer.
            Renderer best = null;
            float bestY = float.NegativeInfinity;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                float y = renderers[i].bounds.max.y;
                if (y > bestY) { bestY = y; best = renderers[i]; }
            }
            return best;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Smart screen auto-tagging
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Walks the scene looking for "smart screens" (Crop Growth Monitor,
        /// Smart Irrigation Tablet, dashboards, analytics canvases). Adds a
        /// <see cref="SmartScreenGlowTarget"/> wherever it finds a UI border /
        /// title / accent or a screen mesh that should glow at night.
        /// </summary>
        private static int TagSmartScreensInScene(ScreenGlowController glowMgr)
        {
            int tagged = 0;

            // 1) Tablets, monitors, analytics, dashboards: any GameObject under
            //    one of those roots whose name marks it as a glow candidate.
            string[] rootNames =
            {
                "SmartIrrigationTablet",
                "SmartFarmTablet",
                "CropGrowthMonitor",
                "FarmDashboard",
                "AnalyticsCanvas",
                "PollBoard",
                "WeatherCanvas",
                "TrainingRoom",
            };
            string[] glowChildHints =
            {
                "border", "accent", "headerstatus", "titletext", "statusled",
                "led", "fill", "ring", "neon", "edge"
            };

            foreach (var rootName in rootNames)
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;

                var graphics = root.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    var g = graphics[i];
                    if (g == null) continue;
                    string n = g.gameObject.name.ToLowerInvariant();
                    bool match = false;
                    for (int h = 0; h < glowChildHints.Length; h++)
                        if (n.Contains(glowChildHints[h])) { match = true; break; }
                    if (!match) continue;
                    if (g.GetComponent<SmartScreenGlowTarget>() != null) continue;

                    var target = Undo.AddComponent<SmartScreenGlowTarget>(g.gameObject);
                    SetField(target, "targetGraphic", g);
                    target.CaptureCurrentAsDay();
                    tagged++;
                }

                // Also catch any MeshRenderer-based "screen" beneath this root
                // (e.g. world-space monitors that aren't on a Canvas).
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (!n.Contains("screen") && !n.Contains("display") && !n.Contains("monitor"))
                        continue;
                    if (r.GetComponent<SmartScreenGlowTarget>() != null) continue;

                    var target = Undo.AddComponent<SmartScreenGlowTarget>(r.gameObject);
                    SetField(target, "targetRenderer", r);
                    target.CaptureCurrentAsDay();
                    tagged++;
                }
            }

            // Push discovered targets into the controller.
            SetField(glowMgr, "targets",
                new List<SmartScreenGlowTarget>(Object.FindObjectsByType<SmartScreenGlowTarget>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)));

            return tagged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Control panel UI
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildControlPanel(GameObject hub)
        {
            var existing = GameObject.Find("EnvironmentControlPanel");
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var manager = hub.GetComponent<DayNightModeManager>();
            if (manager == null) return;

            // Anchor in front of the player; will be re-positioned by hand if needed.
            Vector3 spawnPos = new Vector3(0.95f, 1.45f, 1.4f);
            Quaternion spawnRot = Quaternion.Euler(8f, 200f, 0f);

            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();

            var panel = new GameObject("EnvironmentControlPanel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panel, "Environment Control Panel");
            panel.transform.position   = spawnPos;
            panel.transform.rotation   = spawnRot;
            panel.transform.localScale = new Vector3(PanelScale, PanelScale, PanelScale);

            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 60;

            var scaler = panel.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            panel.AddComponent<GraphicRaycaster>();
            var trackedType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (trackedType != null) panel.AddComponent(trackedType);

            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 380f);

            // Background + border
            var bg = MakePanel(panel.transform, "Background", Vector2.zero, Vector2.one, BgDeep);
            bg.GetComponent<Image>().raycastTarget = true;
            BuildBorderStrips(bg.transform, BorderColor, 0.012f);

            // Header
            var header = MakePanel(panel.transform, "Header",
                new Vector2(0f, 0.83f), new Vector2(1f, 1f), HeaderTint);
            var titleText = MakeText(header.transform, "Title",
                "ENVIRONMENT CONTROL", 26, TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.20f), new Vector2(0.70f, 0.85f),
                AccentGreen, true);
            var statusText = MakeText(header.transform, "StatusText",
                "Mode: DAY", 16, TextAlignmentOptions.Right,
                new Vector2(0.55f, 0.20f), new Vector2(0.92f, 0.85f),
                TextDim);
            var statusLed = MakeColoredCircle(header.transform, "StatusLed",
                new Vector2(0.94f, 0.32f), new Vector2(0.985f, 0.72f), AccentSun);

            // Buttons row
            var buttonsRow = MakePanel(panel.transform, "ButtonsRow",
                new Vector2(0f, 0.30f), new Vector2(1f, 0.80f), new Color(0f, 0f, 0f, 0f));
            buttonsRow.GetComponent<Image>().raycastTarget = false;

            var dayBtn   = BuildModeButton(buttonsRow.transform, "DayButton",   "DAY MODE",   "☀", AccentSun,
                new Vector2(0.05f, 0.10f), new Vector2(0.48f, 0.90f));
            var nightBtn = BuildModeButton(buttonsRow.transform, "NightButton", "NIGHT MODE", "☾", AccentMoon,
                new Vector2(0.52f, 0.10f), new Vector2(0.95f, 0.90f));

            // Progress bar + label
            MakeText(panel.transform, "ProgressLabel",
                "DAY  →  NIGHT", 12, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.27f),
                TextDim, true);

            var progressTrack = MakePanel(panel.transform, "ProgressTrack",
                new Vector2(0.06f, 0.13f), new Vector2(0.94f, 0.19f), BgBarTrack);
            progressTrack.GetComponent<Image>().raycastTarget = false;
            var progressFill = MakePanel(progressTrack.transform, "ProgressFill",
                Vector2.zero, Vector2.one, AccentMoon);
            var progressFillImg = progressFill.GetComponent<Image>();
            progressFillImg.type        = Image.Type.Filled;
            progressFillImg.fillMethod  = Image.FillMethod.Horizontal;
            progressFillImg.fillOrigin  = (int)Image.OriginHorizontal.Left;
            progressFillImg.fillAmount  = 0f;
            progressFillImg.raycastTarget = false;

            // Tag line
            MakeText(panel.transform, "Tagline",
                "Smart Farm — Day & Night",
                12, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.10f),
                new Color(TextDim.r, TextDim.g, TextDim.b, 0.7f));

            // Component
            var ui = panel.AddComponent<EnvironmentControlPanelUI>();
            SetField(ui, "manager",      manager);
            SetField(ui, "dayButton",    dayBtn);
            SetField(ui, "nightButton",  nightBtn);
            SetField(ui, "titleText",    titleText);
            SetField(ui, "statusText",   statusText);
            SetField(ui, "statusLed",    statusLed);
            SetField(ui, "progressFill", progressFillImg);

            // Tag with the UI layer so XR raycasters can hit us.
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(panel, uiLayer);

            EnableXRUIControllers();

            Selection.activeGameObject = panel;
        }

        private static EnvironmentControlButton BuildModeButton(Transform parent, string name,
            string label, string iconChar, Color accent, Vector2 anchorMin, Vector2 anchorMax)
        {
            // Root
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Background
            var bgImg = go.AddComponent<Image>();
            bgImg.color = BgCard;
            bgImg.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor    = Color.white;
            colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;

            // Border (thin tinted strips)
            var borderGO = MakePanel(go.transform, "Border",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Color(accent.r, accent.g, accent.b, 0.25f));
            var borderImg = borderGO.GetComponent<Image>();
            borderImg.raycastTarget = false;
            // Hollow border = inner black panel on top of the tinted box.
            var inner = MakePanel(borderGO.transform, "Inner",
                new Vector2(0.012f, 0.018f), new Vector2(0.988f, 0.982f), BgCard);
            inner.GetComponent<Image>().raycastTarget = false;

            // Icon (☀ / ☾)
            var iconText = MakeText(go.transform, "Icon", iconChar, 64,
                TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.20f), new Vector2(0.34f, 0.85f),
                accent, true);

            // Label
            var labelText = MakeText(go.transform, "Label", label, 24,
                TextAlignmentOptions.Left,
                new Vector2(0.36f, 0.30f), new Vector2(0.96f, 0.78f),
                TextPrimary, true);

            // Subtitle
            MakeText(go.transform, "Subtitle",
                name == "DayButton" ? "Bright • Sunny • Calm" : "Dark • Lit • Immersive",
                14, TextAlignmentOptions.Left,
                new Vector2(0.36f, 0.10f), new Vector2(0.96f, 0.30f),
                TextDim);

            // Component wiring
            var ctrl = go.AddComponent<EnvironmentControlButton>();
            ctrl.SetReferences(btn, bgImg, borderImg, null, labelText);

            // Tweak palette per mode using reflection so we don't expose SetField API.
            Color activeBg     = name == "DayButton"
                ? new Color(0.22f, 0.18f, 0.06f, 1f)
                : new Color(0.06f, 0.18f, 0.30f, 1f);
            Color activeBorder = accent;
            SetField(ctrl, "activeBackground",   activeBg);
            SetField(ctrl, "inactiveBackground", BgCard);
            SetField(ctrl, "activeBorder",       activeBorder);
            SetField(ctrl, "inactiveBorder",     new Color(accent.r, accent.g, accent.b, 0.25f));
            SetField(ctrl, "activeText",         TextPrimary);
            SetField(ctrl, "inactiveText",       TextDim);

            return ctrl;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Border + primitive builders
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildBorderStrips(Transform parent, Color color, float thickness)
        {
            var top    = MakePanel(parent, "BorderTop",    new Vector2(0f, 1f - thickness), new Vector2(1f, 1f), color);
            var bottom = MakePanel(parent, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, thickness), color);
            var left   = MakePanel(parent, "BorderLeft",   new Vector2(0f, 0f), new Vector2(thickness, 1f), color);
            var right  = MakePanel(parent, "BorderRight",  new Vector2(1f - thickness, 0f), new Vector2(1f, 1f), color);
            top.GetComponent<Image>().raycastTarget    = false;
            bottom.GetComponent<Image>().raycastTarget = false;
            left.GetComponent<Image>().raycastTarget   = false;
            right.GetComponent<Image>().raycastTarget  = false;
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

        // ─────────────────────────────────────────────────────────────────────
        //  Misc helpers (mirrored from SmartIrrigationTabletSetupEditor)
        // ─────────────────────────────────────────────────────────────────────

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }

        private static void SetField(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(obj, value);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private static void EnableXRUIControllers()
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
                            bits.intValue = val | uiBit | 1;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }
            if (enabled > 0)
                Debug.Log($"[DayNight] Enabled UI Interaction on {enabled} XR interactor(s).");
        }
    }
}
