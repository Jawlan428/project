using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Smooth horizontal "flow bar" used to visualise irrigation activity.
    ///
    /// Two layered images:
    ///   • <see cref="fillImage"/> — represents the smoothed activity level (0..1).
    ///   • <see cref="shineImage"/> — a translucent sprite that slides left → right
    ///     to suggest moving water. Opacity scales with flow level.
    ///
    /// All animation runs in a single Update at low cost; no allocations.
    ///
    /// Self-heals on Awake by adding a <see cref="RectMask2D"/> to the bar so the
    /// shine highlight can never bleed out of the bar's bounds — this fixes the
    /// "green line extending past the tablet" issue that was visible at 100 %
    /// flow.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Animated Flow Bar")]
    [DisallowMultipleComponent]
    public class AnimatedFlowBar : MonoBehaviour
    {
        [Header("Bar Refs")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Image trackImage;
        [SerializeField] private RectTransform shineRect;
        [SerializeField] private Image shineImage;

        [Header("Animation")]
        [SerializeField, Range(1f, 12f)] private float fillLerpSpeed = 6f;
        [SerializeField, Range(0.1f, 4f)] private float shineSpeed = 1.3f;

        [Header("Colours")]
        [SerializeField] private Color activeColor = new Color(0.40f, 0.75f, 1.00f, 1f);
        [SerializeField] private Color idleColor   = new Color(0.18f, 0.32f, 0.45f, 1f);

        [Header("Clipping")]
        [Tooltip("Automatically add a RectMask2D to this GameObject on Awake so the " +
                 "shine animation stays inside the bar. Disable only if a parent already masks.")]
        [SerializeField] private bool autoAddMask = true;

        private float _targetFlow;
        private float _displayedFlow;
        private float _shineT;
        private float _shineWidth01 = 0.18f;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Sets the target flow level (0..1).</summary>
        public void SetFlow(float flow01)
        {
            _targetFlow = Mathf.Clamp01(flow01);
        }

        public void SetActiveColor(Color c) => activeColor = c;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (autoAddMask)
                EnsureMask();
        }

        private void EnsureMask()
        {
            // RectMask2D clips children to this RectTransform's bounds (cheaper
            // than Mask + no extra draw call). Skip if one already exists or
            // an ancestor mask is in play.
            if (GetComponent<RectMask2D>() != null) return;
            // The bar's own track image must be present for the mask to have a
            // sensible rect, but RectMask2D doesn't require an Image — it uses
            // the RectTransform directly, so we can add it unconditionally.
            gameObject.AddComponent<RectMask2D>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float t  = 1f - Mathf.Exp(-fillLerpSpeed * dt);
            _displayedFlow = Mathf.Lerp(_displayedFlow, _targetFlow, t);

            if (fillImage != null)
            {
                fillImage.fillAmount = _displayedFlow;
                fillImage.color = Color.Lerp(idleColor, activeColor, _displayedFlow);
            }

            if (shineRect != null && shineImage != null)
            {
                _shineT = Mathf.Repeat(_shineT + dt * shineSpeed, 1f);

                // Belt-and-braces: even with the RectMask2D in place, clamp the
                // shine anchors so they never push past the bar's right edge.
                // The shine slides left → right by moving anchorMin from 0 → 1
                // while keeping its width = _shineWidth01; once the right edge
                // would exit, anchorMax is held at 1.0 and the visible portion
                // gracefully thins out.
                float minX = _shineT;
                float maxX = Mathf.Min(1f, _shineT + _shineWidth01);
                if (minX > maxX) minX = maxX; // safety when wrapping

                shineRect.anchorMin = new Vector2(minX, shineRect.anchorMin.y);
                shineRect.anchorMax = new Vector2(maxX, shineRect.anchorMax.y);
                shineRect.offsetMin = shineRect.offsetMax = Vector2.zero;

                var c = activeColor;
                c.a   = 0.55f * _displayedFlow;
                shineImage.color = c;
                shineImage.enabled = _displayedFlow > 0.05f;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers (used by the editor setup script)
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(Image fill, Image track, RectTransform shine, Image shineImg)
        {
            fillImage  = fill;
            trackImage = track;
            shineRect  = shine;
            shineImage = shineImg;
        }
    }
}
