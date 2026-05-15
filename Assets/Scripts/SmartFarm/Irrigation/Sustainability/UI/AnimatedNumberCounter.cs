using TMPro;
using UnityEngine;

namespace SmartFarm.Irrigation.Sustainability.UI
{
    /// <summary>
    /// Smoothly counts a TMP_Text value up/down toward a target number, with an
    /// optional prefix/suffix. Used by the "Water Saved Today" header.
    ///
    /// Quest VR friendly: pure scalar interpolation, only writes to the text
    /// when the displayed integer actually changes.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/UI/Animated Number Counter")]
    [DisallowMultipleComponent]
    public class AnimatedNumberCounter : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private string prefix = "";
        [SerializeField] private string suffix = "L";

        [Header("Animation")]
        [SerializeField, Range(0.5f, 12f)] private float lerpSpeed = 5f;
        [SerializeField] private bool useIntegerDisplay = true;
        [SerializeField] private int  decimalPlaces     = 0;

        private float  _target;
        private float  _displayed;
        private int    _lastWrittenInt = int.MinValue;
        private string _lastWrittenStr;

        public void SetTarget(float value)
        {
            _target = value;
        }

        public void SnapToTarget(float value)
        {
            _target    = value;
            _displayed = value;
            ForceWrite();
        }

        public void SetReferences(TMP_Text text, string newPrefix, string newSuffix)
        {
            label  = text;
            prefix = newPrefix;
            suffix = newSuffix;
        }

        private void Update()
        {
            if (label == null) return;
            float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            _displayed = Mathf.Lerp(_displayed, _target, t);

            if (useIntegerDisplay)
            {
                int v = Mathf.RoundToInt(_displayed);
                if (v != _lastWrittenInt)
                {
                    _lastWrittenInt = v;
                    label.text = $"{prefix}{v:N0}{suffix}";
                }
            }
            else
            {
                string s = _displayed.ToString($"F{Mathf.Clamp(decimalPlaces, 0, 4)}");
                string composed = $"{prefix}{s}{suffix}";
                if (composed != _lastWrittenStr)
                {
                    _lastWrittenStr = composed;
                    label.text = composed;
                }
            }
        }

        private void ForceWrite()
        {
            if (label == null) return;
            if (useIntegerDisplay)
            {
                int v = Mathf.RoundToInt(_displayed);
                _lastWrittenInt = v;
                label.text = $"{prefix}{v:N0}{suffix}";
            }
            else
            {
                string s = _displayed.ToString($"F{Mathf.Clamp(decimalPlaces, 0, 4)}");
                _lastWrittenStr = $"{prefix}{s}{suffix}";
                label.text = _lastWrittenStr;
            }
        }
    }
}
