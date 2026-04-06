using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Translation.Editor
{
    /// <summary>
    /// Full one-click setup for the standalone Translation Dashboard.
    ///
    /// Steps performed automatically:
    ///   1.  Create TranslationHub  (TranslationManager + SubtitleUIController)
    ///   2.  Create TranslationDashboard  (WorldSpace Canvas, fully wired)
    ///   3.  Ensure XR UI EventSystem  (XRUIInputModule, replaces StandaloneInputModule)
    ///   4.  Enable UI Interaction on all XR controllers in the scene
    ///   5.  Add UI raycast mask to XR interactors
    ///   6.  Set UI layer on every dashboard object
    ///   7.  Mark scene dirty + select dashboard
    ///   8.  Print clear next-steps summary
    ///
    /// Menu: Tools > Smart Farm > Create Translation Dashboard
    /// </summary>
    public static class TranslationDashboardEditor
    {
        private const float W       = 720f;
        private const float H       = 540f;
        private const float VRScale = 0.0035f;

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Smart Farm/Create Translation Dashboard")]
        public static void CreateDashboard()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Translation] Stop Play mode before running setup.");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[Translation] Open a scene first.");
                return;
            }

            // ── Step 1 — TranslationHub ───────────────────────────────────────
            var hub = EnsureTranslationHub();

            // ── Step 2 — Dashboard canvas ─────────────────────────────────────
            var dashboard = BuildDashboardCanvas(hub);

            // ── Step 3 — XR UI EventSystem ────────────────────────────────────
            EnsureXRUIEventSystem();

            // ── Step 4 & 5 — XR controller UI interaction ────────────────────
            int controllersPatched = EnableXRUIControllers();

            // ── Step 6 — UI layer ─────────────────────────────────────────────
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) SetLayerRecursive(dashboard, uiLayer);

            // ── Step 7 — Finish ───────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = dashboard;

            // ── Step 8 — Summary ──────────────────────────────────────────────
            Debug.Log(
                "─────────────────────────────────────────────────────────────\n" +
                "[Translation] ✓ Full Setup Complete\n" +
                "─────────────────────────────────────────────────────────────\n" +
                "Created:\n" +
                "  • TranslationHub       (TranslationManager + SubtitleUIController)\n" +
                "  • TranslationDashboard (WorldSpace Canvas — position freely)\n" +
                $"  • XR controllers patched: {controllersPatched}\n" +
                "\n" +
                "REQUIRED before pressing Play:\n" +
                "  1. Select TranslationHub in the Hierarchy\n" +
                "  2. In the Inspector → TranslationManager:\n" +
                "       Whisper Api Key        → your key from platform.openai.com/api-keys\n" +
                "       Libre Translate Endpoint → https://translate.argosopentech.com/translate\n" +
                "         (or your self-hosted instance — leave Api Key empty for free mirror)\n" +
                "\n" +
                "The dashboard is at world position (1.8, 1.5, 3.0).\n" +
                "Move it anywhere you like in the Scene view.\n" +
                "─────────────────────────────────────────────────────────────"
            );
        }

        // ─────────────────────────────────────────────────────────────────────
        // Step 1 — Hub
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject EnsureTranslationHub()
        {
            var existing = GameObject.Find("TranslationHub");
            if (existing != null)
            {
                if (existing.GetComponent<TranslationManager>()   == null) existing.AddComponent<TranslationManager>();
                if (existing.GetComponent<SubtitleUIController>() == null) existing.AddComponent<SubtitleUIController>();
                Debug.Log("[Translation] TranslationHub already exists — verified components.");
                return existing;
            }

            var hub = new GameObject("TranslationHub");
            Undo.RegisterCreatedObjectUndo(hub, "Translation Dashboard Setup");
            hub.AddComponent<TranslationManager>();
            hub.AddComponent<SubtitleUIController>();
            Debug.Log("[Translation] ✓ TranslationHub created.");
            return hub;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Step 2 — Dashboard canvas
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject BuildDashboardCanvas(GameObject hub)
        {
            // Remove any previous dashboard
            var prev = GameObject.Find("TranslationDashboard");
            if (prev != null) { Undo.DestroyObjectImmediate(prev); }

            // ── Root canvas ───────────────────────────────────────────────────
            var root = new GameObject("TranslationDashboard", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Translation Dashboard Setup");

            root.transform.position   = new Vector3(1.8f, 1.5f, 3.0f);
            root.transform.rotation   = Quaternion.Euler(0f, 170f, 0f);
            root.transform.localScale = Vector3.one * VRScale;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.WorldSpace;
            canvas.sortingOrder = 55;
            canvas.worldCamera  = FindCamera();

            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            root.AddComponent<GraphicRaycaster>();

            var xrRaycasterType = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, " +
                "Unity.XR.Interaction.Toolkit");
            if (xrRaycasterType != null)
                root.AddComponent(xrRaycasterType);

            var rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(W, H);

            // ── Background + border ───────────────────────────────────────────
            var bgImg = MakePanel(root.transform, "Background", Vector2.zero, Vector2.one,
                new Color(0.06f, 0.09f, 0.15f, 0.97f)).GetComponent<Image>();
            bgImg.raycastTarget = false;

            MakePanel(root.transform, "Border", Vector2.zero, Vector2.one,
                new Color(0.22f, 0.42f, 0.82f, 0.14f)).GetComponent<Image>().raycastTarget = false;

            // ── Header (62 px) ────────────────────────────────────────────────
            float headerBottom = 1f - 62f / H;
            MakePanel(root.transform, "Header",
                new Vector2(0f, headerBottom), new Vector2(1f, 1f),
                new Color(0.10f, 0.18f, 0.30f, 1f));

            var titleTxt = MakeText(root.transform, "TitleText",
                "🌐  Live Translation", 20, TextAlignmentOptions.Left,
                new Vector2(0.02f, headerBottom + 0.01f), new Vector2(0.60f, 0.99f));
            titleTxt.fontStyle = FontStyles.Bold;

            // Status dot
            var dotGO = MakePanel(root.transform, "StatusDot",
                new Vector2(0.62f, headerBottom + 0.015f), new Vector2(0.67f, 0.985f),
                new Color(0.55f, 0.55f, 0.55f, 1f));

            var statusTxt = MakeText(root.transform, "StatusText",
                "Ready", 14, TextAlignmentOptions.Left,
                new Vector2(0.68f, headerBottom + 0.01f), new Vector2(0.86f, 0.99f));
            statusTxt.color = new Color(0.75f, 0.85f, 1f, 1f);

            // Close button
            var closeBtn = MakeAbsButton(root.transform, "CloseBtn", "✕",
                new Vector2(0.90f, headerBottom + 0.01f), new Vector2(0.99f, 0.99f),
                new Color(0.60f, 0.14f, 0.14f, 1f), 18);

            // Mic level bar (background + fill)
            var micBgGO = new GameObject("MicLevelRoot", typeof(RectTransform));
            micBgGO.transform.SetParent(root.transform, false);
            var micBgRt = (RectTransform)micBgGO.transform;
            micBgRt.anchorMin = new Vector2(0.86f, headerBottom + 0.015f);
            micBgRt.anchorMax = new Vector2(0.89f, headerBottom + 0.9f - headerBottom * 0.1f);
            micBgRt.anchorMax = new Vector2(0.895f, 0.98f);
            micBgRt.anchorMin = new Vector2(0.855f, headerBottom + 0.01f);
            micBgRt.offsetMin = micBgRt.offsetMax = Vector2.zero;
            micBgGO.AddComponent<Image>().color = new Color(0.10f, 0.18f, 0.12f, 0.9f);
            micBgGO.SetActive(false);

            var micFillGO = new GameObject("MicFill", typeof(RectTransform));
            micFillGO.transform.SetParent(micBgGO.transform, false);
            var micFillRt = (RectTransform)micFillGO.transform;
            micFillRt.anchorMin = Vector2.zero; micFillRt.anchorMax = Vector2.one;
            micFillRt.offsetMin = micFillRt.offsetMax = Vector2.zero;
            var micFillImg = micFillGO.AddComponent<Image>();
            micFillImg.type       = Image.Type.Filled;
            micFillImg.fillMethod = Image.FillMethod.Vertical;
            micFillImg.fillOrigin = 0;
            micFillImg.fillAmount = 0f;
            micFillImg.color      = new Color(0.20f, 0.90f, 0.30f, 1f);

            // ── Divider 1 ─────────────────────────────────────────────────────
            MakePanel(root.transform, "Div1",
                new Vector2(0f, headerBottom - 0.004f), new Vector2(1f, headerBottom),
                new Color(0.25f, 0.45f, 0.80f, 0.55f));

            // ── Source language row ───────────────────────────────────────────
            float srcTop    = headerBottom - 0.025f;
            float srcBot    = srcTop       - 0.082f;

            var srcLabel = MakeText(root.transform, "SrcLabel", "Speaking in:", 14,
                TextAlignmentOptions.Left,
                new Vector2(0.01f, srcBot), new Vector2(0.20f, srcTop));
            srcLabel.color = new Color(0.72f, 0.82f, 1f, 0.90f);

            var (srcEn, srcHe, srcAr, srcTh) =
                MakeLangRow(root.transform, "Src", srcBot, srcTop, 0.21f, 0.99f);

            // ── Target language row ───────────────────────────────────────────
            float tgtTop    = srcBot - 0.012f;
            float tgtBot    = tgtTop - 0.082f;

            var tgtLabel = MakeText(root.transform, "TgtLabel", "Translate to:", 14,
                TextAlignmentOptions.Left,
                new Vector2(0.01f, tgtBot), new Vector2(0.20f, tgtTop));
            tgtLabel.color = new Color(0.72f, 0.82f, 1f, 0.90f);

            var (tgtEn, tgtHe, tgtAr, tgtTh) =
                MakeLangRow(root.transform, "Tgt", tgtBot, tgtTop, 0.21f, 0.99f);

            // Defaults: EN source, HE target
            srcEn.GetComponent<Image>().color = new Color(0.10f, 0.45f, 0.90f, 1f);
            tgtHe.GetComponent<Image>().color = new Color(0.10f, 0.45f, 0.90f, 1f);

            // ── Divider 2 ─────────────────────────────────────────────────────
            float div2 = tgtBot - 0.015f;
            MakePanel(root.transform, "Div2",
                new Vector2(0f, div2 - 0.004f), new Vector2(1f, div2),
                new Color(0.25f, 0.45f, 0.80f, 0.35f));

            // ── Controls row 1 ────────────────────────────────────────────────
            float ctrl1Top = div2 - 0.015f;
            float ctrl1Bot = ctrl1Top - 0.088f;

            var listenBtn = MakeAbsButton(root.transform, "ListenBtn",
                "▶  Start Listening",
                new Vector2(0.01f, ctrl1Bot), new Vector2(0.38f, ctrl1Top),
                new Color(0.10f, 0.72f, 0.20f, 1f), 15);

            var subToggleBtn = MakeAbsButton(root.transform, "SubtitleToggle",
                "Subtitles: ON",
                new Vector2(0.40f, ctrl1Bot), new Vector2(0.68f, ctrl1Top),
                new Color(0.10f, 0.62f, 0.28f, 1f), 13);

            var saveToggleBtn = MakeAbsButton(root.transform, "SaveToggle",
                "Auto-Save: ON",
                new Vector2(0.70f, ctrl1Bot), new Vector2(0.99f, ctrl1Top),
                new Color(0.10f, 0.62f, 0.28f, 1f), 13);

            // ── Controls row 2 ────────────────────────────────────────────────
            float ctrl2Top = ctrl1Bot - 0.008f;
            float ctrl2Bot = ctrl2Top - 0.072f;

            var clearBtn = MakeAbsButton(root.transform, "ClearBtn", "Clear",
                new Vector2(0.01f, ctrl2Bot), new Vector2(0.23f, ctrl2Top),
                new Color(0.40f, 0.18f, 0.18f, 1f), 13);

            var saveNowBtn = MakeAbsButton(root.transform, "SaveNowBtn", "Save Transcript",
                new Vector2(0.25f, ctrl2Bot), new Vector2(0.48f, ctrl2Top),
                new Color(0.18f, 0.44f, 0.72f, 1f), 13);

            // Test button — sends a hardcoded phrase, bypasses STT
            var testBtn = MakeAbsButton(root.transform, "TestBtn", "⬤ Test",
                new Vector2(0.50f, ctrl2Bot), new Vector2(0.71f, ctrl2Top),
                new Color(0.55f, 0.28f, 0.08f, 1f), 13);

            // API Key button — opens the API key entry overlay
            var apiKeyToggleBtn = MakeAbsButton(root.transform, "ApiKeyBtn", "🔑 API Key",
                new Vector2(0.73f, ctrl2Bot), new Vector2(0.99f, ctrl2Top),
                new Color(0.22f, 0.18f, 0.45f, 1f), 12);

            // ── API Key overlay panel (hidden by default) ─────────────────────
            // Floats over the transcript area; toggled by the 🔑 API Key button.
            var apiKeyPanelGO = new GameObject("ApiKeyPanel", typeof(RectTransform));
            apiKeyPanelGO.transform.SetParent(root.transform, false);
            var apRt = (RectTransform)apiKeyPanelGO.transform;
            apRt.anchorMin = new Vector2(0.04f, 0.04f);
            apRt.anchorMax = new Vector2(0.96f, ctrl2Bot - 0.01f);
            apRt.offsetMin = apRt.offsetMax = Vector2.zero;
            apiKeyPanelGO.AddComponent<Image>().color = new Color(0.07f, 0.08f, 0.16f, 0.98f);

            // Title
            var apTitle = new GameObject("ApiKeyTitle", typeof(RectTransform));
            apTitle.transform.SetParent(apiKeyPanelGO.transform, false);
            var apTitleRt = (RectTransform)apTitle.transform;
            apTitleRt.anchorMin = new Vector2(0.03f, 0.72f); apTitleRt.anchorMax = new Vector2(0.97f, 0.95f);
            apTitleRt.offsetMin = apTitleRt.offsetMax = Vector2.zero;
            var apTitleTxt = apTitle.AddComponent<TextMeshProUGUI>();
            apTitleTxt.text = "OpenAI Whisper API Key";
            apTitleTxt.fontSize = 15; apTitleTxt.fontStyle = FontStyles.Bold;
            apTitleTxt.color = new Color(0.75f, 0.88f, 1f, 1f);
            apTitleTxt.alignment = TextAlignmentOptions.Center;
            apTitleTxt.raycastTarget = false;

            // Hint
            var apHint = new GameObject("ApiKeyHint", typeof(RectTransform));
            apHint.transform.SetParent(apiKeyPanelGO.transform, false);
            var apHintRt = (RectTransform)apHint.transform;
            apHintRt.anchorMin = new Vector2(0.03f, 0.56f); apHintRt.anchorMax = new Vector2(0.97f, 0.73f);
            apHintRt.offsetMin = apHintRt.offsetMax = Vector2.zero;
            var apHintTxt = apHint.AddComponent<TextMeshProUGUI>();
            apHintTxt.text = "Get yours at  platform.openai.com/api-keys";
            apHintTxt.fontSize = 11;
            apHintTxt.color = new Color(0.60f, 0.72f, 0.90f, 0.85f);
            apHintTxt.alignment = TextAlignmentOptions.Center;
            apHintTxt.raycastTarget = false;

            // Input field
            var apiKeyInputGO = MakeInputField(apiKeyPanelGO.transform, "ApiKeyInput",
                "Paste your sk-... key here",
                new Vector2(0.03f, 0.34f), new Vector2(0.97f, 0.56f));

            // Confirm button
            var apiKeyConfirmBtn = MakeAbsButton(apiKeyPanelGO.transform, "ApiKeyConfirmBtn", "✓  Save Key",
                new Vector2(0.55f, 0.06f), new Vector2(0.97f, 0.32f),
                new Color(0.10f, 0.58f, 0.22f, 1f), 13);

            // Cancel button (calls the same toggle to close)
            var apiKeyCancelBtn = MakeAbsButton(apiKeyPanelGO.transform, "ApiKeyCancelBtn", "✕  Cancel",
                new Vector2(0.03f, 0.06f), new Vector2(0.45f, 0.32f),
                new Color(0.38f, 0.16f, 0.16f, 1f), 13);

            // Wire the cancel button to call the toggle via UnityEvent
            apiKeyCancelBtn.GetComponent<Button>().onClick.AddListener(() => apiKeyPanelGO.SetActive(false));

            apiKeyPanelGO.SetActive(false); // hidden by default

            // ── Transcript section ────────────────────────────────────────────
            float transcriptLabelTop = ctrl2Bot - 0.012f;
            float transcriptLabelBot = transcriptLabelTop - 0.040f;

            var transcriptLabel = MakeText(root.transform, "TranscriptLabel",
                "Live Transcript", 13, TextAlignmentOptions.Left,
                new Vector2(0.01f, transcriptLabelBot), new Vector2(0.50f, transcriptLabelTop));
            transcriptLabel.color = new Color(0.60f, 0.72f, 0.90f, 0.85f);

            // Persistent error label — hidden by default, turns red on API failure
            var errorGO = new GameObject("ErrorLabel", typeof(RectTransform));
            errorGO.transform.SetParent(root.transform, false);
            var errorRt = (RectTransform)errorGO.transform;
            errorRt.anchorMin = new Vector2(0.01f, transcriptLabelBot);
            errorRt.anchorMax = new Vector2(0.99f, transcriptLabelTop);
            errorRt.offsetMin = errorRt.offsetMax = Vector2.zero;
            var errorTxt = errorGO.AddComponent<TextMeshProUGUI>();
            errorTxt.text              = "";
            errorTxt.fontSize          = 13;
            errorTxt.alignment         = TextAlignmentOptions.Right;
            errorTxt.color             = new Color(1f, 0.38f, 0.18f, 1f);
            errorTxt.raycastTarget     = false;
            errorTxt.enableWordWrapping = false;
            errorGO.SetActive(false);

            // Divider 3
            MakePanel(root.transform, "Div3",
                new Vector2(0.55f, transcriptLabelBot + 0.01f),
                new Vector2(0.99f, transcriptLabelBot + 0.012f),
                new Color(0.35f, 0.50f, 0.80f, 0.35f));

            float panelTop = transcriptLabelBot - 0.005f;

            // ── Transcript panel — NO ScrollRect, NO Mask, NO Viewport ───────
            // Plain panel: rows are direct children, positioned manually.
            // This is the only reliable approach for WorldSpace Canvas.
            var transcriptPanel = new GameObject("TranscriptPanel", typeof(RectTransform));
            transcriptPanel.transform.SetParent(root.transform, false);
            var panelRt = (RectTransform)transcriptPanel.transform;
            panelRt.anchorMin = new Vector2(0.01f, 0.01f);
            panelRt.anchorMax = new Vector2(0.99f, panelTop);
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
            transcriptPanel.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.55f);

            // ListRoot = the panel itself (rows parented directly here)
            var contentGO = transcriptPanel;

            // ── Empty-state placeholder ───────────────────────────────────────
            var emptyGO = new GameObject("EmptyState", typeof(RectTransform));
            emptyGO.transform.SetParent(contentGO.transform, false);
            var emptyRt = (RectTransform)emptyGO.transform;
            emptyRt.anchorMin = new Vector2(0.03f, 0.1f);
            emptyRt.anchorMax = new Vector2(0.97f, 0.9f);
            emptyRt.offsetMin = emptyRt.offsetMax = Vector2.zero;
            var emptyTxt = emptyGO.AddComponent<TextMeshProUGUI>();
            emptyTxt.text =
                "No translations yet.\n\n" +
                "① Select source & target language\n" +
                "② Press  <b>▶ Start Listening</b>  and speak\n" +
                "③ Translated text will appear here\n\n" +
                "<size=80%><color=#FF7733>⚠  Requires Whisper API key\n" +
                "Select TranslationHub → TranslationManager\n" +
                "and set the  Whisper Api Key  field</color></size>";
            emptyTxt.fontSize          = 15;
            emptyTxt.alignment         = TextAlignmentOptions.Center;
            emptyTxt.color             = new Color(0.65f, 0.75f, 0.90f, 0.85f);
            emptyTxt.raycastTarget     = false;
            emptyTxt.enableWordWrapping = true;

            // ── Wire TranslationDashboard ─────────────────────────────────────
            var db = root.AddComponent<TranslationDashboard>();
            var mgr = hub.GetComponent<TranslationManager>();

            SetField(db, "translationManager",   mgr);
            SetField(db, "titleText",            titleTxt);
            SetField(db, "statusText",           statusTxt);
            SetField(db, "statusIndicator",      dotGO.GetComponent<Image>());
            SetField(db, "closeButton",          closeBtn.GetComponent<Button>());
            SetField(db, "emptyStateLabel",      emptyGO);
            SetField(db, "errorLabel",           errorTxt);
            SetField(db, "transcriptListRoot",   contentGO.transform);
            SetField(db, "srcEnButton",          srcEn.GetComponent<Button>());
            SetField(db, "srcHeButton",          srcHe.GetComponent<Button>());
            SetField(db, "srcArButton",          srcAr.GetComponent<Button>());
            SetField(db, "srcThButton",          srcTh.GetComponent<Button>());
            SetField(db, "tgtEnButton",          tgtEn.GetComponent<Button>());
            SetField(db, "tgtHeButton",          tgtHe.GetComponent<Button>());
            SetField(db, "tgtArButton",          tgtAr.GetComponent<Button>());
            SetField(db, "tgtThButton",          tgtTh.GetComponent<Button>());
            SetField(db, "listenButton",         listenBtn.GetComponent<Button>());
            SetField(db, "listenButtonLabel",    listenBtn.GetComponentInChildren<TMP_Text>());
            SetField(db, "subtitleToggleButton", subToggleBtn.GetComponent<Button>());
            SetField(db, "subtitleToggleLabel",  subToggleBtn.GetComponentInChildren<TMP_Text>());
            SetField(db, "saveToggleButton",     saveToggleBtn.GetComponent<Button>());
            SetField(db, "saveToggleLabel",      saveToggleBtn.GetComponentInChildren<TMP_Text>());
            SetField(db, "clearButton",          clearBtn.GetComponent<Button>());
            SetField(db, "saveNowButton",        saveNowBtn.GetComponent<Button>());
            SetField(db, "testButton",           testBtn.GetComponent<Button>());
            SetField(db, "apiKeyToggleButton",   apiKeyToggleBtn.GetComponent<Button>());
            SetField(db, "apiKeyPanel",          apiKeyPanelGO);
            SetField(db, "apiKeyInputField",     apiKeyInputGO.GetComponent<TMP_InputField>());
            SetField(db, "apiKeyConfirmButton",  apiKeyConfirmBtn.GetComponent<Button>());
            SetField(db, "micLevelBar",          micFillImg);
            SetField(db, "micLevelRoot",         micBgGO);

            Debug.Log("[Translation] ✓ TranslationDashboard canvas built and wired.");
            return root;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Step 3 — XR UI EventSystem
        // ─────────────────────────────────────────────────────────────────────

        private static void EnsureXRUIEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var esGO = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGO, "Translation Dashboard Setup");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                eventSystem = esGO.GetComponent<UnityEngine.EventSystems.EventSystem>();
                Debug.Log("[Translation] ✓ EventSystem created.");
            }

            // Upgrade to XRUIInputModule if XRI is present
            var xrInputType = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, " +
                "Unity.XR.Interaction.Toolkit");

            if (xrInputType != null && eventSystem.GetComponent(xrInputType) == null)
            {
                var standalone = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (standalone != null) Object.DestroyImmediate(standalone);
                eventSystem.gameObject.AddComponent(xrInputType);
                Debug.Log("[Translation] ✓ XRUIInputModule added to EventSystem.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Steps 4 & 5 — XR controllers
        // ─────────────────────────────────────────────────────────────────────

        private static int EnableXRUIControllers()
        {
            int patched = 0;
            int uiLayer = LayerMask.NameToLayer("UI");

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);

                // Enable UI interaction flag
                var uiProp = so.FindProperty("m_EnableUIInteraction");
                if (uiProp != null && !uiProp.boolValue)
                {
                    uiProp.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    patched++;
                }

                // Ensure UI layer is included in the raycast mask
                if (uiLayer >= 0)
                {
                    var maskProp = so.FindProperty("m_RaycastMask");
                    if (maskProp != null)
                    {
                        var bits = maskProp.FindPropertyRelative("m_Bits");
                        if (bits != null)
                        {
                            int current = bits.intValue;
                            int uiBit   = 1 << uiLayer;
                            if ((current & uiBit) == 0)
                            {
                                bits.intValue = current | uiBit | 1; // UI + Default
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                    }
                }
            }

            if (patched > 0)
                Debug.Log($"[Translation] ✓ Enabled UI Interaction on {patched} XR interactor(s).");

            return patched;
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI helpers
        // ─────────────────────────────────────────────────────────────────────

        private static GameObject MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static TMP_Text MakeText(Transform parent, string name, string value,
            float size, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = value; t.fontSize = size; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false; t.enableWordWrapping = true;
            return t;
        }

        private static GameObject MakeInputField(Transform parent, string name, string placeholder,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.22f, 1f);

            var field = go.AddComponent<TMP_InputField>();
            field.contentType = TMP_InputField.ContentType.Password;

            // Text Area (masked)
            var taGO = new GameObject("Text Area", typeof(RectTransform));
            taGO.transform.SetParent(go.transform, false);
            var taRt = (RectTransform)taGO.transform;
            taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(8f, 4f); taRt.offsetMax = new Vector2(-8f, -4f);
            taGO.AddComponent<RectMask2D>();

            // Placeholder
            var phGO = new GameObject("Placeholder", typeof(RectTransform));
            phGO.transform.SetParent(taGO.transform, false);
            var phRt = (RectTransform)phGO.transform;
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = phRt.offsetMax = Vector2.zero;
            var phTxt = phGO.AddComponent<TextMeshProUGUI>();
            phTxt.text = placeholder;
            phTxt.fontSize = 13;
            phTxt.color = new Color(0.50f, 0.55f, 0.65f, 0.75f);
            phTxt.alignment = TextAlignmentOptions.MidlineLeft;
            phTxt.enableWordWrapping = false;
            phTxt.raycastTarget = false;

            // Text
            var txtGO = new GameObject("Text", typeof(RectTransform));
            txtGO.transform.SetParent(taGO.transform, false);
            var txtRt = (RectTransform)txtGO.transform;
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.text = "";
            txt.fontSize = 13;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.enableWordWrapping = false;
            txt.raycastTarget = false;

            field.textViewport      = taRt;
            field.textComponent     = txt;
            field.placeholder       = phTxt;
            field.targetGraphic     = bg;
            field.caretWidth        = 2;
            field.customCaretColor  = true;
            field.caretColor        = Color.white;
            field.selectionColor    = new Color(0.25f, 0.50f, 1f, 0.40f);

            return go;
        }

        private static GameObject MakeAbsButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color color, float fontSize = 14)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = color;
            go.AddComponent<Button>().targetGraphic = img;
            var tGO = new GameObject("Text", typeof(RectTransform));
            tGO.transform.SetParent(go.transform, false);
            var tRt = (RectTransform)tGO.transform;
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;
            var t = tGO.AddComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = fontSize; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return go;
        }

        private static (GameObject en, GameObject he, GameObject ar, GameObject th)
            MakeLangRow(Transform parent, string prefix,
                float yMin, float yMax, float xStart, float xEnd)
        {
            float w    = (xEnd - xStart - 0.008f * 3f) / 4f;
            float gap  = 0.008f;
            Color idle = new Color(0.18f, 0.20f, 0.30f, 1f);

            GameObject Btn(string code, string label, float x0)
            {
                var go = MakeAbsButton(parent, $"{prefix}{code}", label,
                    new Vector2(x0, yMin), new Vector2(x0 + w, yMax), idle, 15);
                go.GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Bold;
                return go;
            }

            float x = xStart;
            var en = Btn("En", "EN", x); x += w + gap;
            var he = Btn("He", "HE", x); x += w + gap;
            var ar = Btn("Ar", "AR", x); x += w + gap;
            var th = Btn("Th", "TH", x);
            return (en, he, ar, th);
        }

        private static Camera FindCamera()
        {
            if (Camera.main != null) return Camera.main;
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null) return cam;
            var offset = GameObject.Find("Camera Offset");
            return offset != null ? offset.GetComponentInChildren<Camera>() : null;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static void SetField(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            f?.SetValue(obj, value);
        }
    }
}
