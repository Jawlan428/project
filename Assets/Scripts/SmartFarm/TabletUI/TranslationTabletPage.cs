using System.Collections.Generic;
using TMPro;
using Translation;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm
{
    /// <summary>
    /// Translation tab on the Smart Farm Tablet.
    ///
    /// Layout wired via Inspector:
    ///
    ///   Title: "Live Translation"
    ///
    ///   ── Status bar ───────────────────────────────────────
    ///   [●] Listening… / Processing… / Stopped
    ///
    ///   ── Source language ──────────────────────────────────
    ///   Label: "Speaking in:"
    ///   [EN]  [HE]  [AR]  [TH]
    ///
    ///   ── Target language ──────────────────────────────────
    ///   Label: "Translate to:"
    ///   [EN]  [HE]  [AR]  [TH]
    ///
    ///   ── Controls ─────────────────────────────────────────
    ///   [Start Listening]  [Stop]
    ///   [Subtitles: ON]    [Auto-Save: ON]
    ///   [Clear]            [Save Now]
    ///
    ///   ── Live transcript ───────────────────────────────────
    ///   Scrollable list of TranslationEntryRowUI items
    /// </summary>
    public class TranslationTabletPage : MonoBehaviour
    {
        [Header("Manager Reference")]
        [SerializeField] private Translation.TranslationManager translationManager;

        [Header("Status Bar")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image    statusIndicator;

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

        [Header("Live Transcript List")]
        [Tooltip("Parent Transform with VerticalLayoutGroup — rows are spawned here")]
        [SerializeField] private Transform transcriptListRoot;
        [Tooltip("Optional: assign a TranslationEntryRowUI prefab for custom row styling")]
        [SerializeField] private TranslationEntryRowUI rowPrefab;
        [SerializeField] [Range(5, 60)] private int maxVisibleRows = 30;

        // ── Colours ───────────────────────────────────────────────────────────

        private static readonly Color ActiveLang    = new Color(0.10f, 0.45f, 0.90f, 1f);
        private static readonly Color InactiveLang  = new Color(0.18f, 0.20f, 0.28f, 1f);
        private static readonly Color ListenGreen   = new Color(0.10f, 0.72f, 0.20f, 1f);
        private static readonly Color StopRed       = new Color(0.82f, 0.18f, 0.10f, 1f);
        private static readonly Color ToggleOn      = new Color(0.10f, 0.62f, 0.28f, 1f);
        private static readonly Color ToggleOff     = new Color(0.32f, 0.32f, 0.42f, 1f);
        private static readonly Color StatusActive  = new Color(0.18f, 0.88f, 0.36f, 1f);
        private static readonly Color StatusIdle    = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color StatusBusy    = new Color(0.95f, 0.72f, 0.10f, 1f);

        private readonly List<GameObject> _rows = new List<GameObject>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (translationManager == null)
                translationManager = Translation.TranslationManager.Instance
                                     ?? FindFirstObjectByType<Translation.TranslationManager>();

            EnsureListLayout();
        }

        private void Start()
        {
            if (translationManager == null)
                translationManager = Translation.TranslationManager.Instance
                                     ?? FindFirstObjectByType<Translation.TranslationManager>();

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
        }

        private void OnDisable()
        {
            if (translationManager == null) return;
            translationManager.OnTranslationReceived -= OnTranslationReceived;
            translationManager.OnStatusChanged       -= OnStatusChanged;
            translationManager.OnListeningChanged    -= OnListeningChanged;
            translationManager.OnLanguageChanged     -= OnLanguageChanged;
        }

        // ── Button wiring ─────────────────────────────────────────────────────

        private void WireButtons()
        {
            // Source
            if (srcEnButton != null) srcEnButton.onClick.AddListener(() => SetSrc(Translation.TranslationLanguage.English));
            if (srcHeButton != null) srcHeButton.onClick.AddListener(() => SetSrc(Translation.TranslationLanguage.Hebrew));
            if (srcArButton != null) srcArButton.onClick.AddListener(() => SetSrc(Translation.TranslationLanguage.Arabic));
            if (srcThButton != null) srcThButton.onClick.AddListener(() => SetSrc(Translation.TranslationLanguage.Thai));

            // Target
            if (tgtEnButton != null) tgtEnButton.onClick.AddListener(() => SetTgt(Translation.TranslationLanguage.English));
            if (tgtHeButton != null) tgtHeButton.onClick.AddListener(() => SetTgt(Translation.TranslationLanguage.Hebrew));
            if (tgtArButton != null) tgtArButton.onClick.AddListener(() => SetTgt(Translation.TranslationLanguage.Arabic));
            if (tgtThButton != null) tgtThButton.onClick.AddListener(() => SetTgt(Translation.TranslationLanguage.Thai));

            // Controls
            if (listenButton          != null) listenButton.onClick.AddListener(OnListenToggle);
            if (subtitleToggleButton  != null) subtitleToggleButton.onClick.AddListener(OnSubtitleToggle);
            if (saveToggleButton      != null) saveToggleButton.onClick.AddListener(OnSaveToggle);
            if (clearButton           != null) clearButton.onClick.AddListener(OnClear);
            if (saveNowButton         != null) saveNowButton.onClick.AddListener(OnSaveNow);
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void SetSrc(Translation.TranslationLanguage lang)
        {
            translationManager?.SetSourceLanguage(lang);
            HighlightSrcLang(lang);
        }

        private void SetTgt(Translation.TranslationLanguage lang)
        {
            translationManager?.SetTargetLanguage(lang);
            HighlightTgtLang(lang);
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
        }

        private void OnSaveNow() => translationManager?.SaveHistoryToFile();

        // ── Manager event callbacks ───────────────────────────────────────────

        private void OnTranslationReceived(Translation.TranslationEntry entry) => SpawnRow(entry);

        private void OnStatusChanged(string status)
        {
            if (statusText != null) statusText.text = status;
            if (statusIndicator != null)
            {
                bool listening  = translationManager != null && translationManager.IsListening;
                bool processing = translationManager != null && translationManager.IsProcessing;
                statusIndicator.color = processing ? StatusBusy : (listening ? StatusActive : StatusIdle);
            }
        }

        private void OnListeningChanged(bool listening)
        {
            if (listenButton     != null) SetButtonColor(listenButton, listening ? StopRed : ListenGreen);
            if (listenButtonLabel != null) listenButtonLabel.text = listening ? "Stop" : "Start Listening";
        }

        private void OnLanguageChanged(Translation.TranslationLanguage src, Translation.TranslationLanguage tgt)
        {
            HighlightSrcLang(src);
            HighlightTgtLang(tgt);
        }

        // ── Transcript list ───────────────────────────────────────────────────

        private void SpawnRow(Translation.TranslationEntry entry)
        {
            if (transcriptListRoot == null) return;

            GameObject go;
            if (rowPrefab != null)
            {
                go = Instantiate(rowPrefab.gameObject, transcriptListRoot);
                go.GetComponent<TranslationEntryRowUI>()?.Bind(entry);
            }
            else
            {
                go = BuildSimpleRow(entry);
                go.transform.SetParent(transcriptListRoot, false);
            }

            go.SetActive(true);
            go.transform.SetAsFirstSibling(); // newest at top
            _rows.Insert(0, go);

            // Trim old rows
            while (_rows.Count > maxVisibleRows)
            {
                int last = _rows.Count - 1;
                if (_rows[last] != null) Destroy(_rows[last]);
                _rows.RemoveAt(last);
            }
        }

        private static GameObject BuildSimpleRow(Translation.TranslationEntry entry)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.16f, 0.26f, 0.85f);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 76;
            le.minHeight       = 60;

            // Speaker + time
            MakeText(go.transform, "Header",
                $"[{entry.timestampUtc.ToLocalTime():HH:mm:ss}]  {entry.speakerName}", 13,
                TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.62f), new Vector2(0.98f, 0.96f))
                .color = new Color(0.70f, 0.88f, 1f, 1f);

            // Original
            MakeText(go.transform, "Original",
                $"{entry.sourceLanguage.ToDisplayName()}: {entry.originalText}", 12,
                TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.32f), new Vector2(0.98f, 0.62f))
                .color = new Color(0.80f, 0.80f, 0.80f, 1f);

            // Translated
            MakeText(go.transform, "Translated",
                $"{entry.targetLanguage.ToDisplayName()}: {entry.translatedText}", 13,
                TextAlignmentOptions.Left,
                new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.34f))
                .color = Color.white;

            return go;
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (translationManager == null) return;
            HighlightSrcLang(translationManager.SourceLanguage);
            HighlightTgtLang(translationManager.TargetLanguage);
            OnListeningChanged(translationManager.IsListening);
            RefreshToggle(subtitleToggleButton, subtitleToggleLabel,
                translationManager.ShowSubtitlesAboveAvatars, "Subtitles: ON", "Subtitles: OFF");
            RefreshToggle(saveToggleButton, saveToggleLabel,
                translationManager.SaveTranslatedTranscript, "Auto-Save: ON", "Auto-Save: OFF");
            OnStatusChanged("Ready");
        }

        private void HighlightSrcLang(Translation.TranslationLanguage lang)
        {
            SetButtonColor(srcEnButton, lang == Translation.TranslationLanguage.English ? ActiveLang : InactiveLang);
            SetButtonColor(srcHeButton, lang == Translation.TranslationLanguage.Hebrew  ? ActiveLang : InactiveLang);
            SetButtonColor(srcArButton, lang == Translation.TranslationLanguage.Arabic  ? ActiveLang : InactiveLang);
            SetButtonColor(srcThButton, lang == Translation.TranslationLanguage.Thai    ? ActiveLang : InactiveLang);
        }

        private void HighlightTgtLang(Translation.TranslationLanguage lang)
        {
            SetButtonColor(tgtEnButton, lang == Translation.TranslationLanguage.English ? ActiveLang : InactiveLang);
            SetButtonColor(tgtHeButton, lang == Translation.TranslationLanguage.Hebrew  ? ActiveLang : InactiveLang);
            SetButtonColor(tgtArButton, lang == Translation.TranslationLanguage.Arabic  ? ActiveLang : InactiveLang);
            SetButtonColor(tgtThButton, lang == Translation.TranslationLanguage.Thai    ? ActiveLang : InactiveLang);
        }

        private static void RefreshToggle(Button btn, TMP_Text label, bool isOn, string onText, string offText)
        {
            SetButtonColor(btn, isOn ? ToggleOn : ToggleOff);
            if (label != null) label.text = isOn ? onText : offText;
        }

        private static void SetButtonColor(Button btn, Color color)
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
            t.raycastTarget     = false;
            t.textWrappingMode  = TextWrappingModes.Normal;
            return t;
        }

        private void EnsureListLayout()
        {
            if (transcriptListRoot == null) return;

            var vlg = transcriptListRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = transcriptListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 6;
            vlg.padding              = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment       = TextAnchor.UpperCenter;
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var csf = transcriptListRoot.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = transcriptListRoot.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            var rt = transcriptListRoot as RectTransform
                     ?? transcriptListRoot.GetComponent<RectTransform>();
            if (rt != null) rt.pivot = new Vector2(0.5f, 1f);
        }
    }
}
