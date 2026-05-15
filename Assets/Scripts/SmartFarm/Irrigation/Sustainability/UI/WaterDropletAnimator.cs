using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.Sustainability.UI
{
    /// <summary>
    /// Drives a handful of small UI water-drop icons inside a panel to drift
    /// downward and fade, giving the Sustainability Monitor an animated, alive
    /// feel without any particle system overhead.
    ///
    /// Quest VR friendly: zero allocations after Awake, no GC pressure, runs in
    /// a single Update.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/UI/Water Droplet Animator")]
    [DisallowMultipleComponent]
    public class WaterDropletAnimator : MonoBehaviour
    {
        [Header("Container")]
        [SerializeField] private RectTransform container;
        [SerializeField, Range(2, 24)] private int   dropletCount = 10;
        [SerializeField] private Sprite dropletSprite;
        [SerializeField] private Color  dropletColor = new Color(0.55f, 0.85f, 1f, 0.85f);

        [Header("Motion")]
        [SerializeField, Range(20f, 320f)] private float minFallSpeed = 60f;
        [SerializeField, Range(20f, 480f)] private float maxFallSpeed = 140f;
        [SerializeField, Range(2f, 18f)]   private float minSize      = 6f;
        [SerializeField, Range(4f, 36f)]   private float maxSize      = 14f;

        // ── Internal ─────────────────────────────────────────────────────────

        private struct Droplet
        {
            public RectTransform rt;
            public Image         img;
            public float         speed;
            public float         alpha;
            public float         alphaSpeed;
        }

        private readonly List<Droplet> _drops = new();
        private Rect _bounds;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (container == null) container = transform as RectTransform;
            BuildDroplets();
        }

        private void OnEnable()
        {
            BuildDroplets();
        }

        private void BuildDroplets()
        {
            if (container == null) return;

            // Clear any prior children we own
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var c = container.GetChild(i);
                if (c != null && c.name.StartsWith("Droplet_"))
                    Destroy(c.gameObject);
            }
            _drops.Clear();

            _bounds = container.rect;

            for (int i = 0; i < dropletCount; i++)
            {
                var go = new GameObject($"Droplet_{i}", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(container, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                float size = Random.Range(minSize, maxSize);
                rt.sizeDelta = new Vector2(size, size * 1.4f);

                var img = go.AddComponent<Image>();
                img.color = dropletColor;
                img.raycastTarget = false;
                if (dropletSprite != null) img.sprite = dropletSprite;

                _drops.Add(new Droplet
                {
                    rt         = rt,
                    img        = img,
                    speed      = Random.Range(minFallSpeed, maxFallSpeed),
                    alpha      = Random.value,
                    alphaSpeed = Random.Range(0.4f, 1.1f),
                });

                Reset(_drops.Count - 1, randomY: true);
            }
        }

        private void Update()
        {
            if (container == null || _drops.Count == 0) return;
            _bounds = container.rect;
            float dt = Time.deltaTime;

            for (int i = 0; i < _drops.Count; i++)
            {
                var d = _drops[i];
                if (d.rt == null) continue;

                var pos = d.rt.anchoredPosition;
                pos.y -= d.speed * dt;
                d.rt.anchoredPosition = pos;

                d.alpha += d.alphaSpeed * dt;
                float a = 0.30f + 0.60f * (0.5f + 0.5f * Mathf.Sin(d.alpha));
                if (d.img != null)
                {
                    var c = d.img.color;
                    c.a = dropletColor.a * Mathf.Clamp01(a);
                    d.img.color = c;
                }

                // Recycle once dropped off the bottom
                if (pos.y < _bounds.yMin - 12f)
                {
                    Reset(i, randomY: false);
                }

                _drops[i] = d;
            }
        }

        private void Reset(int index, bool randomY)
        {
            var d = _drops[index];
            if (d.rt == null) return;

            float x = Random.Range(_bounds.xMin + 8f, _bounds.xMax - 8f);
            float y = randomY
                ? Random.Range(_bounds.yMin, _bounds.yMax)
                : _bounds.yMax + Random.Range(6f, 60f);

            d.rt.anchoredPosition = new Vector2(x, y);
            d.speed               = Random.Range(minFallSpeed, maxFallSpeed);
            d.alpha               = Random.value * Mathf.PI * 2f;
            _drops[index]         = d;
        }

        public void SetReferences(RectTransform parent, Sprite sprite, Color color)
        {
            container     = parent;
            dropletSprite = sprite;
            dropletColor  = color;
        }
    }
}
