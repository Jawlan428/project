using System;
using UnityEngine;

namespace SmartFarm.Irrigation.Sustainability
{
    /// <summary>
    /// Reads the current weather and produces a human-readable "Smart Water
    /// Recommendation" string + a numeric optimisation factor that contributes
    /// to the Sustainability Score.
    ///
    /// This sits one level above <see cref="WeatherIntegrationSystem"/>:
    /// where that component physically mutates zone flow rates, this one
    /// summarises the outcome for the player and the Eco Alert pipeline.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/Weather Optimization System")]
    public class WeatherOptimizationSystem : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private WeatherIntegrationSystem weatherSystem;
        [SerializeField] private WeatherManager           weatherManager;
        [SerializeField] private IrrigationZoneManager    zoneManager;

        // ── Public state ─────────────────────────────────────────────────────

        public WeatherManager.WeatherType CurrentWeather { get; private set; } = WeatherManager.WeatherType.Sunny;

        /// <summary>Human-friendly tip shown in the Smart Recommendations panel.</summary>
        public string Recommendation { get; private set; } = "Smart irrigation active.";

        /// <summary>0..1 — how well the system is exploiting current weather.</summary>
        public float OptimizationScore01 { get; private set; } = 0.8f;

        /// <summary>True when irrigation should be forcibly paused due to weather.</summary>
        public bool IrrigationLockedByWeather =>
            CurrentWeather == WeatherManager.WeatherType.Storm;

        public event Action<string> OnRecommendationChanged;
        public event Action<WeatherManager.WeatherType, float> OnOptimizationChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (weatherSystem  == null) weatherSystem  = FindFirstObjectByType<WeatherIntegrationSystem>();
            if (weatherManager == null) weatherManager = FindFirstObjectByType<WeatherManager>();
            if (zoneManager    == null) zoneManager    = FindFirstObjectByType<IrrigationZoneManager>();
        }

        private void OnEnable()
        {
            if (weatherSystem  == null) weatherSystem  = FindFirstObjectByType<WeatherIntegrationSystem>();
            if (weatherManager == null) weatherManager = FindFirstObjectByType<WeatherManager>();

            if (weatherManager != null) weatherManager.OnWeatherChanged += HandleWeatherChanged;
            if (weatherSystem  != null) weatherSystem.OnWeatherNotice   += HandleWeatherNotice;

            HandleWeatherChanged(weatherManager != null
                ? weatherManager.CurrentWeather
                : (weatherSystem != null ? weatherSystem.CurrentWeather : WeatherManager.WeatherType.Sunny));
        }

        private void OnDisable()
        {
            if (weatherManager != null) weatherManager.OnWeatherChanged -= HandleWeatherChanged;
            if (weatherSystem  != null) weatherSystem.OnWeatherNotice   -= HandleWeatherNotice;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Weather reaction
        // ─────────────────────────────────────────────────────────────────────

        private void HandleWeatherNotice(WeatherManager.WeatherType w, string _) => HandleWeatherChanged(w);

        private void HandleWeatherChanged(WeatherManager.WeatherType weather)
        {
            CurrentWeather = weather;

            string text;
            float score;
            switch (weather)
            {
                case WeatherManager.WeatherType.Rainy:
                    text  = "Natural rainfall detected. Irrigation reduced — rainwater optimization enabled.";
                    score = 0.95f;
                    break;
                case WeatherManager.WeatherType.Storm:
                    text  = "Storm detected. Irrigation paused for safety. Water saved automatically.";
                    score = 1.00f;
                    break;
                case WeatherManager.WeatherType.Sunny:
                default:
                    text  = ResolveSunnyRecommendation();
                    score = ResolveSunnyScore();
                    break;
            }

            string prev = Recommendation;
            Recommendation       = text;
            OptimizationScore01  = Mathf.Clamp01(score);

            if (prev != Recommendation) OnRecommendationChanged?.Invoke(Recommendation);
            OnOptimizationChanged?.Invoke(weather, OptimizationScore01);
        }

        private string ResolveSunnyRecommendation()
        {
            if (zoneManager == null) return "Sunny weather — smart irrigation active.";

            float m = zoneManager.AverageMoisture;
            if (m < 35f) return "Sunny weather. Crops are thirsty — increase irrigation.";
            if (m > 88f) return "Soil saturated. Pause irrigation to prevent overwatering.";
            if (m > 60f) return "Soil moisture is healthy. Maintain current schedule.";
            return "Sunny weather. Smart irrigation balancing water use.";
        }

        private float ResolveSunnyScore()
        {
            if (zoneManager == null) return 0.75f;
            float m = zoneManager.AverageMoisture;
            if (m > 60f && m < 88f) return 0.85f; // healthy band
            if (m < 30f || m > 92f) return 0.40f; // out of band
            return 0.65f;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(WeatherIntegrationSystem ws, WeatherManager wm, IrrigationZoneManager zm)
        {
            weatherSystem  = ws;
            weatherManager = wm;
            zoneManager    = zm;
        }
    }
}
