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
                // Shine slides across the full width, anchored to parent
                var min = new Vector2(_shineT, shineRect.anchorMin.y);
                var max = new Vector2(_shineT + _shineWidth01, shineRect.anchorMax.y);
                shineRect.anchorMin = min;
                shineRect.anchorMax = max;
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
