using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.UI
{
    /// <summary>
    /// Animated circular gauge used by the Smart Irrigation Tablet.
    ///
    /// Uses a single Image with <c>Image.Type.Filled / Radial360</c> for the ring
    /// itself plus an optional center value text. Animates smoothly between
    /// values and can pulse when the underlying state is critical.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/UI/Circular Water Indicator")]
    [DisallowMultipleComponent]
    public class CircularWaterIndicator : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private Image     trackImage;
        [SerializeField] private Image     fillImage;
        [SerializeField] private TMP_Text  valueText;
        [SerializeField] private TMP_Text  labelText;

        [Header("Animation")]
        [SerializeField, Range(1f, 20f)] private float lerpSpeed   = 7f;
        [SerializeField, Range(0.5f, 6f)] private float pulseSpeed = 1.6f;

        [Header("Display")]
        [SerializeField] private string suffix = "%";
        [SerializeField] private bool   roundedValue = true;

        [Header("Critical State")]
        [SerializeField] private bool pulseOnCritical = true;

        private float  _targetValue01;
        private float  _displayedValue01;
        private float  _displayedValueRaw;
        private Color  _targetColor = Color.white;
        private bool   _isCritical;
        private bool   _isInitialized;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Sets the displayed percentage (0..100) and ring colour.</summary>
        public void SetValue(float percent, Color color, bool isCritical = false)
        {
            _targetValue01 = Mathf.Clamp01(percent / 100f);
            _targetColor   = color;
            _isCritical    = isCritical;

            if (!_isInitialized)
            {
                _displayedValue01    = _targetValue01;
                _displayedValueRaw   = percent;
                if (fillImage != null) fillImage.fillAmount = _displayedValue01;
                _isInitialized       = true;
            }
        }

        /// <summary>Convenience helper used by the tablet — same as SetValue().</summary>
        public void SetValue(float percent, Color color) => SetValue(percent, color, isCritical: false);

        /// <summary>Updates only the label text (e.g. "Soil Moisture").</summary>
        public void SetLabel(string label)
        {
            if (labelText != null) labelText.text = label;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Reset()
        {
            // Try to auto-find an existing Filled radial image as the fill
            var imgs = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < imgs.Length; i++)
            {
                if (imgs[i].type == Image.Type.Filled && imgs[i].fillMethod == Image.FillMethod.Radial360)
                {
                    fillImage = imgs[i];
                    break;
                }
            }
        }

        private void Update()
        {
            if (!_isInitialized) return;
            float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            _displayedValue01 = Mathf.Lerp(_displayedValue01, _targetValue01, t);

            if (fillImage != null)
            {
                fillImage.fillAmount = _displayedValue01;

                Color color = _targetColor;
                if (_isCritical && pulseOnCritical)
                {
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI);
                    color.a *= pulse;
                }
                fillImage.color = Color.Lerp(fillImage.color, color, t);
            }

            float rawTarget = _targetValue01 * 100f;
            _displayedValueRaw = Mathf.Lerp(_displayedValueRaw, rawTarget, t);
            if (valueText != null)
            {
                if (roundedValue)
                    valueText.text = $"{Mathf.RoundToInt(_displayedValueRaw)}{suffix}";
                else
                    valueText.text = $"{_displayedValueRaw:F1}{suffix}";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers (used by the editor setup script)
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(Image track, Image fill, TMP_Text value, TMP_Text label)
        {
            trackImage = track;
            fillImage  = fill;
            valueText  = value;
            labelText  = label;
        }
    }
}
