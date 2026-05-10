using UnityEngine;

namespace SmartFarm.DayNight
{
    /// <summary>
    /// Controls the global scene look (sun, ambient, sky tint, fog) by lerping
    /// between a Day profile and a Night profile based on the manager's
    /// <c>NightWeight</c>. Cooperates with <see cref="WeatherManager"/>:
    ///   • Captures the current weather-driven values as the "Day baseline"
    ///     on Awake so we never fight the weather system at noon.
    ///   • When the weather changes, re-applies the night blend on top so
    ///     night-time stays night even if it starts raining.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/Environment Lighting Controller")]
    public class EnvironmentLightingController : MonoBehaviour
    {
        [Header("Manager")]
        [SerializeField] private DayNightModeManager manager;

        [Header("Sun")]
        [Tooltip("Main directional light used as the sun. Auto-found if empty.")]
        [SerializeField] private Light sunLight;
        [Tooltip("If true, drives directional light intensity / color / rotation.")]
        [SerializeField] private bool driveDirectionalLight = true;
        [SerializeField, Range(0f, 3f)] private float dayLightIntensity   = 1.4f;
        [SerializeField, Range(0f, 3f)] private float nightLightIntensity = 0.08f;
        [SerializeField] private Color dayLightColor   = new Color(1.00f, 0.96f, 0.88f);
        [SerializeField] private Color nightLightColor = new Color(0.55f, 0.70f, 1.00f);
        [SerializeField] private Vector3 dayLightEuler   = new Vector3(50f, 30f, 0f);
        [SerializeField] private Vector3 nightLightEuler = new Vector3(-30f, 200f, 0f);

        [Header("Ambient")]
        [SerializeField] private bool driveAmbient = true;
        [SerializeField] private Color daySkyColor      = new Color(0.55f, 0.70f, 0.95f);
        [SerializeField] private Color dayEquatorColor  = new Color(0.45f, 0.55f, 0.75f);
        [SerializeField] private Color dayGroundColor   = new Color(0.20f, 0.20f, 0.18f);
        [SerializeField] private Color nightSkyColor    = new Color(0.04f, 0.06f, 0.14f);
        [SerializeField] private Color nightEquatorColor= new Color(0.05f, 0.07f, 0.18f);
        [SerializeField] private Color nightGroundColor = new Color(0.02f, 0.03f, 0.06f);

        [Header("Fog")]
        [SerializeField] private bool driveFog = true;
        [SerializeField, Range(0f, 0.1f)] private float dayFogDensity   = 0.0007f;
        [SerializeField, Range(0f, 0.1f)] private float nightFogDensity = 0.012f;
        [SerializeField] private Color dayFogColor   = new Color(0.86f, 0.92f, 1.00f);
        [SerializeField] private Color nightFogColor = new Color(0.06f, 0.08f, 0.14f);

        [Header("Skybox Tint")]
        [Tooltip("If true, lerps RenderSettings.ambientIntensity AND skybox _Tint/_Exposure for a darker night sky.")]
        [SerializeField] private bool driveSkybox = true;
        [SerializeField, Range(0f, 2f)] private float dayAmbientIntensity   = 1.0f;
        [SerializeField, Range(0f, 2f)] private float nightAmbientIntensity = 0.18f;
        [SerializeField] private Color dayCameraBackground   = new Color(0.55f, 0.75f, 0.95f);
        [SerializeField] private Color nightCameraBackground = new Color(0.02f, 0.03f, 0.07f);

        [Header("Day Side Trim")]
        [Tooltip("Single knob that scales the DAY-side brightness only (sun intensity, ambient intensity and ambient colours). " +
                 "Night-side stays untouched. 1 = original, 0.85 = slightly dimmer day, 0.7 = noticeably overcast feel.")]
        [SerializeField, Range(0.3f, 1.2f)] private float dayBrightnessMultiplier = 0.85f;

        [Header("Weather Cooperation")]
        [Tooltip("If a WeatherManager is in the scene, capture its day-time values as the Day baseline on Start.")]
        [SerializeField] private bool captureWeatherBaselineAsDay = true;

        // ── Captured baseline (so we restore exactly what was there at Day) ──
        private bool _baselineCaptured;
        private float _baseDayLightIntensity;
        private Color _baseDayLightColor;
        private Quaternion _baseDayLightRotation;
        private float _baseDayFogDensity;
        private Color _baseDayFogColor;
        private float _baseDayAmbientIntensity;
        private Color _baseDaySkyColor;
        private Color _baseDayEquatorColor;
        private Color _baseDayGroundColor;

        private void Awake()
        {
            if (manager == null) manager = DayNightModeManager.Instance ?? FindFirstObjectByType<DayNightModeManager>();
            if (sunLight == null) sunLight = ResolveSun();
        }

        private void OnEnable()
        {
            if (manager != null)
                manager.OnNightWeightChanged += HandleWeight;
            CaptureBaseline();
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.OnNightWeightChanged -= HandleWeight;
        }

        private static Light ResolveSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) return lights[i];
            return null;
        }

        private void CaptureBaseline()
        {
            if (_baselineCaptured || !captureWeatherBaselineAsDay) return;

            if (sunLight != null)
            {
                _baseDayLightIntensity = sunLight.intensity;
                _baseDayLightColor     = sunLight.color;
                _baseDayLightRotation  = sunLight.transform.rotation;
            }
            _baseDayFogDensity        = RenderSettings.fogDensity;
            _baseDayFogColor          = RenderSettings.fogColor;
            _baseDayAmbientIntensity  = RenderSettings.ambientIntensity;
            _baseDaySkyColor          = RenderSettings.ambientSkyColor;
            _baseDayEquatorColor      = RenderSettings.ambientEquatorColor;
            _baseDayGroundColor       = RenderSettings.ambientGroundColor;

            // Mix captured baseline 50% with inspector defaults so the user keeps
            // their tuning power but the scene starts from a sensible spot.
            dayLightIntensity = Mathf.Lerp(dayLightIntensity, _baseDayLightIntensity, 0.5f);
            dayLightColor     = Color.Lerp(dayLightColor,     _baseDayLightColor,    0.5f);
            dayFogDensity     = Mathf.Lerp(dayFogDensity,     _baseDayFogDensity,    0.5f);
            dayFogColor       = Color.Lerp(dayFogColor,       _baseDayFogColor,      0.5f);
            daySkyColor       = Color.Lerp(daySkyColor,       _baseDaySkyColor,      0.5f);
            dayEquatorColor   = Color.Lerp(dayEquatorColor,   _baseDayEquatorColor,  0.5f);
            dayGroundColor    = Color.Lerp(dayGroundColor,    _baseDayGroundColor,   0.5f);
            dayAmbientIntensity = Mathf.Lerp(dayAmbientIntensity, _baseDayAmbientIntensity, 0.5f);

            _baselineCaptured = true;
        }

        private void HandleWeight(float nightWeight)
        {
            float w = Mathf.Clamp01(nightWeight);

            // dayMul = dayBrightnessMultiplier at full day, 1 at full night.
            // Used to scale ONLY the day side of brightness so night stays untouched.
            float dayMul = Mathf.Lerp(dayBrightnessMultiplier, 1f, w);

            if (driveDirectionalLight && sunLight != null)
            {
                sunLight.intensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, w) * dayMul;
                sunLight.color     = Color.Lerp(dayLightColor,     nightLightColor,     w);
                sunLight.transform.rotation = Quaternion.Slerp(
                    Quaternion.Euler(dayLightEuler),
                    Quaternion.Euler(nightLightEuler),
                    w);
                if (RenderSettings.sun == null) RenderSettings.sun = sunLight;
            }

            if (driveAmbient)
            {
                RenderSettings.ambientSkyColor     = Color.Lerp(daySkyColor,     nightSkyColor,     w) * dayMul;
                RenderSettings.ambientEquatorColor = Color.Lerp(dayEquatorColor, nightEquatorColor, w) * dayMul;
                RenderSettings.ambientGroundColor  = Color.Lerp(dayGroundColor,  nightGroundColor,  w) * dayMul;
            }

            if (driveFog)
            {
                RenderSettings.fog        = true;
                RenderSettings.fogDensity = Mathf.Lerp(dayFogDensity, nightFogDensity, w);
                RenderSettings.fogColor   = Color.Lerp(dayFogColor,   nightFogColor,   w);
            }

            if (driveSkybox)
            {
                RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbientIntensity, nightAmbientIntensity, w) * dayMul;

                Color bg = Color.Lerp(dayCameraBackground, nightCameraBackground, w) * dayMul;
                bg.a = 1f;
                var cams = Camera.allCameras;
                if (cams != null)
                {
                    for (int i = 0; i < cams.Length; i++)
                    {
                        var cam = cams[i];
                        if (cam == null) continue;
                        if (cam.clearFlags == CameraClearFlags.SolidColor)
                            cam.backgroundColor = bg;
                    }
                }
            }
        }

        /// <summary>
        /// Runtime / tools API for the Day-side brightness trim. 1 = original,
        /// values below 1 dim the day side without touching night.
        /// </summary>
        public void SetDayBrightnessMultiplier(float value)
        {
            dayBrightnessMultiplier = Mathf.Clamp(value, 0.3f, 1.2f);
            if (DayNightModeManager.Instance != null)
                DayNightModeManager.Instance.ReapplyCurrentWeight();
            else
                HandleWeight(0f);
        }

        /// <summary>Editor / runtime tools can call this to re-capture a fresh Day baseline.</summary>
        public void RecaptureBaselineNow()
        {
            _baselineCaptured = false;
            CaptureBaseline();
        }
    }
}
