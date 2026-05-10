using System;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Reactive bridge between <see cref="WeatherManager"/> and
    /// <see cref="IrrigationZoneManager"/>.
    ///
    /// Behaviour matches the Smart Irrigation Tablet spec:
    ///   • Sunny  → boost irrigation (zones in Auto mode irrigate more eagerly).
    ///   • Rainy  → reduce irrigation (less demand, lower flow per tick).
    ///   • Storm  → stop irrigation entirely (safety override).
    ///
    /// Implementation strategy: rather than mutating zones every tick, this system
    /// adjusts global per-zone <c>waterPerTick</c> multipliers + can force-disable
    /// every zone. It exposes events so the tablet UI can react with banners.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Weather Integration System")]
    public class WeatherIntegrationSystem : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private WeatherManager weatherManager;
        [SerializeField] private IrrigationZoneManager zoneManager;

        [Header("Multipliers")]
        [Tooltip("Water flow multiplier under Sunny weather.")]
        [SerializeField, Range(0.1f, 3f)] private float sunnyMultiplier = 1.4f;

        [Tooltip("Water flow multiplier under Rainy weather.")]
        [SerializeField, Range(0.0f, 2f)] private float rainyMultiplier = 0.35f;

        [Tooltip("Water flow multiplier under Storm weather (irrigation forced off regardless).")]
        [SerializeField, Range(0.0f, 1f)] private float stormMultiplier = 0f;

        // ── Runtime ──────────────────────────────────────────────────────────

        public WeatherManager.WeatherType CurrentWeather { get; private set; } = WeatherManager.WeatherType.Sunny;
        public float CurrentMultiplier { get; private set; } = 1f;
        public string LastNotice { get; private set; } = "";

        public bool IsStormActive => CurrentWeather == WeatherManager.WeatherType.Storm;

        /// <summary>Fires whenever weather changes (after multipliers are applied).</summary>
        public event Action<WeatherManager.WeatherType, string> OnWeatherNotice;

        // We capture the original waterPerTick values when we first see a zone, so
        // multipliers can be re-applied cleanly without drift.
        private float[] _baselineFlow = System.Array.Empty<float>();
        private int     _baselineCount;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (weatherManager == null)
                weatherManager = FindFirstObjectByType<WeatherManager>();
            if (zoneManager == null)
                zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
        }

        private void OnEnable()
        {
            if (weatherManager == null)
                weatherManager = FindFirstObjectByType<WeatherManager>();
            if (zoneManager == null)
                zoneManager = FindFirstObjectByType<IrrigationZoneManager>();

            if (weatherManager != null)
            {
                weatherManager.OnWeatherChanged += HandleWeatherChanged;
                CurrentWeather = weatherManager.CurrentWeather;
            }

            CaptureBaseline();
            ApplyMultipliersFor(CurrentWeather, silent: true);
        }

        private void OnDisable()
        {
            if (weatherManager != null)
                weatherManager.OnWeatherChanged -= HandleWeatherChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Weather handling
        // ─────────────────────────────────────────────────────────────────────

        private void HandleWeatherChanged(WeatherManager.WeatherType weather)
        {
            CurrentWeather = weather;
            ApplyMultipliersFor(weather, silent: false);
        }

        private void ApplyMultipliersFor(WeatherManager.WeatherType weather, bool silent)
        {
            if (zoneManager == null) return;

            float multiplier = weather switch
            {
                WeatherManager.WeatherType.Sunny => sunnyMultiplier,
                WeatherManager.WeatherType.Rainy => rainyMultiplier,
                WeatherManager.WeatherType.Storm => stormMultiplier,
                _ => 1f
            };

            CurrentMultiplier = multiplier;

            // Apply to live zones using captured baselines
            var zones = zoneManager.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null) continue;
                float baseline = i < _baselineCount ? _baselineFlow[i] : z.waterPerTick;
                z.waterPerTick = Mathf.Max(0f, baseline * multiplier);
            }

            // Storm safety override: force disable every zone regardless of mode
            if (weather == WeatherManager.WeatherType.Storm)
                zoneManager.ForceDisableAllForReason("Storm Irrigation Disabled");

            LastNotice = weather switch
            {
                WeatherManager.WeatherType.Sunny => "Sunny — irrigation boosted",
                WeatherManager.WeatherType.Rainy => "Rainy — irrigation reduced",
                WeatherManager.WeatherType.Storm => "Storm Irrigation Disabled",
                _ => ""
            };

            if (!silent)
            {
                EventLogger.LogEvent(LastNotice);
                OnWeatherNotice?.Invoke(weather, LastNotice);
            }
        }

        private void CaptureBaseline()
        {
            if (zoneManager == null) return;
            var zones = zoneManager.Zones;
            if (_baselineFlow.Length < zones.Count)
                _baselineFlow = new float[zones.Count];
            _baselineCount = zones.Count;
            for (int i = 0; i < zones.Count; i++)
                _baselineFlow[i] = zones[i] != null ? zones[i].waterPerTick : 0f;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetWeatherManager(WeatherManager mgr) => weatherManager = mgr;
        public void SetZoneManager(IrrigationZoneManager mgr) => zoneManager = mgr;

        public void RefreshBaseline()
        {
            CaptureBaseline();
            ApplyMultipliersFor(CurrentWeather, silent: true);
        }
    }
}
