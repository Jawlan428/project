using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace SmartFarm
{
    /// <summary>
    /// Central tick manager for all CropGrowthController instances.
    ///
    /// Responsibilities:
    ///   - Runs the single coroutine that ticks every registered CropGrowthController
    ///   - Propagates global temperature from FarmSimulationManager to all crops
    ///   - Tracks aggregate stats (average health, mature count, soil moisture, cumulative yield)
    ///   - Exposes HarvestAll() for VR interactions and tablet UI
    ///   - Host-only simulation (mirrors the existing FarmSimulationManager pattern)
    ///
    /// Quest VR friendly: zero per-frame Update, single WaitForSeconds coroutine.
    /// </summary>
    [AddComponentMenu("SmartFarm/Crops/Growth Manager")]
    public class GrowthManager : MonoBehaviour
    {
        public static GrowthManager Instance { get; private set; }

        [Header("Tick Settings")]
        [SerializeField, Tooltip("Seconds between simulation ticks (matches FarmSimulationManager for alignment).")]
        private float tickInterval = 0.5f;

        [Header("Growth Speed")]
        [SerializeField, Tooltip("Multiplies how fast all crops grow. 1 = normal, 3 = 3× faster. Raise to demo the full cycle quicker."), Min(0.1f)]
        private float globalGrowthSpeed = 3f;

        [Header("References (auto-found if left empty)")]
        [SerializeField] private FarmSimulationManager farmSimulationManager;

        // ── Aggregate Stats (read-only, updated every tick) ───────────────────

        /// <summary>Average health (0–100) across all registered crops.</summary>
        public float AverageCropHealth   { get; private set; } = 100f;

        /// <summary>Average soil moisture (0–100) across all registered crops.</summary>
        public float AverageSoilMoisture { get; private set; } = 60f;

        /// <summary>Number of crops currently at the Mature stage.</summary>
        public int   MatureCropCount     { get; private set; }

        /// <summary>Number of mature crops with health >= 60 (mirrors FarmSimulationManager's 'predictedYield' logic).</summary>
        public int   MatureHealthyCropCount { get; private set; }

        /// <summary>Total number of registered crop controllers.</summary>
        public int   TotalCropCount      => _crops.Count;

        /// <summary>Cumulative yield collected across all Harvest() calls this session.</summary>
        public float TotalHarvestedYield { get; private set; }

        /// <summary>Total number of individual harvest events.</summary>
        public int   TotalHarvestCount   { get; private set; }

        /// <summary>Multiplier applied to every crop's tick deltaTime. Used by the Crop Monitor to compute accurate harvest ETAs.</summary>
        public float GlobalGrowthSpeed   => Mathf.Max(0.0001f, globalGrowthSpeed);

        /// <summary>Tick interval (seconds) so external systems can align polling cadence.</summary>
        public float TickInterval        => Mathf.Max(0.05f, tickInterval);

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired after any harvest. Parameter is the new cumulative yield total.</summary>
        public static event System.Action<float> OnYieldAccumulated;

        // ── Private ───────────────────────────────────────────────────────────

        private readonly List<CropGrowthController> _crops = new();
        private Coroutine  _tickCoroutine;
        private CropFieldNetworkSync _cropNetworkSync;
        private WeatherManager       _weatherManager;

        private bool ShouldRunSimulation => NetworkHelper.IsSimulationAuthority;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (farmSimulationManager == null)
                farmSimulationManager = FindFirstObjectByType<FarmSimulationManager>();

            if (farmSimulationManager != null)
                farmSimulationManager.OnStateChanged += HandleSimulationStateChanged;

            // Cache the network sync component (on the same hub, or anywhere in the scene)
            _cropNetworkSync = GetComponent<CropFieldNetworkSync>()
                            ?? FindFirstObjectByType<CropFieldNetworkSync>();

            // Subscribe to weather changes so all crops reset when weather switches
            _weatherManager = FindFirstObjectByType<WeatherManager>();
            if (_weatherManager != null)
                _weatherManager.OnWeatherChanged += HandleWeatherChanged;

            CropGrowthController.OnCropHarvested += HandleCropHarvested;

            // Auto-discover crops that were placed before GrowthManager started
            foreach (var crop in FindObjectsByType<CropGrowthController>(FindObjectsSortMode.None))
                RegisterCrop(crop);

            if (ShouldRunSimulation)
            {
                _tickCoroutine = StartCoroutine(TickLoop());
                EventLogger.LogEvent($"GrowthManager started — {_crops.Count} crop(s) registered (Host)");
            }
            else
            {
                EventLogger.LogEvent("GrowthManager: client mode — crops display only");
            }
        }

        private void OnDestroy()
        {
            if (farmSimulationManager != null)
                farmSimulationManager.OnStateChanged -= HandleSimulationStateChanged;

            if (_weatherManager != null)
                _weatherManager.OnWeatherChanged -= HandleWeatherChanged;

            CropGrowthController.OnCropHarvested -= HandleCropHarvested;

            if (Instance == this) Instance = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Tick Loop
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(tickInterval);
            while (true)
            {
                yield return wait;
                if (!ShouldRunSimulation) continue;
                Tick(tickInterval);
            }
        }

        private void Tick(float deltaTime)
        {
            float totalHealth   = 0f;
            float totalMoisture = 0f;
            int   matureCount   = 0;
            int   matureHealthy = 0;

            // Scale delta time by the global speed multiplier so growth (and weather
            // damage in Storm) all run faster without changing any CropData values.
            float scaledDelta = deltaTime * globalGrowthSpeed;

            // Iterate backwards so we can safely remove null entries
            for (int i = _crops.Count - 1; i >= 0; i--)
            {
                var crop = _crops[i];
                if (crop == null) { _crops.RemoveAt(i); continue; }

                crop.SimulateTick(scaledDelta);

                totalHealth   += crop.Health;
                totalMoisture += crop.SoilMoisture;

                if (crop.CurrentStage == CropStage.Mature)
                {
                    matureCount++;
                    if (crop.Health >= 60f) matureHealthy++;
                }
            }

            int count = _crops.Count;
            AverageCropHealth       = count > 0 ? totalHealth   / count : 100f;
            AverageSoilMoisture     = count > 0 ? totalMoisture / count : 60f;
            MatureCropCount         = matureCount;
            MatureHealthyCropCount  = matureHealthy;

            // Broadcast updated crop states to all clients
            if (_cropNetworkSync == null)
                _cropNetworkSync = FindFirstObjectByType<CropFieldNetworkSync>();

            _cropNetworkSync?.SetCropData(_crops);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Registration
        // ─────────────────────────────────────────────────────────────────────

        public void RegisterCrop(CropGrowthController crop)
        {
            if (crop != null && !_crops.Contains(crop))
                _crops.Add(crop);
        }

        public void UnregisterCrop(CropGrowthController crop) =>
            _crops.Remove(crop);

        // ─────────────────────────────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleSimulationStateChanged(FarmSimulationState state)
        {
            // Propagate authoritative temperature to every crop controller
            foreach (var crop in _crops)
                crop?.SetGlobalTemperature(state.temperature);
        }

        /// <summary>
        /// Called whenever the weather changes (Sunny / Rainy / Storm).
        /// Resets every crop to Seed stage so a fresh growth cycle begins under the new weather.
        /// Sunny and Rainy let crops grow to Mature safely; Storm can kill them.
        /// Only runs on the simulation authority (host / session owner).
        /// </summary>
        private void HandleWeatherChanged(WeatherManager.WeatherType newWeather)
        {
            if (!ShouldRunSimulation) return;

            // Reset every crop — including mature ones — so a new growth cycle starts
            // under the new weather. This way the demo always reacts visually to every
            // weather button press.
            ResetAllCrops();

            EventLogger.LogEvent(
                $"Weather changed to {newWeather} — all {_crops.Count} crop(s) reset to Seed stage");
        }

        private void HandleCropHarvested(CropGrowthController crop, float yield)
        {
            TotalHarvestedYield += yield;
            TotalHarvestCount++;
            OnYieldAccumulated?.Invoke(TotalHarvestedYield);
            EventLogger.LogEvent(
                $"Farm cumulative yield: {TotalHarvestedYield:F0} units " +
                $"({TotalHarvestCount} harvest{(TotalHarvestCount == 1 ? "" : "s")})");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resets every registered crop to Seed stage with full health.
        /// Called automatically on weather change; can also be called from tablet UI or VR tools.
        /// </summary>
        public void ResetAllCrops()
        {
            for (int i = _crops.Count - 1; i >= 0; i--)
            {
                var crop = _crops[i];
                if (crop == null) { _crops.RemoveAt(i); continue; }
                crop.ResetToSeed();
            }

            // Recompute aggregates immediately after reset
            AverageCropHealth   = 100f;
            AverageSoilMoisture = 60f;
            MatureCropCount     = 0;
            MatureHealthyCropCount = 0;

            // Broadcast the reset state to all clients right away
            _cropNetworkSync?.SetCropData(_crops);
        }

        /// <summary>All registered crop controllers (read-only).</summary>
        public IReadOnlyList<CropGrowthController> GetAllCrops() => _crops;

        /// <summary>Only the crops that are currently harvestable.</summary>
        public List<CropGrowthController> GetHarvestableCrops() =>
            _crops.FindAll(c => c != null && c.IsHarvestable);

        /// <summary>
        /// Harvest every mature crop in the scene.
        /// Suitable for a "Harvest All" tablet UI button or VR action.
        /// Returns total yield collected.
        /// </summary>
        public float HarvestAll()
        {
            float total = 0f;
            foreach (var crop in _crops)
            {
                if (crop != null && crop.IsHarvestable)
                    total += crop.Harvest();
            }
            if (total > 0f)
                EventLogger.LogEvent($"HarvestAll — collected {total:F0} total units");
            return total;
        }

        /// <summary>
        /// Returns aggregated crop stats for blending into FarmSimulationState.
        /// Used by FarmSimulationManager.SimulateTick().
        /// </summary>
        public (float avgHealth, float avgMoisture, int matureHealthyCount) GetAggregateStats() =>
            (AverageCropHealth, AverageSoilMoisture, MatureHealthyCropCount);
    }
}
