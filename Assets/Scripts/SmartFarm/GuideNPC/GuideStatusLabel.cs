using TMPro;
using UnityEngine;

namespace SmartFarm.GuideNPC
{
    /// <summary>
    /// A small world-space TMP label that floats above the guide and shows the
    /// welcome message / walking status. Built entirely in code so no prefab is
    /// required. Billboards toward the player so it's always readable.
    /// </summary>
    public class GuideStatusLabel : MonoBehaviour
    {
        private Canvas _canvas;
        private CanvasGroup _group;
        private TMP_Text _title;
        private TMP_Text _subtitle;
        private float _targetAlpha;

        public static GuideStatusLabel Create(Transform parent, float height)
        {
            var go = new GameObject("GuideStatusLabel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);

            var label = go.AddComponent<GuideStatusLabel>();
            label.Build();
            return label;
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 50;

            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100f;

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(520f, 200f);
            rt.localScale = Vector3.one * 0.0016f; // ~0.83m wide, comfortable to read.

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _group.alpha = 0f;

            // Rounded dark panel.
            var bg = CreateImage(rt, "Panel", new Color(0.04f, 0.12f, 0.10f, 0.92f));
            Fill(bg.rectTransform, Vector2.zero, Vector2.one);

            var accent = CreateImage(rt, "Accent", new Color(0.30f, 1f, 0.66f, 1f));
            var aRT = accent.rectTransform;
            aRT.anchorMin = new Vector2(0f, 0f);
            aRT.anchorMax = new Vector2(1f, 0.03f);
            aRT.offsetMin = aRT.offsetMax = Vector2.zero;

            _title = CreateText(rt, "Title", "Welcome", 44f, FontStyles.Bold,
                new Color(0.92f, 1f, 0.96f), TextAlignmentOptions.Center);
            var tRT = _title.rectTransform;
            tRT.anchorMin = new Vector2(0.05f, 0.45f);
            tRT.anchorMax = new Vector2(0.95f, 0.92f);
            tRT.offsetMin = tRT.offsetMax = Vector2.zero;

            _subtitle = CreateText(rt, "Subtitle", "", 26f, FontStyles.Normal,
                new Color(0.65f, 0.95f, 0.82f), TextAlignmentOptions.Center);
            var sRT = _subtitle.rectTransform;
            sRT.anchorMin = new Vector2(0.05f, 0.1f);
            sRT.anchorMax = new Vector2(0.95f, 0.45f);
            sRT.offsetMin = sRT.offsetMax = Vector2.zero;
        }

        public void Show(string title, string subtitle)
        {
            if (_title != null) _title.text = title;
            if (_subtitle != null) _subtitle.text = subtitle;
            _targetAlpha = 1f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _targetAlpha = 0f;
        }

        public void FaceCamera(Transform player)
        {
            if (_group != null)
                _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, Time.deltaTime * 4f);

            Transform cam = player;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;

            Vector3 dir = transform.position - cam.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // ── helpers ──
        private static UnityEngine.UI.Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<UnityEngine.UI.Image>();
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
