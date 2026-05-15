using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartFarm.Irrigation.Sustainability.UI
{
    /// <summary>
    /// Drop-in feedback layer for a UI Button used on the Sustainability page in
    /// a VR/XR scene.
    ///
    /// • Press scale + colour pulse on pointer/poke down (works with the XR
    ///   Interaction Toolkit's <c>TrackedDeviceGraphicRaycaster</c> which fires
    ///   the same EventSystem pointer events as a 2D mouse).
    /// • Optional haptic pulse through the existing <see cref="VRHapticsHelper"/>.
    /// • Hover tint when the XR ray enters the button — clarifies the focus
    ///   target without any extra interactable component.
    ///
    /// Quest VR friendly: no allocations, only writes transform/color on actual
    /// state changes.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/UI/Sustainability Poke Button")]
    [RequireComponent(typeof(RectTransform))]
    public class SustainabilityPokeButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Target")]
        [SerializeField] private Button   targetButton;
        [SerializeField] private Image    backgroundImage;
        [SerializeField] private RectTransform pressTarget;

        [Header("Press feedback")]
        [SerializeField, Range(0.5f, 1f)] private float pressScale = 0.94f;
        [SerializeField, Range(1f, 30f)]  private float lerpSpeed  = 14f;

        [Header("Colour")]
        [SerializeField] private bool tintOnHover = true;
        [SerializeField] private Color hoverTint  = new Color(1.10f, 1.10f, 1.10f, 1f);

        [Header("Haptics (optional)")]
        [SerializeField] private VRHapticsHelper haptics;
        [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.35f;
        [SerializeField, Range(0.01f, 0.2f)] private float hapticDuration = 0.04f;

        private Vector3 _baseScale = Vector3.one;
        private Color   _baseColor = Color.white;
        private bool    _pressed;
        private bool    _hover;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Reset()
        {
            targetButton    = GetComponent<Button>();
            backgroundImage = GetComponent<Image>();
            pressTarget     = transform as RectTransform;
        }

        private void Awake()
        {
            if (targetButton    == null) targetButton    = GetComponent<Button>();
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (pressTarget     == null) pressTarget     = transform as RectTransform;
            if (haptics         == null) haptics         = FindFirstObjectByType<VRHapticsHelper>();

            if (pressTarget != null)        _baseScale = pressTarget.localScale;
            if (backgroundImage != null)    _baseColor = backgroundImage.color;
        }

        private void OnDisable()
        {
            _pressed = false;
            _hover   = false;
            if (pressTarget != null)     pressTarget.localScale = _baseScale;
            if (backgroundImage != null) backgroundImage.color  = _baseColor;
        }

        private void Update()
        {
            if (pressTarget == null) return;

            float targetK = _pressed ? pressScale : 1f;
            float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            pressTarget.localScale = Vector3.Lerp(pressTarget.localScale, _baseScale * targetK, t);

            if (tintOnHover && backgroundImage != null)
            {
                Color goal = _hover || _pressed
                    ? new Color(_baseColor.r * hoverTint.r, _baseColor.g * hoverTint.g, _baseColor.b * hoverTint.b, _baseColor.a)
                    : _baseColor;
                backgroundImage.color = Color.Lerp(backgroundImage.color, goal, t);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Pointer events (works for XR ray + poke through the EventSystem)
        // ─────────────────────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            if (targetButton != null && !targetButton.interactable) return;
            _pressed = true;
            if (haptics != null) haptics.PulseBoth(hapticAmplitude, hapticDuration);
        }

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData)  => _hover = false;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(Button btn, Image bg, RectTransform target, VRHapticsHelper hap)
        {
            targetButton    = btn;
            backgroundImage = bg;
            pressTarget     = target;
            haptics         = hap;
        }
    }
}
