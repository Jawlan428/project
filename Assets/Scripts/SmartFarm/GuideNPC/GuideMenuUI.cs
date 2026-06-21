using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.GuideNPC
{
    /// <summary>
    /// Floating world-space VR menu shown next to the guide. Builds itself in code
    /// (Canvas + TrackedDeviceGraphicRaycaster + large buttons) so it works out of
    /// the box with the XR Interaction Toolkit ray / poke interactors.
    ///
    /// Buttons are generated from the guide's destination list, so adding a
    /// destination automatically adds a button. The four farm areas are:
    /// Crop Field, Meeting Area, Smart Screens, Training Room.
    /// </summary>
    public class GuideMenuUI : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Where the menu floats relative to the guide (local offset, metres).")]
        [SerializeField] private Vector3 localOffset = new Vector3(0.7f, 1.4f, 0.2f);

        [Tooltip("Physical width of the menu in metres.")]
        [SerializeField] private float menuWidthMeters = 0.6f;

        [Tooltip("Height of each button in canvas pixels.")]
        [SerializeField] private float buttonHeight = 130f;

        [Tooltip("Re-face the player every frame while visible.")]
        [SerializeField] private bool billboard = true;

        [Header("Theme")]
        [SerializeField] private Color panelColor  = new Color(0.04f, 0.12f, 0.10f, 0.96f);
        [SerializeField] private Color headerColor = new Color(0.06f, 0.20f, 0.16f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.12f, 0.42f, 0.30f, 1f);
        [SerializeField] private Color buttonHover = new Color(0.20f, 0.65f, 0.45f, 1f);
        [SerializeField] private Color textColor   = new Color(0.95f, 1f, 0.97f, 1f);
        [SerializeField] private Color accentColor = new Color(0.30f, 1f, 0.66f, 1f);

        public bool IsVisible { get; private set; }

        private SmartFarmGuideNPC _guide;
        private Transform _player;
        private Canvas _canvas;
        private RectTransform _root;
        private readonly List<Button> _buttons = new List<Button>();
        private bool _built;

        public void Initialize(SmartFarmGuideNPC guide)
        {
            _guide = guide;
            if (Camera.main != null) _player = Camera.main.transform;
            EnsureBuilt();
        }

        private void Update()
        {
            if (!IsVisible) return;
            FollowAndFacePlayer();
        }

        public void Show()
        {
            EnsureBuilt();
            PositionNextToGuide();
            gameObject.SetActive(true);
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            IsVisible = true;
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            IsVisible = false;
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_player == null && Camera.main != null) _player = Camera.main.transform;

            var canvasGO = new GameObject("GuideMenuCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 60;
            if (Camera.main != null) _canvas.worldCamera = Camera.main;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            canvasGO.AddComponent<GraphicRaycaster>();
            AddTrackedDeviceRaycaster(canvasGO);

            _root = canvasGO.GetComponent<RectTransform>();
            const float canvasWidth = 600f;
            _root.sizeDelta = new Vector2(canvasWidth, 760f);
            float scale = menuWidthMeters / canvasWidth;
            _root.localScale = new Vector3(scale, scale, scale);

            // Panel background.
            var bg = MakeImage(_root, "Panel", panelColor);
            Fill(bg.rectTransform, Vector2.zero, Vector2.one);
            bg.raycastTarget = true; // catch stray rays so they don't pass through.

            // Header.
            var header = MakeImage(_root, "Header", headerColor);
            var hRT = header.rectTransform;
            hRT.anchorMin = new Vector2(0f, 0.86f);
            hRT.anchorMax = new Vector2(1f, 1f);
            hRT.offsetMin = hRT.offsetMax = Vector2.zero;

            var title = MakeText(header.rectTransform, "Title", "Farm Guide", 40f,
                FontStyles.Bold, textColor, TextAlignmentOptions.Center);
            Fill(title.rectTransform, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f));

            var accent = MakeImage(_root, "Accent", accentColor);
            var accRT = accent.rectTransform;
            accRT.anchorMin = new Vector2(0f, 0.855f);
            accRT.anchorMax = new Vector2(1f, 0.862f);
            accRT.offsetMin = accRT.offsetMax = Vector2.zero;

            BuildButtons();

            Hide();
        }

        private void BuildButtons()
        {
            foreach (var b in _buttons)
                if (b != null) Object.Destroy(b.gameObject);
            _buttons.Clear();

            var entries = BuildEntryList();

            // Vertical stack inside the body area (below the header).
            float topPad = 0.83f;     // start just under the header
            float bottomPad = 0.03f;
            float available = topPad - bottomPad;
            int count = Mathf.Max(1, entries.Count);
            float slot = available / count;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float maxY = topPad - slot * i;
                float minY = topPad - slot * (i + 1);
                // small gap between buttons
                float gap = slot * 0.12f;

                var btn = MakeButton(_root, "Btn_" + entry.area, entry.label,
                    new Vector2(0.06f, minY + gap * 0.5f),
                    new Vector2(0.94f, maxY - gap * 0.5f));

                var captured = entry.area;
                btn.onClick.AddListener(() =>
                {
                    if (_guide != null) _guide.GoTo(captured);
                });
                _buttons.Add(btn);
            }
        }

        /// <summary>
        /// Use the guide's configured destinations if present; otherwise fall back
        /// to the four default areas so buttons always appear.
        /// </summary>
        private List<GuideDestinationEntry> BuildEntryList()
        {
            var list = new List<GuideDestinationEntry>();

            if (_guide != null && _guide.Destinations != null && _guide.Destinations.Count > 0)
            {
                foreach (var d in _guide.Destinations)
                    if (d != null) list.Add(d);
            }

            if (list.Count == 0)
            {
                list.Add(new GuideDestinationEntry(GuideArea.CropField,    GuideAreaLabels.For(GuideArea.CropField),    null));
                list.Add(new GuideDestinationEntry(GuideArea.MeetingArea,  GuideAreaLabels.For(GuideArea.MeetingArea),  null));
                list.Add(new GuideDestinationEntry(GuideArea.SmartScreens, GuideAreaLabels.For(GuideArea.SmartScreens), null));
                list.Add(new GuideDestinationEntry(GuideArea.TrainingRoom, GuideAreaLabels.For(GuideArea.TrainingRoom), null));
            }
            return list;
        }

        /// <summary>Rebuild buttons (call after changing the guide's destinations at runtime).</summary>
        public void RefreshButtons()
        {
            if (!_built) { EnsureBuilt(); return; }
            BuildButtons();
        }

        // ── Positioning ───────────────────────────────────────────────────────

        private void PositionNextToGuide()
        {
            if (_guide == null) return;
            var t = _guide.transform;
            transform.position = t.TransformPoint(localOffset);
            FollowAndFacePlayer();
        }

        private void FollowAndFacePlayer()
        {
            if (_guide != null)
                transform.position = _guide.transform.TransformPoint(localOffset);

            if (!billboard) return;
            if (_player == null && Camera.main != null) _player = Camera.main.transform;
            if (_player == null) return;

            Vector3 dir = transform.position - _player.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // ── UI primitives ─────────────────────────────────────────────────────

        private static void AddTrackedDeviceRaycaster(GameObject go)
        {
            var type = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (type != null && go.GetComponent(type) == null)
                go.AddComponent(type);
        }

        private Button MakeButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = buttonColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonHover;
            colors.pressedColor = accentColor;
            colors.selectedColor = buttonHover;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var t = MakeText(rt, "Label", label, 34f, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
            Fill(t.rectTransform, new Vector2(0.06f, 0f), new Vector2(0.94f, 1f));
            return btn;
        }

        private static Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text MakeText(Transform parent, string name, string text, float size,
            FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Fill(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
