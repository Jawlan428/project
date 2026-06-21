using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Harvest
{
    /// <summary>
    /// A small world-space TMP label that floats above an apple and tells the
    /// player whether it is ready for harvesting. Built entirely in code so no
    /// prefab is required.
    ///
    /// It follows its apple at a fixed world height and a fixed world size –
    /// independent of the apple's own transform scale – and billboards toward the
    /// player so it stays readable in VR.
    ///
    /// Ready    -> green "Ready to Harvest"
    /// Not Ready -> red  "Not Ready Yet"
    /// </summary>
    public class AppleHarvestLabel : MonoBehaviour
    {
        private static readonly Color ReadyGreen = new Color(0.30f, 1.00f, 0.45f, 1f);
        private static readonly Color NotReadyRed = new Color(1.00f, 0.32f, 0.28f, 1f);
        private static readonly Color PanelDark = new Color(0.04f, 0.06f, 0.06f, 0.92f);

        // Desired on-screen size in world units (panel ~0.34m wide), kept constant
        // regardless of how the apple itself is scaled.
        private const float WorldScale = 0.0008f;

        private CanvasGroup _group;
        private Image _accent;
        private Image _dot;
        private TMP_Text _text;
        private Transform _follow;
        private Transform _cam;
        private float _worldHeight;
        private float _targetAlpha;

        /// <summary>Creates a label that follows <paramref name="apple"/> at the given world height.</summary>
        public static AppleHarvestLabel Create(Transform apple, float worldHeight)
        {
            var go = new GameObject("AppleHarvestLabel");
            // Parent to the apple so it is destroyed with it, but the transform is
            // fully driven in LateUpdate so the apple's scale never affects it.
            go.transform.SetParent(apple, false);

            var label = go.AddComponent<AppleHarvestLabel>();
            label._follow = apple;
            label._worldHeight = Mathf.Max(0.05f, worldHeight);
            label.Build();
            return label;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 60;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 120f;

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(420f, 110f);

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _group.alpha = 0f;

            var bg = CreateImage(rt, "Panel", PanelDark);
            Fill(bg.rectTransform, Vector2.zero, Vector2.one);

            _accent = CreateImage(rt, "Accent", ReadyGreen);
            var aRT = _accent.rectTransform;
            aRT.anchorMin = new Vector2(0f, 0f);
            aRT.anchorMax = new Vector2(1f, 0.06f);
            aRT.offsetMin = aRT.offsetMax = Vector2.zero;

            _dot = CreateImage(rt, "StatusDot", ReadyGreen);
            _dot.sprite = BuildCircleSprite();
            _dot.type = Image.Type.Simple;
            var dRT = _dot.rectTransform;
            dRT.anchorMin = new Vector2(0.04f, 0.30f);
            dRT.anchorMax = new Vector2(0.18f, 0.78f);
            dRT.offsetMin = dRT.offsetMax = Vector2.zero;

            _text = CreateText(rt, "Status", "Ready to Harvest", 38f, FontStyles.Bold,
                Color.white, TextAlignmentOptions.Left);
            var tRT = _text.rectTransform;
            tRT.anchorMin = new Vector2(0.22f, 0.08f);
            tRT.anchorMax = new Vector2(0.97f, 0.92f);
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;

            // Place once immediately so it isn't stuck at the origin for a frame.
            DriveTransform();
        }

        /// <summary>Updates label colours and text for the given ripeness.</summary>
        public void SetStatus(bool ready)
        {
            Color c = ready ? ReadyGreen : NotReadyRed;
            if (_accent != null) _accent.color = c;
            if (_dot != null) _dot.color = c;
            if (_text != null)
            {
                _text.color = c;
                _text.text = ready ? "Ready to Harvest" : "Not Ready Yet";
            }
        }

        public void Show()
        {
            _targetAlpha = 1f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _targetAlpha = 0f;
        }

        private void LateUpdate()
        {
            if (_group != null)
                _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.deltaTime * 6f);

            DriveTransform();
        }

        // Drives world position, a fixed world scale, and the billboard rotation.
        private void DriveTransform()
        {
            if (_follow == null) return;

            transform.position = _follow.position + Vector3.up * _worldHeight;

            // Counter the parent's scale so the panel is always the same world size.
            Vector3 ls = _follow.lossyScale;
            transform.localScale = new Vector3(
                WorldScale / Mathf.Max(0.0001f, ls.x),
                WorldScale / Mathf.Max(0.0001f, ls.y),
                WorldScale / Mathf.Max(0.0001f, ls.z));

            Transform cam = ResolveCamera();
            if (cam == null) return;

            Vector3 dir = transform.position - cam.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private Transform ResolveCamera()
        {
            if (_cam != null && _cam.gameObject.activeInHierarchy) return _cam;
            if (Camera.main != null) { _cam = Camera.main.transform; return _cam; }
            var any = Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
            _cam = any != null ? any.transform : null;
            return _cam;
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, float size,
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
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Fill(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // Generates a soft solid circle sprite at runtime (no texture asset needed).
        private static Sprite _circleSprite;
        private static Sprite BuildCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "AppleStatusDot" };
            float r = size * 0.5f;
            Vector2 center = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float a = Mathf.Clamp01((r - d) / 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circleSprite;
        }
    }
}
