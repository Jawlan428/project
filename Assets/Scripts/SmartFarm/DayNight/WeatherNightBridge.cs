using UnityEngine;

namespace SmartFarm.DayNight
{
    /// <summary>
    /// Glue between the existing <see cref="WeatherManager"/> and the day/night
    /// system.
    ///
    /// Behaviours:
    ///   • When weather changes to <c>Storm</c> AND it is currently night, ask
    ///     <see cref="StreetLampManager"/> to switch lamps into flicker mode.
    ///   • Otherwise switch lamps back to steady glow.
    ///   • When the weather changes mid-transition, the lighting controller
    ///     re-applies its night blend so the day/night look "wins" on top.
    /// </summary>
    [AddComponentMenu("SmartFarm/Day Night/Weather Night Bridge")]
    public class WeatherNightBridge : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private DayNightModeManager manager;
        [SerializeField] private WeatherManager weatherManager;
        [SerializeField] private StreetLampManager lampManager;
        [SerializeField] private EnvironmentLightingController lightingController;

        [Header("Behaviour")]
        [Tooltip("If true, lamps flicker during a Storm at night. Day storms keep lamps off.")]
        [SerializeField] private bool flickerOnNightStorm = true;

        [Tooltip("Re-apply the lighting blend after a weather change so weather + day/night never desync.")]
        [SerializeField] private bool reapplyLightingOnWeatherChange = true;

        private void Awake()
        {
            if (manager == null)            manager            = DayNightModeManager.Instance ?? FindFirstObjectByType<DayNightModeManager>();
            if (weatherManager == null)     weatherManager     = FindFirstObjectByType<WeatherManager>();
            if (lampManager == null)        lampManager        = FindFirstObjectByType<StreetLampManager>();
            if (lightingController == null) lightingController = FindFirstObjectByType<EnvironmentLightingController>();
        }

        private void OnEnable()
        {
            if (weatherManager != null)
                weatherManager.OnWeatherChanged += HandleWeatherChanged;
            if (manager != null)
                manager.OnTransitionComplete += HandleTransitionComplete;
        }

        private void OnDisable()
        {
            if (weatherManager != null)
                weatherManager.OnWeatherChanged -= HandleWeatherChanged;
            if (manager != null)
                manager.OnTransitionComplete -= HandleTransitionComplete;
        }

        private void HandleWeatherChanged(WeatherManager.WeatherType type)
        {
            UpdateFlicker(type);

            if (reapplyLightingOnWeatherChange && manager != null)
            {
                // Push the current blend back out so the lighting controller
                // (and every other subscriber) re-applies on top of the new
                // weather state. Doesn't interrupt an in-progress transition.
                manager.ReapplyCurrentWeight();
            }
        }

        private void HandleTransitionComplete(DayNightMode mode)
        {
            if (weatherManager != null)
                UpdateFlicker(weatherManager.CurrentWeather);
        }

        private void UpdateFlicker(WeatherManager.WeatherType type)
        {
            if (lampManager == null) return;
            bool nightStorm = flickerOnNightStorm
                              && type == WeatherManager.WeatherType.Storm
                              && manager != null
                              && manager.CurrentMode == DayNightMode.Night;
            lampManager.SetStormFlicker(nightStorm);
        }
    }
}
