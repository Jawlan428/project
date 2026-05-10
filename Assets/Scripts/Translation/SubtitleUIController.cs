using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Translation
{
    /// <summary>
    /// Manages two subtitle display surfaces:
    ///
    ///   1. Floating world-space panels above each VR participant's head.
    ///      One SubtitleDisplay is created per unique speaker name and re-parented
    ///      to that avatar's head transform (auto-discovered by name search).
    ///
    ///   2. Scrolling transcript inside the Translation tablet tab.
    ///      TranslationTabletPage subscribes to OnTranslationReceived directly, so
    ///      this controller only drives the floating subtitles and any dedicated
    ///      transcript TMP_Text assigned in the Inspector.
    ///
    /// Place this component on TranslationHub or any persistent GameObject.
    /// </summary>
    public class SubtitleUIController : MonoBehaviour
    {
        [Header("World-Space Subtitle Settings")]
        [Tooltip("Offset above the avatar's head anchor (metres)")]
        [SerializeField] private Vector3 subtitleOffset     = new Vector3(0f, 0.25f, 0f);
        [Tooltip("World-space scale of the subtitle canvas (keep small for VR)")]
        [SerializeField] private float   canvasWorldScale   = 0.003f;
        [Tooltip("Width of subtitle panel in canvas pixels")]
        [SerializeField] private float   panelWidth         = 460f;
        [Tooltip("Height of subtitle panel in canvas pixels")]
        [SerializeField] private float   panelHeight        = 110f;
        [Tooltip("How long each subtitle stays visible (seconds)")]
        [SerializeField] [Range(2f, 20f)] private float subtitleDuration = 7f;

        [Header("Tablet Transcript (optional extra display)")]
        [Tooltip("Assign a TMP_Text inside the tablet to mirror all translations as plain text")]
        [SerializeField] private TMP_Text transcriptMirrorText;
        [SerializeField] [Range(5, 50)] private int maxMirrorLines = 12;

        // ── Private ───────────────────────────────────────────────────────────

        private TranslationManager _manager;
        private readonly Dictionary<string, SubtitleDisplay> _displays = new Dictionary<string, SubtitleDisplay>();
        private readonly Queue<string>                        _mirrorLines = new Queue<string>();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _manager = TranslationManager.Instance ?? FindFirstObjectByType<TranslationManager>();
            if (_manager != null)
            {
                _manager.OnTranslationReceived += HandleTranslation;
                _manager.OnSubtitleToggle      += HandleSubtitleToggle;
            }
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.OnTranslationReceived -= HandleTranslation;
                _manager.OnSubtitleToggle      -= HandleSubtitleToggle;
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void HandleTranslation(TranslationEntry entry)
        {
            if (_manager != null && _manager.ShowSubtitlesAboveAvatars)
                ShowAvatarSubtitle(entry);

            UpdateMirrorText(entry);
        }

        private void HandleSubtitleToggle(bool visible)
        {
            foreach (var d in _displays.Values)
                if (d != null) d.gameObject.SetActive(visible && d.gameObject.activeSelf);
        }

        // ── World-space floating subtitles ────────────────────────────────────

        private void ShowAvatarSubtitle(TranslationEntry entry)
        {
            if (!_displays.TryGetValue(entry.speakerName, out var display) || display == null)
            {
                var headTransform = FindAvatarHead(entry.speakerName);
                display = BuildSubtitleDisplay(headTransform, entry.speakerName);
                _displays[entry.speakerName] = display;
            }

            display.ShowSubtitle(entry);
        }

        private SubtitleDisplay BuildSubtitleDisplay(Transform parent, string speakerName)
        {
            var go = new GameObject($"Subtitle_{speakerName}", typeof(RectTransform));

            // Parent to avatar head if found, otherwise to this controller
            Transform anchor = parent ?? transform;
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = subtitleOffset;
            go.transform.localScale    = Vector3.one * canvasWorldScale;

            // World-space canvas
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.WorldSpace;
            canvas.sortingOrder = 120;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(panelWidth, panelHeight);

            // Semi-transparent background
            var bgGO = new GameObject("BG", typeof(RectTransform));
            bgGO.transform.SetParent(go.transform, false);
            var bgRt = (RectTransform)bgGO.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.72f);

            // Speaker label (small, top)
            var labelGO  = MakeText(go.transform, "SpeakerLabel", "", 18,
                TextAlignmentOptions.Center, new Vector2(0.02f, 0.72f), new Vector2(0.98f, 0.98f));
            labelGO.color = new Color(0.70f, 0.88f, 1f, 1f);

            // Subtitle text (larger, centre/bottom)
            var subText = MakeText(go.transform, "SubtitleText", "", 24,
                TextAlignmentOptions.Center, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.72f));
            subText.color = Color.white;
            subText.textWrappingMode = TextWrappingModes.Normal;

            var display = go.AddComponent<SubtitleDisplay>();
            display.Initialise(subText, labelGO, subtitleDuration);

            go.SetActive(false);
            return display;
        }

        // ── Mirror text (optional tablet panel) ───────────────────────────────

        private void UpdateMirrorText(TranslationEntry entry)
        {
            if (transcriptMirrorText == null) return;

            string line =
                $"<color=#B8E0FF>[{entry.timestampUtc.ToLocalTime():HH:mm}] {entry.speakerName}:</color>\n" +
                $"<size=90%>{entry.translatedText}</size>";

            _mirrorLines.Enqueue(line);
            if (_mirrorLines.Count > maxMirrorLines) _mirrorLines.Dequeue();

            transcriptMirrorText.text = string.Join("\n\n", _mirrorLines);
        }

        // ── Avatar head discovery ─────────────────────────────────────────────

        /// <summary>
        /// Searches the scene for an avatar belonging to <paramref name="playerName"/>.
        /// Checks: avatar GameObject name, PlayerIdentity, PlayerNameSync components.
        /// Falls back to null if not found (subtitle is then placed at the world origin).
        /// </summary>
        private static Transform FindAvatarHead(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return null;

            // Check PlayerNameSync components first — they hold the synced display name
            var nameSyncs = FindObjectsByType<PlayerNameSync>(FindObjectsSortMode.None);
            foreach (var ns in nameSyncs)
            {
                if (ns == null) continue;
                string n = ns.gameObject.name;
                if (n.IndexOf(playerName, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                return FindHeadChild(ns.transform) ?? ns.transform;
            }

            // Fall back to scanning all GameObjects by name
            var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t == null) continue;
                if (t.gameObject.name.IndexOf(playerName, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                return FindHeadChild(t) ?? t;
            }

            return null;
        }

        private static Transform FindHeadChild(Transform root)
        {
            string[] headNames = { "Head", "head", "HeadTarget", "CenterEyeAnchor", "Camera" };
            foreach (string n in headNames)
            {
                var t = root.Find(n);
                if (t != null) return t;
            }
            // Deep search one level below
            foreach (Transform child in root)
                foreach (string n in headNames)
                    if (child.name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return child;
            return null;
        }

        // ── Text helper ───────────────────────────────────────────────────────

        private static TMP_Text MakeText(Transform parent, string name, string value,
            float size, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt  = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text          = value;
            t.fontSize      = size;
            t.alignment     = align;
            t.color         = Color.white;
            t.raycastTarget = false;
            return t;
        }
    }
}
