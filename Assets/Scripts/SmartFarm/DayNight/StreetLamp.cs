using UnityEngine;

namespace SmartFarm.DayNight
{
    /// <summary>
    /// Per-lamp behaviour: owns a <see cref="Light"/> (point/spot), an optional
    /// emissive bulb renderer, and a small storm-flicker mode. Created by the
    /// editor setup tool on every <c>SpaceZeta_StreetLamps</c> prefab instance
    /// in the scene, but you can also drop it manually on any lamp that has a
    /// child Light.
    ///
    /// The lamp does NOT subscribe to the manager directly — <see cref="StreetLampManager"/>
    /// orchestrates fades for the whole rig so all lamps stay in lockstep.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/Street Lamp")]
    [DisallowMultipleComponent]
    public class StreetLamp : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Point or Spot light driven by the manager. If empty, the first child Light is used.")]
        [SerializeField] private Light lampLight;

        [Tooltip("Optional emissive renderer (the lamp bulb / glass) lit at night.")]
        [SerializeField] private Renderer bulbRenderer;
        [Tooltip("Material slot index inside bulbRenderer to drive (0 = first material).")]
        [SerializeField] private int bulbMaterialIndex = 0;

        [Header("On / Off Settings")]
        [SerializeField, Range(0f, 12f)] private float onIntensity  = 2.5f;
        [SerializeField, Range(0f, 12f)] private float offIntensity = 0f;
        [SerializeField] private Color warmColor = new Color(1.00f, 0.78f, 0.42f);
        [SerializeField, ColorUsage(false, true)]
        private Color bulbEmissionOn = new Color(2.0f, 1.20f, 0.55f);

        [Header("Storm Flicker (driven by WeatherNightBridge)")]
        [SerializeField, Range(0f, 1f)] private float flickerStrength = 0.45f;
        [SerializeField, Range(0.5f, 30f)] private float flickerSpeed = 11f;

        private MaterialPropertyBlock _mpb;
        private float _baseWeight;          // 0..1, where 1 means fully on
        private float _flickerWeight;       // 0..1, only > 0 when storm flicker is enabled
        private bool _stormMode;
        private float _flickerSeed;
        private bool _initialised;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            EnsureRefs();
            _flickerSeed = Random.value * 1000f;
            ApplyToHardware();
            _initialised = true;
        }

        private void OnEnable()
        {
            if (_initialised) ApplyToHardware();
        }

        private void Update()
        {
            if (!_stormMode || flickerStrength <= 0.001f) return;
            // Slight per-lamp offset so neighbours don't flicker in unison.
            float n = Mathf.PerlinNoise((Time.time + _flickerSeed) * flickerSpeed * 0.13f, _flickerSeed * 0.07f);
            // n in [0..1]; map to [-1..1] then scale.
            _flickerWeight = (n - 0.5f) * 2f;
            ApplyToHardware();
        }

        // ── Public control surface (called by StreetLampManager) ─────────────

        /// <summary>Set the steady-state on/off blend (1 = lamp on, 0 = lamp off). Manager lerps this.</summary>
        public void SetWeight(float onWeight)
        {
            _baseWeight = Mathf.Clamp01(onWeight);
            ApplyToHardware();
        }

        /// <summary>Toggle the storm flicker mode. When false, the flicker term decays back to 0.</summary>
        public void SetStormFlicker(bool enabled)
        {
            _stormMode = enabled;
            if (!enabled)
            {
                _flickerWeight = 0f;
                ApplyToHardware();
            }
        }

        /// <summary>Tools / editor can swap colour at runtime.</summary>
        public void SetWarmColor(Color color)
        {
            warmColor = color;
            if (lampLight != null) lampLight.color = color;
            ApplyToHardware();
        }

        public Light Light => lampLight;
        public Renderer BulbRenderer => bulbRenderer;

        // ── Internals ────────────────────────────────────────────────────────

        private void EnsureRefs()
        {
            if (lampLight == null)
                lampLight = GetComponentInChildren<Light>(true);

            if (lampLight != null)
            {
                lampLight.color = warmColor;
                lampLight.shadows = lampLight.shadows == LightShadows.None ? LightShadows.Soft : lampLight.shadows;
                lampLight.renderMode = LightRenderMode.Auto;
            }
        }

        private void ApplyToHardware()
        {
            float onWeight = Mathf.Clamp01(_baseWeight + _flickerWeight * flickerStrength * _baseWeight);

            if (lampLight != null)
            {
                bool shouldEnable = onWeight > 0.01f;
                if (lampLight.enabled != shouldEnable) lampLight.enabled = shouldEnable;
                lampLight.intensity = Mathf.Lerp(offIntensity, onIntensity, onWeight);
                lampLight.color     = warmColor;
            }

            if (bulbRenderer != null)
            {
                _mpb ??= new MaterialPropertyBlock();
                bulbRenderer.GetPropertyBlock(_mpb, bulbMaterialIndex);
                Color emission = Color.Lerp(Color.black, bulbEmissionOn, onWeight);
                _mpb.SetColor(EmissionColorId, emission);
                bulbRenderer.SetPropertyBlock(_mpb, bulbMaterialIndex);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            ApplyToHardware();
        }
#endif
    }
}
