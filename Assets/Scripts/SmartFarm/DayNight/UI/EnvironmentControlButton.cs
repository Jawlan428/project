using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.DayNight.UI
{
    /// <summary>
    /// Visual state controller for a Day or Night mode button. Highlights the
    /// active mode with a brighter background, accent border, and bolder label.
    /// Holds the <see cref="UnityEngine.UI.Button"/> reference so the panel can
    /// hook click events without writing boilerplate per button.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/UI/Environment Control Button")]
    [RequireComponent(typeof(RectTransform))]
    public class EnvironmentControlButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text label;

        [Header("Colors")]
        [SerializeField] private Color activeBackground   = new Color(0.10f, 0.42f, 0.30f, 1f);
        [SerializeField] private Color inactiveBackground = new Color(0.06f, 0.12f, 0.16f, 0.95f);
        [SerializeField] private Color activeBorder       = new Color(0.30f, 1.40f, 0.75f, 1f);
        [SerializeField] private Color inactiveBorder     = new Color(0.10f, 0.40f, 0.30f, 0.55f);
        [SerializeField] private Color activeText         = new Color(1.00f, 1.00f, 0.95f, 1f);
        [SerializeField] private Color inactiveText       = new Color(0.65f, 0.85f, 0.78f, 1f);

        [Header("Animation")]
        [SerializeField, Range(0.05f, 1.5f)] private float fadeDuration = 0.25f;

        public Button Button => button;

        private bool _isActive;
        private float _animElapsed;
        private bool _animating;
        private Color _bgFrom, _bgTo, _borderFrom, _borderTo, _textFrom, _textTo;

        private void Reset()
        {
            button     = GetComponent<Button>();
            background = GetComponent<Image>();
        }

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            ApplyInstantly(_isActive);
        }

        private void Update()
        {
            if (!_animating) return;
            _animElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_animElapsed / Mathf.Max(0.001f, fadeDuration));
            float k = Mathf.SmoothStep(0f, 1f, t);
            if (background != null) background.color = Color.Lerp(_bgFrom,     _bgTo,     k);
            if (border     != null) border.color     = Color.Lerp(_borderFrom, _borderTo, k);
            if (label      != null) label.color      = Color.Lerp(_textFrom,   _textTo,   k);
            if (t >= 1f) _animating = false;
        }

        public void SetActiveVisual(bool active, bool animate = true)
        {
            _isActive = active;
            if (label != null) label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;

            if (!animate || !isActiveAndEnabled || fadeDuration <= 0.01f)
            {
                ApplyInstantly(active);
                return;
            }

            _bgFrom     = background != null ? background.color : Color.clear;
            _borderFrom = border     != null ? border.color     : Color.clear;
            _textFrom   = label      != null ? label.color      : Color.clear;
            _bgTo       = active ? activeBackground : inactiveBackground;
            _borderTo   = active ? activeBorder     : inactiveBorder;
            _textTo     = active ? activeText       : inactiveText;
            _animElapsed = 0f;
            _animating  = true;
        }

        private void ApplyInstantly(bool active)
        {
            if (background != null) background.color = active ? activeBackground : inactiveBackground;
            if (border     != null) border.color     = active ? activeBorder     : inactiveBorder;
            if (label      != null)
            {
                label.color     = active ? activeText : inactiveText;
                label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
            _animating = false;
        }

        // Accessors used by the editor setup tool to wire references.
        public void SetReferences(Button btn, Image bg, Image bd, Image icon, TMP_Text lbl)
        {
            button = btn; background = bg; border = bd; iconImage = icon; label = lbl;
        }

        public Image IconImage => iconImage;
    }
}
