using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlantGrowth;

namespace SmartFarm
{
    /// <summary>
    /// Manages 3 weather conditions: Sunny, Rainy, Storm.
    /// Controls visuals (light, skybox, fog, particles, audio) + farm simulation (tick-based).
    /// No farm logic in UI - UI only calls SetWeather().
    /// Quest-friendly: tick-based, no per-frame allocations.
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public enum WeatherType
        {
            Sunny,
            Rainy,
            Storm
        }

        [Header("References")]
        [SerializeField] private FarmSimulationManager simulationManager;
        [SerializeField] private PlantGrowthManager plantGrowthManager;

        [Header("Visuals - Light")]
        [SerializeField] private Light directionalLight;
        [SerializeField] private float sunnyLightIntensity = 1.7f;
        [SerializeField] private Color sunnyLightColor = new Color(1f, 0.95f, 0.88f);  // Bright warm sun
        [SerializeField] private float rainyLightIntensity = 0.32f;
        [SerializeField] private Color rainyLightColor = new Color(0.65f, 0.7f, 0.82f);  // Dim gray overcast
        [SerializeField] private float stormLightIntensity = 0.15f;
        [SerializeField] private Color stormLightColor = new Color(0.5f, 0.52f, 0.6f);  // Dark storm

        [Header("Visuals - Skybox")]
        [SerializeField] [Tooltip("Sunny keeps your original scene sky. Rainy/Storm use weather skyboxes.")]
        private bool sunnyUsesOriginalSky = true;
        [SerializeField] private Material sunnySkybox;
        [SerializeField] private Material rainySkybox;
        [SerializeField] private Material stormSkybox;
        [SerializeField] [Tooltip("Overlay that darkens your sky for Rainy/Storm (required when Keep Original Sky)")]
        private SkyOverlay skyOverlay;

        [Header("Visuals - Ambient (scene lighting)")]
        [SerializeField] private bool updateAmbientLight = true;
        [SerializeField] private Color sunnyAmbientSky = new Color(0.5f, 0.62f, 0.88f);  // Bright blue
        [SerializeField] private Color rainyAmbientSky = new Color(0.38f, 0.4f, 0.48f);  // Dull gray
        [SerializeField] private Color stormAmbientSky = new Color(0.18f, 0.2f, 0.26f);

        [Header("Visuals - Fog")]
        [SerializeField] private float sunnyFogDensity = 0.0005f;
        [SerializeField] private Color sunnyFogColor = new Color(0.88f, 0.92f, 0.98f);  // Almost clear
        [SerializeField] private float rainyFogDensity = 0.018f;
        [SerializeField] private Color rainyFogColor = new Color(0.48f, 0.52f, 0.6f);  // Heavy gray mist
        [SerializeField] private float stormFogDensity = 0.03f;
        [SerializeField] private Color stormFogColor = new Color(0.28f, 0.3f, 0.36f);  // Heavy gloom

        [Header("Visuals - Clouds")]
        [SerializeField] private CloudLayer cloudLayer;

        [Header("Visuals - Rain Particles")]
        [SerializeField] private ParticleSystem rainParticleSystem;
        [SerializeField] private float rainyEmissionRate = 1800f;
        [SerializeField] private float stormEmissionRate = 3500f;

        [Header("Visuals - Lightning (optional)")]
        [SerializeField] private LightningEffect lightningEffect;

        [Header("Audio")]
        [SerializeField] private AudioSource sunnyAmbientSource;
        [SerializeField] private AudioSource rainyAmbientSource;
        [SerializeField] private AudioSource stormAmbientSource;

        [Header("Tick Settings")]
        [SerializeField] [Tooltip("Interval in seconds between weather effect ticks")]
        private float tickInterval = 0.5f;

        [Header("Sunny - Simulation")]
        [SerializeField] [Range(0, 50)] private float sunnyTemperature = 30f;
        [SerializeField] [Range(0, 100)] private float sunnySunlight = 90f;

        [Header("Rainy - Simulation")]
        [SerializeField] [Range(0, 50)] private float rainyTemperature = 20f;
        [SerializeField] [Range(0, 100)] private float rainySunlight = 55f;
        [SerializeField] [Tooltip("Water added per plant per tick")] private float rainyWaterPerTick = 2f;
        [SerializeField] [Tooltip("Health restored per plant per tick")] private float rainyHealthPerTick = 0.5f;

        [Header("Storm - Simulation")]
        [SerializeField] [Range(0, 50)] private float stormTemperature = 16f;
        [SerializeField] [Range(0, 100)] private float stormSunlight = 35f;
        [SerializeField] [Tooltip("Water added per plant per tick")] private float stormWaterPerTick = 6f;
        [SerializeField] [Tooltip("Health damage per plant per tick")] private float stormHealthDamagePerTick = 1.5f;
        [SerializeField] [Range(0f, 1f)] [Tooltip("Chance per plant per tick for extra damage")] private float stormRandomDamageChance = 0.15f;
        [SerializeField] [Tooltip("Extra damage when random hit")] private float stormRandomDamageAmount = 5f;

        public WeatherType CurrentWeather { get; private set; } = WeatherType.Sunny;
        public event System.Action<WeatherType> OnWeatherChanged;

        private Coroutine _tickCoroutine;
        private Material _defaultSkybox;

        private bool ShouldRunSimulation
        {
            get
            {
                if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
                    return true;
                return Unity.Netcode.NetworkManager.Singleton.IsServer;
            }
        }

        private void Awake()
        {
            if (simulationManager == null)
                simulationManager = FindFirstObjectByType<FarmSimulationManager>();
            if (plantGrowthManager == null)
                plantGrowthManager = FindFirstObjectByType<PlantGrowthManager>();
            if (directionalLight == null)
                directionalLight = FindFirstObjectByType<Light>();
            _defaultSkybox = RenderSettings.skybox;
        }

        private void Start()
        {
            ApplyWeatherVisualsAndSimulation(CurrentWeather);
            if (ShouldRunSimulation)
                _tickCoroutine = StartCoroutine(WeatherTickLoop());
            if (lightningEffect != null)
                lightningEffect.Disable();
        }

        private void OnDisable()
        {
            if (_tickCoroutine != null)
            {
                StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }
        }

        /// <summary>
        /// Set weather. Call from UI only. Applies visuals + simulation + logs event.
        /// </summary>
        public void SetWeather(WeatherType type)
        {
            if (CurrentWeather == type) return;

            CurrentWeather = type;
            ApplyWeatherVisuals(type);
            ApplyWeatherSimulation(type);
            OnWeatherChanged?.Invoke(type);
            EventLogger.LogEvent($"Weather changed to {type}");

            if (type == WeatherType.Storm && lightningEffect != null)
                lightningEffect.EnableForStorm();
            else if (lightningEffect != null)
                lightningEffect.Disable();
        }

        private void ApplyWeatherVisuals(WeatherType type)
        {
            // Directional Light
            if (directionalLight != null)
            {
                switch (type)
                {
                    case WeatherType.Sunny:
                        directionalLight.intensity = sunnyLightIntensity;
                        directionalLight.color = sunnyLightColor;
                        break;
                    case WeatherType.Rainy:
                        directionalLight.intensity = rainyLightIntensity;
                        directionalLight.color = rainyLightColor;
                        break;
                    case WeatherType.Storm:
                        directionalLight.intensity = stormLightIntensity;
                        directionalLight.color = stormLightColor;
                        break;
                }
            }

            // Skybox: Sunny keeps original sky, Rainy/Storm use weather skyboxes.
            if (type == WeatherType.Sunny && sunnyUsesOriginalSky)
            {
                RenderSettings.skybox = _defaultSkybox;
            }
            else
            {
                Material sky = type switch
                {
                    WeatherType.Sunny => sunnySkybox ?? _defaultSkybox,
                    WeatherType.Rainy => rainySkybox ?? _defaultSkybox,
                    WeatherType.Storm => stormSkybox ?? _defaultSkybox,
                    _ => _defaultSkybox
                };
                if (sky != null)
                    RenderSettings.skybox = sky;
            }

            // Disable full-screen overlay by default in this mode to keep UI clear.
            if (skyOverlay != null)
                skyOverlay.SetWeather(WeatherType.Sunny);

            // Cloud layer
            if (cloudLayer != null)
                cloudLayer.SetWeather(type);

            // Ambient light (sky/equator/ground) for more realistic scene lighting
            if (updateAmbientLight)
            {
                Color ambient = type switch
                {
                    WeatherType.Sunny => sunnyAmbientSky,
                    WeatherType.Rainy => rainyAmbientSky,
                    WeatherType.Storm => stormAmbientSky,
                    _ => sunnyAmbientSky
                };
                RenderSettings.ambientSkyColor = ambient;
                RenderSettings.ambientEquatorColor = Color.Lerp(ambient, Color.gray, 0.3f);
                RenderSettings.ambientGroundColor = Color.Lerp(ambient, Color.black, 0.5f);
            }

            // Fog
            RenderSettings.fog = true;
            switch (type)
            {
                case WeatherType.Sunny:
                    RenderSettings.fogDensity = sunnyFogDensity;
                    RenderSettings.fogColor = sunnyFogColor;
                    break;
                case WeatherType.Rainy:
                    RenderSettings.fogDensity = rainyFogDensity;
                    RenderSettings.fogColor = rainyFogColor;
                    break;
                case WeatherType.Storm:
                    RenderSettings.fogDensity = stormFogDensity;
                    RenderSettings.fogColor = stormFogColor;
                    break;
            }

            // Rain particles
            if (rainParticleSystem != null)
            {
                var emission = rainParticleSystem.emission;
                bool enable = type == WeatherType.Rainy || type == WeatherType.Storm;
                rainParticleSystem.gameObject.SetActive(enable);
                if (enable)
                {
                    emission.rateOverTime = type == WeatherType.Storm ? stormEmissionRate : rainyEmissionRate;
                }
            }

            // Audio
            SetAmbientAudio(type);
        }

        private void SetAmbientAudio(WeatherType type)
        {
            if (sunnyAmbientSource != null)
                sunnyAmbientSource.enabled = type == WeatherType.Sunny;
            if (rainyAmbientSource != null)
            {
                rainyAmbientSource.enabled = type == WeatherType.Rainy;
                if (type == WeatherType.Rainy && !rainyAmbientSource.isPlaying)
                    rainyAmbientSource.Play();
            }
            if (stormAmbientSource != null)
            {
                stormAmbientSource.enabled = type == WeatherType.Storm;
                if (type == WeatherType.Storm && !stormAmbientSource.isPlaying)
                    stormAmbientSource.Play();
            }
        }

        private void ApplyWeatherSimulation(WeatherType type)
        {
            if (!ShouldRunSimulation) return;

            float temp = type switch
            {
                WeatherType.Sunny => sunnyTemperature,
                WeatherType.Rainy => rainyTemperature,
                WeatherType.Storm => stormTemperature,
                _ => sunnyTemperature
            };

            float sunlight = type switch
            {
                WeatherType.Sunny => sunnySunlight,
                WeatherType.Rainy => rainySunlight,
                WeatherType.Storm => stormSunlight,
                _ => sunnySunlight
            };

            if (simulationManager != null)
                simulationManager.SetGlobalTemperature(temp);

            if (plantGrowthManager != null)
                plantGrowthManager.SetGlobalSunlight(sunlight);
        }

        private void ApplyWeatherVisualsAndSimulation(WeatherType type)
        {
            ApplyWeatherVisuals(type);
            ApplyWeatherSimulation(type);
        }

        private IEnumerator WeatherTickLoop()
        {
            var wait = new WaitForSeconds(tickInterval);
            while (true)
            {
                yield return wait;
                if (!ShouldRunSimulation) continue;
                ApplyWeatherTick(CurrentWeather, tickInterval);
            }
        }

        private void ApplyWeatherTick(WeatherType type, float deltaTime)
        {
            var plants = GetPlants();
            if (plants.Count == 0) return;

            switch (type)
            {
                case WeatherType.Sunny:
                    // Temperature and sunlight set immediately; moisture decreases naturally via PlantController decay
                    break;

                case WeatherType.Rainy:
                    for (int i = 0; i < plants.Count; i++)
                    {
                        var p = plants[i];
                        if (p == null || p.IsDead) continue;
                        p.Water(rainyWaterPerTick * deltaTime);
                        p.ModifyHealth(rainyHealthPerTick * deltaTime);
                    }
                    break;

                case WeatherType.Storm:
                    for (int i = 0; i < plants.Count; i++)
                    {
                        var p = plants[i];
                        if (p == null || p.IsDead) continue;
                        p.Water(stormWaterPerTick * deltaTime);
                        p.ModifyHealth(-stormHealthDamagePerTick * deltaTime);

                        if (stormRandomDamageChance > 0 && Random.value < stormRandomDamageChance)
                            p.ModifyHealth(-stormRandomDamageAmount);
                    }
                    break;
            }
        }

        private List<PlantController> GetPlants()
        {
            var list = new List<PlantController>();
            if (plantGrowthManager != null && plantGrowthManager.Plants != null)
            {
                foreach (var p in plantGrowthManager.Plants)
                    if (p != null) list.Add(p);
            }
            if (list.Count == 0)
            {
                var found = FindObjectsByType<PlantController>(FindObjectsSortMode.None);
                foreach (var p in found) list.Add(p);
            }
            return list;
        }
    }
}
