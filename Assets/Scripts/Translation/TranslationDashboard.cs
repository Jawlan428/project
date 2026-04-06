using System.Collections.Generic;
using TMPro;
using Translation;
using UnityEngine;
using UnityEngine.UI;

namespace Translation
{
    /// <summary>
    /// Standalone world-space Translation Dashboard.
    ///
    /// A self-contained floating panel placed anywhere in the VR scene.
    /// NOT part of the Smart Farm Tablet — it exists as its own WorldSpace Canvas.
    ///
    /// Layout (all wired via Inspector / setup editor):
    ///
    ///   ┌─────────────────────────────────────────────────────────┐
    ///   │  🌐 Live Translation              [●] Ready   [✕ Close] │  ← header
    ///   ├─────────────────────────────────────────────────────────┤
    ///   │  Speaking in:   [EN]  [HE]  [AR]  [TH]                 │
    ///   │  Translate to:  [EN]  [HE]  [AR]  [TH]                 │
    ///   ├─────────────────────────────────────────────────────────┤
    ///   │  [▶ Start Listening]  [Subtitles: ON]  [Auto-Save: ON]  │
    ///   │                              [Clear]   [Save Transcript]│
    ///   ├─────────────────────────────────────────────────────────┤
    ///   │  Live Transcript ─────────────────────────────────────  │
    ///   │  ┌─────────────────────────────────────────────────┐    │
    ///   │  │ [HH:mm:ss] Speaker Name                         │    │
    ///   │  │  SourceLang: original text                      │    │
    ///   │  │  TargetLang: translated text                    │    │
    ///   │  └─────────────────────────────────────────────────┘    │
    ///   └─────────────────────────────────────────────────────────┘
    ///
    /// To show / hide: call Show() / Hide() or toggle the GameObject.
    /// </summary>
    public class TranslationDashboard : MonoBehaviour
    {
        [Header("Manager Reference")]
        [SerializeField] private TranslationManager translationManager;

        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image    statusIndicator;
        [SerializeField] private Button   closeButton;

        [Header("Source Language Buttons")]
        [SerializeField] private Button srcEnButton;
        [SerializeField] private Button srcHeButton;
        [SerializeField] private Button srcArButton;
        [SerializeField] private Button srcThButton;

        [Header("Target Language Buttons")]
        [SerializeField] private Button tgtEnButton;
        [SerializeField] private Button tgtHeButton;
        [SerializeField] private Button tgtArButton;
        [SerializeField] private Button tgtThButton;

        [Header("Control Buttons")]
        [SerializeField] private Button   listenButton;
        [SerializeField] private TMP_Text listenButtonLabel;
        [SerializeField] private Button   subtitleToggleButton;
        [SerializeField] private TMP_Text subtitleToggleLabel;
        [SerializeField] private Button   saveToggleButton;
        [SerializeField] private TMP_Text saveToggleLabel;
        [SerializeField] private Button   clearButton;
        [SerializeField] private Button   saveNowButton;
        [Tooltip("Sends a hardcoded Arabic phrase to verify the translation pipeline without STT")]
        [SerializeField] private Button   testButton;

        [Header("Error Display")]
        [Tooltip("Shows the most recent error message in red — stays until a translation succeeds")]
        [SerializeField] private TMP_Text errorLabel;

        [Header("API Key Setup")]
        [Tooltip("Overlay panel shown when the user taps the API Key button — hidden by default")]
        [SerializeField] private GameObject      apiKeyPanel;
        [SerializeField] private TMP_InputField  apiKeyInputField;
        [SerializeField] private Button          apiKeyConfirmButton;
        [SerializeField] private Button          apiKeyToggleButton;

        [Header("Microphone Indicator")]
        [Tooltip("Fill Image used as a live microphone level bar")]
        [SerializeField] private Image  micLevelBar;
        [Tooltip("Background of the mic level bar — shown only while listening")]
        [SerializeField] private GameObject micLevelRoot;

        [Header("Live Transcript")]
        [Tooltip("Content Transform inside the ScrollRect — has VerticalLayoutGroup")]
        [SerializeField] private Transform transcriptListRoot;
        [Tooltip("Shown when no translations have arrived yet")]
        [SerializeField] private GameObject emptyStateLabel;
        [SerializeField] [Range(5, 80)] private int maxVisibleRows = 40;

        // ── Colours ───────────────────────────────────────────────────────────

        private static readonly Color ActiveLang   = new Color(0.10f, 0.45f, 0.90f, 1f);
        private static readonly Color InactiveLang = new Color(0.18f, 0.20f, 0.30f, 1f);
        private static readonly Color ListenGreen  = new Color(0.10f, 0.72f, 0.20f, 1f);
        private static readonly Color StopRed      = new Color(0.82f, 0.18f, 0.10f, 1f);
        private static readonly Color ToggleOn     = new Color(0.10f, 0.62f, 0.28f, 1f);
        private static readonly Color ToggleOff    = new Color(0.30f, 0.30f, 0.42f, 1f);
        private static readonly Color StatusActive = new Color(0.18f, 0.88f, 0.36f, 1f);
        private static readonly Color StatusIdle   = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color StatusBusy   = new Color(0.95f, 0.72f, 0.10f, 1f);

        private readonly List<GameObject> _rows = new List<GameObject>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Update()
        {
            // Live mic level bar — fills proportionally to voice amplitude
            if (micLevelBar != null && translationManager != null)
            {
                float level = translationManager.MicLevel;
                // Scale 0–0.05 → 0–1 fill (most speech sits in this range)
                micLevelBar.fillAmount = Mathf.Clamp01(level / 0.05f);

                // Colour: green when below threshold, yellow when voice detected
                micLevelBar.color = level > 0.005f
                    ? new Color(0.20f, 0.90f, 0.30f, 1f)
                    : new Color(0.35f, 0.55f, 0.35f, 0.55f);
            }

            if (micLevelRoot != null && translationManager != null)
                micLevelRoot.SetActive(translationManager.IsListening);
        }

        private void Awake()
        {
            if (translationManager == null)
                translationManager = TranslationManager.Instance
                                     ?? FindFirstObjectByType<TranslationManager>();
            EnsureListLayout();
        }

        private void Start()
        {
            if (translationManager == null)
                translationManager = TranslationManager.Instance
                                     ?? FindFirstObjectByType<TranslationManager>();

            WireButtons();
            RefreshAll();
        }

        private void OnEnable()
        {
            if (translationManager == null) return;
            translationManager.OnTranslationReceived += OnTranslationReceived;
            translationManager.OnStatusChanged       += OnStatusChanged;
            translationManager.OnListeningChanged    += OnListeningChanged;
            translationManager.OnLanguageChanged     += OnLanguageChanged;
            translationManager.OnErrorChanged        += OnErrorChanged;
        }

        private void OnDisable()
        {
            if (translationManager == null) return;
            translationManager.OnTranslationReceived -= OnTranslationReceived;
            translationManager.OnStatusChanged       -= OnStatusChanged;
            translationManager.OnListeningChanged    -= OnListeningChanged;
            translationManager.OnLanguageChanged     -= OnLanguageChanged;
            translationManager.OnErrorChanged        -= OnErrorChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
        public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

        // ── Button wiring ─────────────────────────────────────────────────────

        private void WireButtons()
        {
            // Source
            if (srcEnButton != null) srcEnButton.onClick.AddListener(() => SetSrc(TranslationLanguage.English));
            if (srcHeButton != null) srcHeButton.onClick.AddListener(() => SetSrc(TranslationLanguage.Hebrew));
            if (srcArButton != null) srcArButton.onClick.AddListener(() => SetSrc(TranslationLanguage.Arabic));
            if (srcThButton != null) srcThButton.onClick.AddListener(() => SetSrc(TranslationLanguage.Thai));

            // Target
            if (tgtEnButton != null) tgtEnButton.onClick.AddListener(() => SetTgt(TranslationLanguage.English));
            if (tgtHeButton != null) tgtHeButton.onClick.AddListener(() => SetTgt(TranslationLanguage.Hebrew));
            if (tgtArButton != null) tgtArButton.onClick.AddListener(() => SetTgt(TranslationLanguage.Arabic));
            if (tgtThButton != null) tgtThButton.onClick.AddListener(() => SetTgt(TranslationLanguage.Thai));

            // Controls
            if (listenButton         != null) listenButton.onClick.AddListener(OnListenToggle);
            if (subtitleToggleButton != null) subtitleToggleButton.onClick.AddListener(OnSubtitleToggle);
            if (saveToggleButton     != null) saveToggleButton.onClick.AddListener(OnSaveToggle);
            if (clearButton          != null) clearButton.onClick.AddListener(OnClear);
            if (saveNowButton        != null) saveNowButton.onClick.AddListener(OnSaveNow);
            if (closeButton          != null) closeButton.onClick.AddListener(Hide);
            if (testButton           != null) testButton.onClick.AddListener(OnTestTranslation);
            if (apiKeyToggleButton   != null) apiKeyToggleButton.onClick.AddListener(OnApiKeyToggle);
            if (apiKeyConfirmButton  != null) apiKeyConfirmButton.onClick.AddListener(OnApiKeyConfirm);
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void SetSrc(TranslationLanguage lang)
        {
            translationManager?.SetSourceLanguage(lang);
            HighlightSrc(lang);
        }

        private void SetTgt(TranslationLanguage lang)
        {
            translationManager?.SetTargetLanguage(lang);
            HighlightTgt(lang);
        }

        private void OnListenToggle()  => translationManager?.ToggleListening();

        private void OnSubtitleToggle()
        {
            if (translationManager == null) return;
            translationManager.ShowSubtitlesAboveAvatars = !translationManager.ShowSubtitlesAboveAvatars;
            RefreshToggle(subtitleToggleButton, subtitleToggleLabel,
                translationManager.ShowSubtitlesAboveAvatars, "Subtitles: ON", "Subtitles: OFF");
        }

        private void OnSaveToggle()
        {
            if (translationManager == null) return;
            translationManager.SaveTranslatedTranscript = !translationManager.SaveTranslatedTranscript;
            RefreshToggle(saveToggleButton, saveToggleLabel,
                translationManager.SaveTranslatedTranscript, "Auto-Save: ON", "Auto-Save: OFF");
        }

        private void OnClear()
        {
            translationManager?.ClearHistory();
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i] != null) Destroy(_rows[i]);
            _rows.Clear();


            if (emptyStateLabel != null) emptyStateLabel.SetActive(true);
        }

        private void OnSaveNow() => translationManager?.SaveHistoryToFile();

        /// <summary>
        /// Sends a hardcoded Arabic test phrase directly to the translation pipeline —
        /// bypasses the microphone and Whisper STT completely.
        /// Use this to verify the LibreTranslate API and the UI are both working.
        /// </summary>
        private void OnTestTranslation()
        {
            string testPhrase = translationManager != null &&
                                translationManager.SourceLanguage == TranslationLanguage.Arabic
                ? "مرحبا بالجميع، كيف حالكم؟"   // Hello everyone, how are you?
                : "Hello, this is a translation test.";

            translationManager?.SubmitText(testPhrase, "Test");
            if (statusText != null) statusText.text = "Sending test phrase…";
        }

        private void OnApiKeyToggle()
        {
            if (apiKeyPanel == null) return;
            bool nowVisible = !apiKeyPanel.activeSelf;
            apiKeyPanel.SetActive(nowVisible);
            if (nowVisible && apiKeyInputField != null)
                apiKeyInputField.ActivateInputField();
        }

        private void OnApiKeyConfirm()
        {
            if (apiKeyInputField == null || translationManager == null) return;
            string key = apiKeyInputField.text.Trim();
            if (string.IsNullOrEmpty(key)) return;

            translationManager.SetWhisperApiKey(key);
            apiKeyInputField.text = "";
            if (apiKeyPanel != null) apiKeyPanel.SetActive(false);
            if (statusText != null) statusText.text = "Whisper API key saved ✓";
            if (errorLabel != null) { errorLabel.gameObject.SetActive(false); errorLabel.text = ""; }
        }

        // ── Manager event callbacks ───────────────────────────────────────────

        private void OnTranslationReceived(TranslationEntry entry) => SpawnRow(entry);

        private void OnStatusChanged(string status)
        {
            if (statusText != null) statusText.text = status;

            bool isError = !string.IsNullOrEmpty(status) &&
                           (status.StartsWith("STT:") ||
                            status.StartsWith("Translation:") ||
                            status.Contains("error", System.StringComparison.OrdinalIgnoreCase) ||
                            status.Contains("not configured", System.StringComparison.OrdinalIgnoreCase));

            if (statusText != null)
                statusText.color = isError ? new Color(1f, 0.42f, 0.22f, 1f)
                                           : new Color(0.75f, 0.85f, 1f, 1f);

            if (statusIndicator != null)
            {
                bool listening  = translationManager != null && translationManager.IsListening;
                bool processing = translationManager != null && translationManager.IsProcessing;
                statusIndicator.color = isError    ? new Color(1f, 0.35f, 0.10f, 1f)
                                      : processing ? StatusBusy
                                      : listening  ? StatusActive
                                      : StatusIdle;
            }
        }

        private void OnListeningChanged(bool listening)
        {
            SetBtnColor(listenButton, listening ? StopRed : ListenGreen);
            if (listenButtonLabel != null)
                listenButtonLabel.text = listening ? "⏹  Stop" : "▶  Start Listening";
        }

        private void OnErrorChanged(string error)
        {
            if (errorLabel == null) return;
            bool hasError = !string.IsNullOrEmpty(error);
            errorLabel.gameObject.SetActive(hasError);
            if (hasError) errorLabel.text = $"⚠  {error}";
        }

        private void OnLanguageChanged(TranslationLanguage src, TranslationLanguage tgt)
        {
            HighlightSrc(src);
            HighlightTgt(tgt);
        }

        // ── Transcript rows ───────────────────────────────────────────────────

        private const float RowHeight = 118f;
        private const float RowGap   = 5f;

        private void SpawnRow(TranslationEntry entry)
        {
            if (transcriptListRoot == null) return;

            if (emptyStateLabel != null) emptyStateLabel.SetActive(false);

            // Trim oldest rows first so index maths is correct
            while (_rows.Count >= maxVisibleRows)
            {
                int last = _rows.Count - 1;
                if (_rows[last] != null) Destroy(_rows[last]);
                _rows.RemoveAt(last);
            }

            var go = BuildRow(entry);
            go.SetActive(true);
            go.transform.SetParent(transcriptListRoot, false);
            _rows.Insert(0, go);

            // ── Manual positioning inside plain TranscriptPanel ───────────────
            // Newest row at the top, older rows step downward.
            // No ScrollRect / Viewport / Mask — just plain RectTransform children.
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null) continue;
                var rt = _rows[i].GetComponent<RectTransform>();
                if (rt == null) continue;

                rt.anchorMin        = new Vector2(0f, 1f);   // anchor to top of panel
                rt.anchorMax        = new Vector2(1f, 1f);
                rt.pivot            = new Vector2(0.5f, 1f); // pivot at top-centre
                rt.sizeDelta        = new Vector2(0f, RowHeight);
                rt.anchoredPosition = new Vector2(0f, -i * (RowHeight + RowGap));
            }
        }

        private static GameObject BuildRow(TranslationEntry entry)
        {
            var go = new GameObject("Row", typeof(RectTransform));

            var rowRt = (RectTransform)go.transform;
            rowRt.anchorMin        = new Vector2(0f, 1f);
            rowRt.anchorMax        = new Vector2(1f, 1f);
            rowRt.pivot            = new Vector2(0.5f, 1f);
            rowRt.sizeDelta        = new Vector2(0f, RowHeight);
            rowRt.anchoredPosition = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = entry.targetLanguage switch
            {
                TranslationLanguage.Hebrew  => new Color(0.10f, 0.18f, 0.35f, 0.92f),
                TranslationLanguage.Arabic  => new Color(0.16f, 0.10f, 0.30f, 0.92f),
                TranslationLanguage.Thai    => new Color(0.08f, 0.22f, 0.20f, 0.92f),
                _                           => new Color(0.10f, 0.14f, 0.24f, 0.90f)
            };

            // ── Header: timestamp + speaker (always LTR) ─────────────────────
            var header = MakeText(go.transform, "Header",
                $"[{entry.timestampUtc.ToLocalTime():HH:mm:ss}]  {entry.speakerName}",
                11, TextAlignmentOptions.Left,
                new Vector2(0.01f, 0.84f), new Vector2(0.99f, 0.99f));
            header.color = new Color(0.70f, 0.88f, 1f, 1f);

            // ── Source label (always LTR — just the language name) ────────────
            var srcLabel = MakeText(go.transform, "SrcLabel",
                $"{entry.sourceLanguage.ToDisplayName()}:",
                10, TextAlignmentOptions.Left,
                new Vector2(0.01f, 0.67f), new Vector2(0.99f, 0.83f));
            srcLabel.color = new Color(0.65f, 0.72f, 0.85f, 0.80f);

            // ── Source content (RTL-aware — handles Arabic/Hebrew source) ─────
            var srcContent = MakeText(go.transform, "SrcContent",
                entry.originalText,
                12, TextAlignmentOptions.Left,
                new Vector2(0.01f, 0.50f), new Vector2(0.99f, 0.67f));
            srcContent.color = new Color(0.78f, 0.78f, 0.78f, 1f);
            TranslationFontHelper.ApplyContent(srcContent, entry.sourceLanguage);

            // ── Target label (always LTR) ─────────────────────────────────────
            var tgtLabel = MakeText(go.transform, "TgtLabel",
                $"{entry.targetLanguage.ToDisplayName()}:",
                10, TextAlignmentOptions.Left,
                new Vector2(0.01f, 0.32f), new Vector2(0.99f, 0.49f));
            tgtLabel.color = new Color(0.65f, 0.80f, 1f, 0.85f);

            // ── Target content (RTL-aware — handles Arabic/Hebrew translation) ─
            var tgtContent = MakeText(go.transform, "TgtContent",
                entry.translatedText,
                13, TextAlignmentOptions.Left,
                new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.32f));
            tgtContent.color = Color.white;
            TranslationFontHelper.ApplyContent(tgtContent, entry.targetLanguage);

            return go;
        }

        // ── UI refresh ────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (translationManager == null) return;
            HighlightSrc(translationManager.SourceLanguage);
            HighlightTgt(translationManager.TargetLanguage);
            OnListeningChanged(translationManager.IsListening);
            RefreshToggle(subtitleToggleButton, subtitleToggleLabel,
                translationManager.ShowSubtitlesAboveAvatars, "Subtitles: ON", "Subtitles: OFF");
            RefreshToggle(saveToggleButton, saveToggleLabel,
                translationManager.SaveTranslatedTranscript, "Auto-Save: ON", "Auto-Save: OFF");
            if (statusText != null) statusText.text = "Ready";
        }

        private void HighlightSrc(TranslationLanguage lang)
        {
            SetBtnColor(srcEnButton, lang == TranslationLanguage.English ? ActiveLang : InactiveLang);
            SetBtnColor(srcHeButton, lang == TranslationLanguage.Hebrew  ? ActiveLang : InactiveLang);
            SetBtnColor(srcArButton, lang == TranslationLanguage.Arabic  ? ActiveLang : InactiveLang);
            SetBtnColor(srcThButton, lang == TranslationLanguage.Thai    ? ActiveLang : InactiveLang);
        }

        private void HighlightTgt(TranslationLanguage lang)
        {
            SetBtnColor(tgtEnButton, lang == TranslationLanguage.English ? ActiveLang : InactiveLang);
            SetBtnColor(tgtHeButton, lang == TranslationLanguage.Hebrew  ? ActiveLang : InactiveLang);
            SetBtnColor(tgtArButton, lang == TranslationLanguage.Arabic  ? ActiveLang : InactiveLang);
            SetBtnColor(tgtThButton, lang == TranslationLanguage.Thai    ? ActiveLang : InactiveLang);
        }

        private static void RefreshToggle(Button btn, TMP_Text label,
            bool isOn, string onText, string offText)
        {
            SetBtnColor(btn, isOn ? ToggleOn : ToggleOff);
            if (label != null) label.text = isOn ? onText : offText;
        }

        private static void SetBtnColor(Button btn, Color color)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = color;
        }

        private static TMP_Text MakeText(Transform parent, string name, string value,
            float size, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text             = value;
            t.fontSize         = size;
            t.alignment        = align;
            t.color            = Color.white;
            t.raycastTarget    = false;
            t.enableWordWrapping = true;
            return t;
        }

        private void EnsureListLayout()
        {
            // Nothing needed — TranscriptPanel is a plain panel, no auto-layout.
        }
    }
}
