using UnityEngine;
using UnityEngine.UI;

namespace SmartFarm.DayNight
{
    /// <summary>
    /// Marker dropped on any GameObject we want the <see cref="ScreenGlowController"/>
    /// to drive between Day and Night.
    ///
    /// Supports two output types automatically:
    ///   1. A world-space mesh screen — a <see cref="Renderer"/> on the same
    ///      GameObject (or assigned manually). The component drives
    ///      <c>_EmissionColor</c> via a MaterialPropertyBlock so we never
    ///      mutate shared materials.
    ///   2. A UI graphic — an <see cref="Graphic"/> (Image, RawImage, TMP_Text)
    ///      on the same GameObject. Day colour = base; night colour = HDR-boosted
    ///      neon variant for that "alive" tablet feel.
    ///
    /// Drop one on the Crop Growth Monitor screen, the Smart Irrigation Tablet
    /// border, the analytics canvases, the dashboard, etc. The setup tool will
    /// also auto-tag the obvious candidates for you.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/Smart Screen Glow Target")]
    [DisallowMultipleComponent]
    public class SmartScreenGlowTarget : MonoBehaviour
    {
        public enum DriveMode
        {
            Auto,
            Renderer,
            UIGraphic
        }

        [Header("Mode")]
        [Tooltip("Auto picks Renderer if one is attached, otherwise UI Graphic.")]
        [SerializeField] private DriveMode mode = DriveMode.Auto;

        [Header("Renderer Output")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private int materialIndex = 0;
        [Tooltip("Day-time emission. Often near black.")]
        [SerializeField, ColorUsage(false, true)] private Color dayEmission   = new Color(0.05f, 0.20f, 0.10f);
        [Tooltip("Night-time emission. Bright neon green by default.")]
        [SerializeField, ColorUsage(false, true)] private Color nightEmission = new Color(0.20f, 2.00f, 0.85f);

        [Header("UI Graphic Output")]
        [SerializeField] private Graphic targetGraphic;
        [Tooltip("Day-time colour (often the original UI tint).")]
        [SerializeField] private Color dayColor   = new Color(0.30f, 0.85f, 0.55f, 1f);
        [Tooltip("Night-time colour. Typically HDR-bright neon green for tablets / borders.")]
        [SerializeField, ColorUsage(true, true)] private Color nightColor = new Color(0.40f, 1.85f, 0.95f, 1f);

        [Header("Optional Boost")]
        [SerializeField, Range(0.5f, 4f)] private float nightBoost = 1.0f;

        private MaterialPropertyBlock _mpb;
        private bool _capturedDayColor;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public Renderer TargetRenderer => targetRenderer;
        public Graphic  TargetGraphic  => targetGraphic;

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
            targetGraphic  = GetComponent<Graphic>();
        }

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetGraphic  == null) targetGraphic  = GetComponent<Graphic>();

            // If the user didn't fine-tune dayColor for a UI graphic, capture
            // the live colour now so the day state stays exactly what the
            // designer painted in the inspector for that Image / Text.
            if (!_capturedDayColor && targetGraphic != null)
            {
                dayColor = targetGraphic.color;
                _capturedDayColor = true;
            }
        }

        /// <summary>Drive the visual to the given night weight (0 = day, 1 = night).</summary>
        public void ApplyWeight(float nightWeight)
        {
            float w = Mathf.Clamp01(nightWeight);
            float boostedW = Mathf.Clamp01(w * nightBoost);

            DriveMode resolved = mode;
            if (resolved == DriveMode.Auto)
                resolved = targetRenderer != null ? DriveMode.Renderer : DriveMode.UIGraphic;

            if (resolved == DriveMode.Renderer && targetRenderer != null)
            {
                _mpb ??= new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(_mpb, materialIndex);
                Color emission = Color.Lerp(dayEmission, nightEmission, boostedW);
                _mpb.SetColor(EmissionColorId, emission);
                targetRenderer.SetPropertyBlock(_mpb, materialIndex);
            }
            else if (resolved == DriveMode.UIGraphic && targetGraphic != null)
            {
                Color c = Color.Lerp(dayColor, nightColor, boostedW);
                targetGraphic.color = c;
            }
        }

        /// <summary>Editor / runtime helper to overwrite the Day baseline (call before lerping).</summary>
        public void CaptureCurrentAsDay()
        {
            if (targetGraphic != null) dayColor = targetGraphic.color;
            if (targetRenderer != null && targetRenderer.sharedMaterial != null
                && targetRenderer.sharedMaterial.HasProperty(EmissionColorId))
            {
                dayEmission = targetRenderer.sharedMaterial.GetColor(EmissionColorId);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            ApplyWeight(DayNightModeManager.Instance != null ? DayNightModeManager.Instance.NightWeight : 0f);
        }
#endif
    }
}
