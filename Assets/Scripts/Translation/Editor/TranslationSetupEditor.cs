using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using SmartFarm;

namespace Translation.Editor
{
    /// <summary>
    /// One-click setup for the VR Meeting Translation System.
    ///
    /// What this does:
    ///   1. Creates TranslationHub GameObject with TranslationManager + SubtitleUIController
    ///   2. Finds SmartFarmTablet in the scene
    ///   3. Adds a Translation tab button to the existing tab bar (resizes all 6 tabs to fit)
    ///   4. Creates TranslationPage inside ContentRoot
    ///   5. Builds the full Translation UI (language selectors, transcript list, controls)
    ///   6. Wires TabletAppController with the new translationTabButton + translationPage
    ///   7. Marks scene dirty
    ///
    /// Prerequisite: Run "Tools > Smart Farm > Full Platform Setup" first.
    ///
    /// Menu: Tools > Smart Farm > Setup Translation Tab
    /// </summary>
    public static class TranslationSetupEditor
    {
        [MenuItem("Tools/Smart Farm/Setup Translation Tab")]
        public static void SetupTranslation()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Translation] Stop Play mode first.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[Translation] Open a scene first.");
                return;
            }

            // ── 1. Create / find TranslationHub ──────────────────────────────
            var hub = EnsureTranslationHub();

            // ── 2. Find SmartFarmTablet ───────────────────────────────────────
            var tablet = GameObject.Find("SmartFarmTablet");
            if (tablet == null)
            {
                Debug.LogError("[Translation] SmartFarmTablet not found.\n" +
                               "Run  Tools > Smart Farm > Full Platform Setup  first.");
                return;
            }

            // ── 3. Find TabBar and resize all tab buttons to fit 6 ───────────
            var tabBar = FindChildByName(tablet.transform, "TabBar");
            ResizeTabBarForSixTabs(tabBar);

            // ── 4. Add Translation tab button ─────────────────────────────────
            var translationTabBtn = AddTranslationTabButton(tabBar?.transform ?? tablet.transform);

            // ── 5. Find / create ContentRoot and TranslationPage ──────────────
            var contentRoot = FindChildByName(tablet.transform, "ContentRoot");
            if (contentRoot == null)
            {
                Debug.LogError("[Translation] ContentRoot not found inside SmartFarmTablet.");
                return;
            }

            var existingPage = FindChildByName(contentRoot.transform, "TranslationPage");
            if (existingPage != null)
            {
                // Rebuild
                while (existingPage.transform.childCount > 0)
                    Object.DestroyImmediate(existingPage.transform.GetChild(0).gameObject);
                var oldPage = existingPage.GetComponent<TranslationTabletPage>();
                if (oldPage != null) Object.DestroyImmediate(oldPage);
            }
            else
            {
                existingPage = new GameObject("TranslationPage", typeof(RectTransform));
                existingPage.transform.SetParent(contentRoot.transform, false);
                Stretch((RectTransform)existingPage.transform);
                existingPage.SetActive(false);
            }

            // ── 6. Build Translation page UI ──────────────────────────────────
            BuildTranslationPage(existingPage.transform, out var pageUI);

            // ── 7. Wire TranslationManager reference ──────────────────────────
            var translationManager = hub.GetComponent<TranslationManager>();
            SetField(pageUI, "translationManager", translationManager);

            // ── 8. Wire TabletAppController ───────────────────────────────────
            var appCtrl = tablet.GetComponent<TabletAppController>();
            if (appCtrl != null)
            {
                SetField(appCtrl, "translationTabButton", translationTabBtn?.GetComponent<Button>());
                SetField(appCtrl, "translationPage",      existingPage);
            }

            // ── 9. UI layer ───────────────────────────────────────────────────
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(existingPage, uiLayer);
            if (translationTabBtn != null && uiLayer >= 0)
                SetLayerRecursive(translationTabBtn, uiLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = existingPage;

            Debug.Log("[Translation] Setup complete!\n" +
                      "• TranslationHub created with TranslationManager + SubtitleUIController\n" +
                      "• Translation tab added to SmartFarmTablet\n" +
                      "• Open TranslationManager in Inspector to set your Whisper API key\n" +
                      "  and LibreTranslate endpoint before pressing Play.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Hub creation
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject EnsureTranslationHub()
        {
            var existing = GameObject.Find("TranslationHub");
            if (existing != null)
            {
                EnsureComponent<TranslationManager>(existing);
                EnsureComponent<SubtitleUIController>(existing);
                return existing;
            }

            var hub = new GameObject("TranslationHub");
            Undo.RegisterCreatedObjectUndo(hub, "Translation Setup");
            hub.AddComponent<TranslationManager>();
            hub.AddComponent<SubtitleUIController>();
            Debug.Log("[Translation] Created TranslationHub.");
            return hub;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tab bar — resize existing 5 buttons and add 6th
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Repositions the 5 existing tab buttons to fit 6 (120 px wide each).
        /// Centers: -310, -186, -62, 62, 186, 310  (gap = 124)
        /// </summary>
        private static void ResizeTabBarForSixTabs(GameObject tabBar)
        {
            if (tabBar == null) return;

            string[] names = { "Button_Overview", "Button_Irrigation", "Button_Alerts", "Button_Polls", "Button_History" };
            float[] xPositions = { -310f, -186f, -62f, 62f, 186f };

            for (int i = 0; i < names.Length; i++)
            {
                var child = FindChildByName(tabBar.transform, names[i]);
                // Also try by partial match (button naming varies)
                if (child == null)
                {
                    for (int c = 0; c < tabBar.transform.childCount; c++)
                    {
                        var t = tabBar.transform.GetChild(c);
                        if (i < tabBar.transform.childCount)
                        {
                            child = tabBar.transform.GetChild(i).gameObject;
                            break;
                        }
                    }
                }
                if (child == null) continue;

                var rt = child.GetComponent<RectTransform>();
                if (rt == null) continue;
                rt.sizeDelta         = new Vector2(120f, 40f);
                rt.anchoredPosition  = new Vector2(xPositions[i], rt.anchoredPosition.y);
            }
        }

        private static GameObject AddTranslationTabButton(Transform tabBar)
        {
            var btn = CreateButton(tabBar, "Translation", new Vector2(310f, -8f));
            ResizeButton(btn, 120f, 40f);
            btn.GetComponentInChildren<TMP_Text>().text = "Translate";
            btn.GetComponent<Image>().color = new Color(0.10f, 0.35f, 0.72f, 1f);
            return btn;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Translation page builder
        // ─────────────────────────────────────────────────────────────────────

        private static void BuildTranslationPage(Transform page, out TranslationTabletPage pageUI)
        {
            // ── Title + status bar ────────────────────────────────────────────
            CreateText(page, "TitleText", "Live Translation", 20,
                TextAlignmentOptions.Center, Vector2.zero,
                new Vector2(0.03f, 0.90f), new Vector2(0.78f, 0.99f));

            // Status indicator dot
            var statusDot = CreatePanel(page, "StatusDot", Vector2.zero,
                new Vector2(0.80f, 0.91f), new Vector2(0.87f, 0.98f),
                new Color(0.55f, 0.55f, 0.55f, 1f));

            var statusText = CreateText(page, "StatusText", "Ready", 14,
                TextAlignmentOptions.Left, Vector2.zero,
                new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.91f));
            statusText.color = new Color(0.75f, 0.85f, 1f, 1f);

            // ── Source language row ───────────────────────────────────────────
            CreateText(page, "SrcLabel", "Speaking in:", 14,
                TextAlignmentOptions.Left, new Vector2(6, 0),
                new Vector2(0.02f, 0.73f), new Vector2(0.35f, 0.81f))
                .color = new Color(0.7f, 0.8f, 1f, 0.9f);

            var srcEn = CreateButton(page, "EN", new Vector2(-150f, -168f)); ResizeButton(srcEn, 85f, 32f);
            var srcHe = CreateButton(page, "HE", new Vector2( -55f, -168f)); ResizeButton(srcHe, 85f, 32f);
            var srcAr = CreateButton(page, "AR", new Vector2(  45f, -168f)); ResizeButton(srcAr, 85f, 32f);
            var srcTh = CreateButton(page, "TH", new Vector2( 145f, -168f)); ResizeButton(srcTh, 85f, 32f);
            srcEn.GetComponentInChildren<TMP_Text>().text = "EN";
            srcHe.GetComponentInChildren<TMP_Text>().text = "HE";
            srcAr.GetComponentInChildren<TMP_Text>().text = "AR";
            srcTh.GetComponentInChildren<TMP_Text>().text = "TH";

            // ── Target language row ───────────────────────────────────────────
            CreateText(page, "TgtLabel", "Translate to:", 14,
                TextAlignmentOptions.Left, new Vector2(6, 0),
                new Vector2(0.02f, 0.60f), new Vector2(0.35f, 0.68f))
                .color = new Color(0.7f, 0.8f, 1f, 0.9f);

            var tgtEn = CreateButton(page, "tEN", new Vector2(-150f, -238f)); ResizeButton(tgtEn, 85f, 32f);
            var tgtHe = CreateButton(page, "tHE", new Vector2( -55f, -238f)); ResizeButton(tgtHe, 85f, 32f);
            var tgtAr = CreateButton(page, "tAR", new Vector2(  45f, -238f)); ResizeButton(tgtAr, 85f, 32f);
            var tgtTh = CreateButton(page, "tTH", new Vector2( 145f, -238f)); ResizeButton(tgtTh, 85f, 32f);
            tgtEn.GetComponentInChildren<TMP_Text>().text = "EN";
            tgtHe.GetComponentInChildren<TMP_Text>().text = "HE";
            tgtAr.GetComponentInChildren<TMP_Text>().text = "AR";
            tgtTh.GetComponentInChildren<TMP_Text>().text = "TH";

            // Highlight defaults: EN source, HE target
            srcEn.GetComponent<Image>().color = new Color(0.10f, 0.45f, 0.90f, 1f);
            tgtHe.GetComponent<Image>().color = new Color(0.10f, 0.45f, 0.90f, 1f);

            // ── Control buttons ───────────────────────────────────────────────
            var listenBtn  = CreateButton(page, "ListenToggle",  new Vector2(-155f, -298f)); ResizeButton(listenBtn,  165f, 40f);
            var clearBtn   = CreateButton(page, "Clear",         new Vector2(  10f, -298f)); ResizeButton(clearBtn,    90f, 40f);
            var saveNowBtn = CreateButton(page, "SaveNow",       new Vector2( 115f, -298f)); ResizeButton(saveNowBtn,  95f, 40f);

            listenBtn.GetComponentInChildren<TMP_Text>().text  = "Start Listening";
            clearBtn.GetComponentInChildren<TMP_Text>().text   = "Clear";
            saveNowBtn.GetComponentInChildren<TMP_Text>().text = "Save";

            listenBtn.GetComponent<Image>().color  = new Color(0.10f, 0.72f, 0.20f, 1f);
            saveNowBtn.GetComponent<Image>().color = new Color(0.20f, 0.50f, 0.72f, 1f);

            // ── Toggle buttons ────────────────────────────────────────────────
            var subToggle  = CreateButton(page, "SubtitleToggle",  new Vector2(-122f, -350f)); ResizeButton(subToggle,  185f, 36f);
            var saveToggle = CreateButton(page, "SaveToggle",      new Vector2( 100f, -350f)); ResizeButton(saveToggle, 185f, 36f);

            var subToggleLabel  = subToggle.GetComponentInChildren<TMP_Text>();
            var saveToggleLabel = saveToggle.GetComponentInChildren<TMP_Text>();
            subToggleLabel.text  = "Subtitles: ON";
            saveToggleLabel.text = "Auto-Save: ON";
            subToggle.GetComponent<Image>().color  = new Color(0.10f, 0.62f, 0.28f, 1f);
            saveToggle.GetComponent<Image>().color = new Color(0.10f, 0.62f, 0.28f, 1f);

            // ── Transcript scroll area ────────────────────────────────────────
            // ScrollRect wrapper
            var scrollGO  = new GameObject("TranscriptScrollView", typeof(RectTransform));
            scrollGO.transform.SetParent(page, false);
            var scrollRt  = (RectTransform)scrollGO.transform;
            scrollRt.anchorMin = new Vector2(0.01f, 0.02f);
            scrollRt.anchorMax = new Vector2(0.99f, 0.38f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
            var sr = scrollGO.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical   = true;
            sr.scrollSensitivity = 30f;
            scrollGO.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.6f);

            // Viewport
            var vpGO = new GameObject("Viewport", typeof(RectTransform));
            vpGO.transform.SetParent(scrollGO.transform, false);
            var vpRt = (RectTransform)vpGO.transform;
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            vpGO.AddComponent<Image>().color = Color.clear;
            var mask = vpGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content (list root)
            var contentGO = new GameObject("ListRoot", typeof(RectTransform));
            contentGO.transform.SetParent(vpGO.transform, false);
            var contentRt = (RectTransform)contentGO.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot     = new Vector2(0.5f, 1f);
            contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6; vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;    vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.content  = contentRt;
            sr.viewport = vpRt;

            // Section label for transcript
            CreateText(page, "TranscriptLabel", "Live Transcript", 13,
                TextAlignmentOptions.Left, new Vector2(4, 0),
                new Vector2(0.02f, 0.37f), new Vector2(0.55f, 0.42f))
                .color = new Color(0.65f, 0.75f, 0.90f, 0.85f);

            // ── Wire TranslationTabletPage ────────────────────────────────────
            pageUI = page.gameObject.AddComponent<TranslationTabletPage>();

            SetField(pageUI, "statusText",           statusText.GetComponent<TMP_Text>());
            SetField(pageUI, "statusIndicator",      statusDot.GetComponent<Image>());

            SetField(pageUI, "srcEnButton", srcEn.GetComponent<Button>());
            SetField(pageUI, "srcHeButton", srcHe.GetComponent<Button>());
            SetField(pageUI, "srcArButton", srcAr.GetComponent<Button>());
            SetField(pageUI, "srcThButton", srcTh.GetComponent<Button>());

            SetField(pageUI, "tgtEnButton", tgtEn.GetComponent<Button>());
            SetField(pageUI, "tgtHeButton", tgtHe.GetComponent<Button>());
            SetField(pageUI, "tgtArButton", tgtAr.GetComponent<Button>());
            SetField(pageUI, "tgtThButton", tgtTh.GetComponent<Button>());

            SetField(pageUI, "listenButton",         listenBtn.GetComponent<Button>());
            SetField(pageUI, "listenButtonLabel",    listenBtn.GetComponentInChildren<TMP_Text>());
            SetField(pageUI, "subtitleToggleButton", subToggle.GetComponent<Button>());
            SetField(pageUI, "subtitleToggleLabel",  subToggleLabel);
            SetField(pageUI, "saveToggleButton",     saveToggle.GetComponent<Button>());
            SetField(pageUI, "saveToggleLabel",      saveToggleLabel);
            SetField(pageUI, "clearButton",          clearBtn.GetComponent<Button>());
            SetField(pageUI, "saveNowButton",        saveNowBtn.GetComponent<Button>());
            SetField(pageUI, "transcriptListRoot",   contentGO.transform);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI helpers
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject CreatePanel(Transform parent, string name, Vector2 yOffsets,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(0, yOffsets.x);
            rt.offsetMax = new Vector2(0, yOffsets.y);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value,
            float size, TextAlignmentOptions align, Vector2 offset,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = offset;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = value; t.fontSize = size; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false;
            return t;
        }

        private static GameObject CreateButton(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject($"Button_{label.Replace(" ", "")}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120f, 36f);
            var img = go.AddComponent<Image>(); img.color = new Color(0.20f, 0.22f, 0.32f, 1f);
            go.AddComponent<Button>().targetGraphic = img;
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.AddComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 14; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return go;
        }

        private static void ResizeButton(GameObject btn, float w, float h)
        {
            var rt = btn.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static GameObject FindChildByName(Transform root, string name)
        {
            if (root == null) return null;
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

        private static void SetField(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(obj, value);
        }

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Removes the Translation tab from the Smart Farm Tablet and restores
        /// the original 5-tab layout (Overview / Irrigation / Alerts / Polls / History).
        ///
        /// Menu: Tools > Smart Farm > Remove Translation Tab from Tablet
        /// </summary>
        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Smart Farm/Remove Translation Tab from Tablet")]
        public static void RemoveTranslationTab()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Translation] Stop Play mode first.");
                return;
            }

            var tablet = GameObject.Find("SmartFarmTablet");
            if (tablet == null)
            {
                Debug.LogWarning("[Translation] SmartFarmTablet not found in scene.");
                return;
            }

            bool changed = false;

            // ── 1. Remove the Translate tab button ────────────────────────────
            // The setup script names it "Button_Translation"
            var tabBar = FindChildByName(tablet.transform, "TabBar");
            if (tabBar != null)
            {
                // Search for any button whose name contains "Translation" or "Translate"
                for (int i = tabBar.transform.childCount - 1; i >= 0; i--)
                {
                    var child = tabBar.transform.GetChild(i);
                    string n = child.name.ToLowerInvariant();
                    if (n.Contains("translat"))
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                        Debug.Log($"[Translation] Removed tab button: {child.name}");
                        changed = true;
                    }
                }

                // ── 2. Restore original 5-button sizes and positions ──────────
                // Original: 150 px wide, centred at -330, -165, 0, 165, 330
                string[] originalNames = { "Overview", "Irrigation", "Alerts", "Polls", "History" };
                float[]  originalX     = { -330f, -165f, 0f, 165f, 330f };

                for (int i = 0; i < tabBar.transform.childCount && i < originalNames.Length; i++)
                {
                    var btn = tabBar.transform.GetChild(i);
                    var rt  = btn.GetComponent<RectTransform>();
                    if (rt == null) continue;

                    Undo.RecordObject(rt, "Restore Tab Button");
                    rt.sizeDelta        = new Vector2(150f, 40f);
                    rt.anchoredPosition = new Vector2(originalX[i], rt.anchoredPosition.y);
                    changed = true;
                }

                Debug.Log("[Translation] Restored original 5-tab button layout (150 px, original positions).");
            }

            // ── 3. Remove the TranslationPage from ContentRoot ────────────────
            var contentRoot = FindChildByName(tablet.transform, "ContentRoot");
            if (contentRoot != null)
            {
                var translationPage = FindChildByName(contentRoot.transform, "TranslationPage");
                if (translationPage != null)
                {
                    Undo.DestroyObjectImmediate(translationPage);
                    Debug.Log("[Translation] Removed TranslationPage from ContentRoot.");
                    changed = true;
                }
            }

            // ── 4. Clear leftover Inspector references on TabletAppController ─
            var appCtrl = tablet.GetComponent<SmartFarm.TabletAppController>();
            if (appCtrl != null)
            {
                // Fields were already removed from the script; serialised nulls
                // are harmless, but record undo just in case.
                Undo.RecordObject(appCtrl, "Remove Translation Tab References");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[Translation] ✓ Tablet restored to original 5-tab layout.");
            }
            else
            {
                Debug.Log("[Translation] Nothing to remove — tablet already has no Translation tab.");
            }
        }
    }
}
