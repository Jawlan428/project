using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlantGrowth;
using Unity.Netcode;

namespace SmartFarm
{
    /// <summary>
    /// Authoritative farm simulation manager. Runs ONLY on host.
    /// Controls: global temperature, irrigation, daily water usage, predicted yield, alerts.
    /// Tick-based (0.5–1s), Quest-friendly. No per-frame Update.
    /// </summary>
    public class FarmSimulationManager : MonoBehaviour
    {
        public static FarmSimulationManager Instance { get; private set; }
        public event System.Action<FarmSimulationState> OnStateChanged;

        [Header("Tick Settings")]
        [SerializeField] [Tooltip("Interval in seconds between simulation ticks")]
        private float tickInterval = 0.5f;

        [Header("Environment")]
        [SerializeField] [Range(0, 50)] private float globalTemperature = 24f;
        [SerializeField] private bool irrigationEnabled;
        [SerializeField] [Tooltip("Water added per plant per tick when irrigation is ON")]
        private float irrigationWaterPerTick = 5f;

        [Header("Alert Thresholds")]
        [SerializeField] private float lowSoilMoistureThreshold = 30f;
        [SerializeField] private float highTemperatureThreshold = 35f;
        [SerializeField] private float criticalCropHealthThreshold = 40f;

        [Header("References")]
        [SerializeField] private FarmSimulationNetworkSync networkSync;
        [SerializeField] private PlantGrowthManager plantGrowthManager;
        [Tooltip("Auto-found at runtime. Leave empty — GrowthManager registers itself.")]
        [SerializeField] private GrowthManager cropGrowthManager;

        private float _waterUsageToday;
        private float _lastDayResetTime;
        private Coroutine _tickCoroutine;
        private readonly List<string> _activeAlerts = new List<string>();

        public float GlobalTemperature => globalTemperature;
        public bool IrrigationEnabled => irrigationEnabled;
        public float WaterUsageToday => _waterUsageToday;
        public IReadOnlyList<string> ActiveAlerts => _activeAlerts;

        /// <summary>
        /// Whether this instance should run the simulation.
        /// Uses NetworkHelper so it works in both LocalOnly (IsServer) and DA (IsSessionOwner) mode.
        /// </summary>
        private bool ShouldRunSimulation => NetworkHelper.IsSimulationAuthority;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            PlantController.OnStageChanged -= OnPlantStageChanged;
            if (Instance == this)
                Instance = null;
            if (_tickCoroutine != null)
                StopCoroutine(_tickCoroutine);
        }

        private void OnPlantStageChanged(PlantController plant, int oldStage, int newStage)
        {
            if (!ShouldRunSimulation) return;
            string stageName = GetStageName(plant, newStage);
            EventLogger.LogPlantStageChanged(plant.PlantId ?? plant.name, newStage, stageName);
        }

        private static string GetStageName(PlantController _, int stageIndex)
        {
            return stageIndex switch
            {
                0 => "Seed",
                1 => "Sprout",
                2 => "Young",
                3 => "Mature",
                4 => "Dead",
                _ => $"Stage{stageIndex}"
            };
        }

        private void Start()
        {
            if (plantGrowthManager == null)
                plantGrowthManager = FindFirstObjectByType<PlantGrowthManager>();

            if (networkSync == null)
                networkSync = GetComponent<FarmSimulationNetworkSync>();

            if (cropGrowthManager == null)
                cropGrowthManager = FindFirstObjectByType<GrowthManager>();

            PlantController.OnStageChanged += OnPlantStageChanged;

            if (ShouldRunSimulation)
            {
                _lastDayResetTime = Time.time;
                EventLogger.LogEvent("Farm Simulation started (Host)");
                _tickCoroutine = StartCoroutine(TickLoop());
            }
            else
            {
                EventLogger.LogEvent("Farm Simulation (Client - display only)");
            }
        }

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(tickInterval);
            while (true)
            {
                yield return wait;
                if (!ShouldRunSimulation) continue;

                SimulateTick(tickInterval);
            }
        }

        /// <summary>
        /// Run one simulation tick. Host only.
        /// </summary>
        private void SimulateTick(float deltaTime)
        {
            var plants = GetPlants();

            if (cropGrowthManager == null)
                cropGrowthManager = GrowthManager.Instance;

            // Skip tick only when there is truly nothing to simulate
            if (plants.Count == 0 && (cropGrowthManager == null || cropGrowthManager.TotalCropCount == 0))
                return;

            // Reset daily water usage every 5 minutes (simplified "day")
            if (Time.time - _lastDayResetTime > 300f)
            {
                _waterUsageToday = 0;
                _lastDayResetTime = Time.time;
            }

            // Apply irrigation to PlantControllers
            if (irrigationEnabled)
            {
                float waterPerPlant = irrigationWaterPerTick * deltaTime;
                foreach (var p in plants)
                {
                    if (p == null || p.IsDead) continue;
                    p.Water(waterPerPlant);
                    _waterUsageToday += waterPerPlant;
                }
            }

            // Apply irrigation to CropGrowthControllers
            if (irrigationEnabled && cropGrowthManager != null)
            {
                float waterPerCrop = irrigationWaterPerTick * deltaTime;
                foreach (var crop in cropGrowthManager.GetAllCrops())
                {
                    if (crop == null || crop.CurrentStage == CropStage.Dead) continue;
                    crop.Water(waterPerCrop);
                    _waterUsageToday += waterPerCrop;
                }
            }

            // Apply global temperature to plants (PlantGrowthManager does this in its tick, but we set our value)
            if (plantGrowthManager != null)
                plantGrowthManager.SetGlobalTemperature(globalTemperature);

            // Compute aggregates from PlantControllers
            float soilSum = 0f, healthSum = 0f;
            int count = 0, matureHealthy = 0;
            foreach (var p in plants)
            {
                if (p == null) continue;
                soilSum += p.WaterLevel;
                healthSum += p.Health;
                count++;
                if (!p.IsDead && p.StageIndex >= 2 && p.Health >= 60f) // Young or Mature, healthy
                    matureHealthy++;
            }

            float soilMoisture = count > 0 ? soilSum   / count : 50f;
            float cropHealth   = count > 0 ? healthSum / count : 100f;

            // Blend in CropGrowthController aggregates from GrowthManager
            if (cropGrowthManager != null && cropGrowthManager.TotalCropCount > 0)
            {
                var (cropAvgHealth, cropAvgMoisture, cropMatureHealthy) = cropGrowthManager.GetAggregateStats();
                int cropCount  = cropGrowthManager.TotalCropCount;
                int totalCount = count + cropCount;

                soilMoisture  = (soilMoisture * count + cropAvgMoisture * cropCount) / totalCount;
                cropHealth    = (cropHealth   * count + cropAvgHealth   * cropCount) / totalCount;
                matureHealthy += cropMatureHealthy;
            }

            // Update alerts
            _activeAlerts.Clear();
            if (soilMoisture < lowSoilMoistureThreshold)
                _activeAlerts.Add("Low Water Level");
            if (globalTemperature > highTemperatureThreshold)
                _activeAlerts.Add("High Temperature Risk");
            if (cropHealth < criticalCropHealthThreshold)
                _activeAlerts.Add("Crop Health Critical");

            // Build state and sync
            var state = new FarmSimulationState
            {
                soilMoisturePercent = soilMoisture,
                cropHealthPercent = cropHealth,
                waterUsageToday = _waterUsageToday,
                temperature = globalTemperature,
                predictedYield = matureHealthy,
                irrigationEnabled = irrigationEnabled,
                activeAlertsJson = "[\"" + string.Join("\",\"", _activeAlerts) + "\"]",
                timestampTicks = System.DateTime.UtcNow.Ticks
            };

            if (networkSync != null && networkSync.IsSpawned)
                networkSync.SetState(state);

            OnStateChanged?.Invoke(state);
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

        /// <summary>
        /// Set global temperature. Host only. Logs to EventLogger.
        /// </summary>
        public void SetGlobalTemperature(float value)
        {
            if (!ShouldRunSimulation) return;
            globalTemperature = Mathf.Clamp(value, 0, 50);
            EventLogger.LogTemperatureChanged(globalTemperature);
        }

        /// <summary>
        /// Set irrigation state. Host only. Typically called when poll result is applied.
        /// </summary>
        public void SetIrrigationEnabled(bool enabled)
        {
            irrigationEnabled = enabled;
            if (!ShouldRunSimulation) return;
            EventLogger.LogIrrigationChanged(enabled);
        }

        /// <summary>
        /// Instant moisture boost across living plants (manual UI action).
        /// </summary>
        public void ApplyInstantMoistureBoost(float amountPerPlant)
        {
            var plants = GetPlants();
            for (int i = 0; i < plants.Count; i++)
            {
                var p = plants[i];
                if (p == null || p.IsDead) continue;
                p.Water(Mathf.Max(0f, amountPerPlant));
                _waterUsageToday += Mathf.Max(0f, amountPerPlant);
            }
            EventLogger.LogEvent($"Irrigation boost applied (+{amountPerPlant:F0} per plant)");
        }

        /// <summary>
        /// Toggle irrigation. Host only.
        /// </summary>
        public void ToggleIrrigation()
        {
            SetIrrigationEnabled(!irrigationEnabled);
        }

        /// <summary>
        /// Get current dashboard state. Works on host and clients (clients get from network sync).
        /// </summary>
        public FarmSimulationState GetState()
        {
            if (networkSync != null && networkSync.IsSpawned)
                return networkSync.GetState();

            // Fallback when not networked
            var plants = GetPlants();
            float soil = 50f, health = 100f;
            int count = 0, yield = 0;
            foreach (var p in plants)
            {
                if (p == null) continue;
                soil += p.WaterLevel;
                health += p.Health;
                count++;
                if (!p.IsDead && p.StageIndex >= 2 && p.Health >= 60) yield++;
            }
            if (count > 0) { soil /= count; health /= count; }

            return new FarmSimulationState
            {
                soilMoisturePercent = soil,
                cropHealthPercent = health,
                waterUsageToday = _waterUsageToday,
                temperature = globalTemperature,
                predictedYield = yield,
                irrigationEnabled = irrigationEnabled,
                activeAlertsJson = "[]",
                timestampTicks = System.DateTime.UtcNow.Ticks
            };
        }
    }
}
