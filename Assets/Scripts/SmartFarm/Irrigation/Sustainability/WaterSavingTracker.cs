using System;
using UnityEngine;

namespace SmartFarm.Irrigation.Sustainability
{
    /// <summary>
    /// Tracks how much water the smart irrigation system has SAVED today vs a
    /// configurable "baseline" (dumb / always-on) irrigation strategy.
    ///
    /// We don't run a parallel simulation — instead the savings number is derived
    /// each tick from real signals we already collect:
    ///
    ///   • <b>Auto idle savings</b>    — zones in Auto mode that decided NOT to
    ///     irrigate this tick still would've been running under a dumb schedule.
    ///   • <b>Weather rain savings</b> — when weather is Rainy/Storm, every litre
    ///     a dumb system would've poured is counted as saved.
    ///   • <b>Efficiency savings</b>   — the fraction of every actually-applied
    ///     litre that is "smart-targeted" vs wasted runoff is also a saving.
    ///
    /// The result is exposed as a smoothed "Water Saved Today" counter that the
    /// Sustainability Monitor UI can animate.
    ///
    /// Quest VR friendly: pure scalar math on a slow timer, zero allocations.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/Water Saving Tracker")]
    public class WaterSavingTracker : MonoBehaviour
    {
        // ── Configuration ────────────────────────────────────────────────────

        [Header("References (auto-found if empty)")]
        [SerializeField] private IrrigationZoneManager       zoneManager;
        [SerializeField] private WeatherIntegrationSystem    weatherSystem;
        [SerializeField] private WaterAnalyticsSystem        analytics;

        [Header("Baseline (\"dumb\" schedule)")]
        [Tooltip("Water (units) per zone per second a non-smart system would always use.")]
        [SerializeField, Range(0.5f, 25f)] private float baselineWaterPerZonePerSec = 6f;

        [Tooltip("How often the savings counter ticks.")]
        [SerializeField, Range(0.25f, 4f)] private float sampleSeconds = 0.5f;

        [Header("Smoothing")]
        [Tooltip("How quickly the visible counter chases the true value (UI feels).")]
        [SerializeField, Range(0.5f, 20f)] private float displayLerpSpeed = 3.5f;

        // ── Runtime state ────────────────────────────────────────────────────

        private float _sampleTimer;
        private float _trueSavedLitres;
        private float _displayedSavedLitres;

        /// <summary>Total water saved today (smoothed, animated value the UI shows).</summary>
        public float WaterSavedTodayLitres => _displayedSavedLitres;

        /// <summary>"Real" cumulative savings — the value the displayed counter is chasing.</summary>
        public float TrueWaterSavedLitres => _trueSavedLitres;

        /// <summary>Litres-per-second a baseline schedule would burn right now.</summary>
        public float BaselineBurnRate => baselineWaterPerZonePerSec
            * (zoneManager != null ? Mathf.Max(0, zoneManager.Zones.Count) : 0);

        public event Action<float> OnWaterSavedChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (zoneManager   == null) zoneManager   = FindFirstObjectByType<IrrigationZoneManager>();
            if (weatherSystem == null) weatherSystem = FindFirstObjectByType<WeatherIntegrationSystem>();
            if (analytics     == null) analytics     = FindFirstObjectByType<WaterAnalyticsSystem>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _sampleTimer += dt;
            if (_sampleTimer >= sampleSeconds)
            {
                AccumulateSavings(_sampleTimer);
                _sampleTimer = 0f;
            }

            // Smooth the displayed value so the UI counter "ticks up" gracefully.
            float t = 1f - Mathf.Exp(-displayLerpSpeed * dt);
            float prev = _displayedSavedLitres;
            _displayedSavedLitres = Mathf.Lerp(_displayedSavedLitres, _trueSavedLitres, t);
            if (!Mathf.Approximately(prev, _displayedSavedLitres))
                OnWaterSavedChanged?.Invoke(_displayedSavedLitres);
        }

        private void AccumulateSavings(float deltaTime)
        {
            if (zoneManager == null) return;

            var zones = zoneManager.Zones;
            if (zones == null || zones.Count == 0) return;

            float weatherMultiplier = weatherSystem != null
                ? GetWeatherSavingMultiplier(weatherSystem.CurrentWeather)
                : 1f;

            // For every zone, compare what a baseline schedule WOULD have used to
            // what we actually used this tick.
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null) continue;

                // Baseline burn: a fixed amount per zone per second
                float wouldHaveUsed = baselineWaterPerZonePerSec * deltaTime;

                // What we *actually* used this tick (estimated): waterPerTick *
                // tick fraction if flowing, else zero.
                float actuallyUsed = z.isFlowing
                    ? z.waterPerTick * deltaTime * Mathf.Clamp01(z.flowRate)
                    : 0f;

                float saved = Mathf.Max(0f, wouldHaveUsed - actuallyUsed) * weatherMultiplier;
                _trueSavedLitres += saved;
            }
        }

        private static float GetWeatherSavingMultiplier(WeatherManager.WeatherType weather) => weather switch
        {
            WeatherManager.WeatherType.Rainy => 1.35f, // rain top-up — credit the saving
            WeatherManager.WeatherType.Storm => 1.65f, // storm pause — credit even more
            _ => 1.0f
        };

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void ResetCounter()
        {
            _trueSavedLitres = 0f;
            _displayedSavedLitres = 0f;
            OnWaterSavedChanged?.Invoke(0f);
        }

        public void SetReferences(IrrigationZoneManager zm, WeatherIntegrationSystem ws, WaterAnalyticsSystem an)
        {
            zoneManager   = zm;
            weatherSystem = ws;
            analytics     = an;
        }
    }
}
