using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

namespace PlantGrowth
{
    /// <summary>
    /// Singleton manager: runs tick-based plant simulation.
    /// No per-plant Update(); uses coroutine with fixed interval.
    /// Quest-friendly: minimal allocations, batched updates.
    /// </summary>
    public class PlantGrowthManager : MonoBehaviour
    {
        public static PlantGrowthManager Instance { get; private set; }

        [Header("Tick Settings")]
        [Tooltip("Interval in seconds between simulation ticks")]
        [SerializeField] private float tickInterval = 0.5f;

        [Header("Environment (applied to all plants if no override)")]
        [SerializeField] [Range(0, 100)] private float globalSunlight = 75f;
        [SerializeField] [Range(0, 50)] private float globalTemperature = 24f;

        [Header("Persistence")]
        [SerializeField] private bool autoSaveOnTick = true;
        [SerializeField] private float autoSaveIntervalSeconds = 30f;

        private readonly List<PlantController> _plants = new List<PlantController>();
        private Coroutine _tickCoroutine;
        private float _lastAutoSaveTime;

        public IReadOnlyList<PlantController> Plants => _plants;

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
            if (Instance == this)
                Instance = null;
            if (_tickCoroutine != null)
                StopCoroutine(_tickCoroutine);
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) SaveNow();
        }

        private void Start()
        {
            StartCoroutine(InitializeAndRun());
        }

        private IEnumerator InitializeAndRun()
        {
            yield return null; // Defer 1 frame so VR/network can connect first

            FindAndRegisterAllPlantsInScene();
            Debug.Log($"[PlantGrowth] Manager started. Found {_plants.Count} plants in scene. (If 0, run Tools > Farm > Farm Setup)");
            if (_plants.Count == 0) yield break;

            bool isClient = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer;

            if (isClient)
            {
                // Client: initialize plants with defaults (no save load, no tick)
                foreach (var p in _plants)
                    if (p != null) p.Initialize(null);
                Debug.Log("[PlantGrowth] Client - plants initialized, simulation disabled (host-authoritative).");
                yield break;
            }

#if UNITY_EDITOR
            PlantSaveLoadService.DeleteSave(); // In editor: always start fresh each Play
#endif
            PlantSaveLoadService.LoadAndRestorePlants(_plants);
            ApplyCatchUpSimulation();
            ApplyGlobalEnvironment();
            _tickCoroutine = StartCoroutine(TickLoop());
            _lastAutoSaveTime = Time.time;
        }

        /// <summary>
        /// Find all PlantControllers in the scene and register them. Ensures setup works regardless of script order.
        /// </summary>
        private void FindAndRegisterAllPlantsInScene()
        {
            var allPlants = FindObjectsByType<PlantController>(FindObjectsSortMode.None);
            foreach (var p in allPlants)
            {
                if (p != null && !_plants.Contains(p))
                    _plants.Add(p);
            }
        }

        /// <summary>
        /// Register a plant. Called by PlantController or when instantiating.
        /// </summary>
        public void RegisterPlant(PlantController plant)
        {
            if (plant == null || _plants.Contains(plant)) return;
            _plants.Add(plant);
        }

        /// <summary>
        /// Unregister a plant (e.g., when destroyed).
        /// </summary>
        public void UnregisterPlant(PlantController plant)
        {
            _plants.Remove(plant);
        }

        /// <summary>
        /// Set global sunlight (0..100). Applied to all plants on next tick.
        /// </summary>
        public void SetGlobalSunlight(float value)
        {
            globalSunlight = Mathf.Clamp(value, 0, 100);
        }

        /// <summary>
        /// Set global temperature (Celsius). Applied to all plants on next tick.
        /// </summary>
        public void SetGlobalTemperature(float value)
        {
            globalTemperature = value;
        }

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(tickInterval);
            while (true)
            {
                yield return wait;
                float dt = tickInterval;
                ApplyGlobalEnvironment();
                for (int i = _plants.Count - 1; i >= 0; i--)
                {
                    var p = _plants[i];
                    if (p == null)
                    {
                        _plants.RemoveAt(i);
                        continue;
                    }
                    p.SimulateTick(dt);
                }
                if (autoSaveOnTick && Time.time - _lastAutoSaveTime >= autoSaveIntervalSeconds)
                {
                    PlantSaveLoadService.SavePlants(_plants);
                    _lastAutoSaveTime = Time.time;
                }
            }
        }

        private void ApplyGlobalEnvironment()
        {
            foreach (var p in _plants)
            {
                if (p == null || p.IsDead) continue;
                p.SetSunlight(globalSunlight);
                p.SetTemperature(globalTemperature);
            }
        }

        /// <summary>
        /// Simulate plants for time passed since last save (offline catch-up).
        /// Capped to prevent freeze from corrupted/old save data.
        /// </summary>
        private void ApplyCatchUpSimulation()
        {
            const float maxStepSeconds = 5f;
            const float maxCatchUpSeconds = 300f; // Cap total: 5 min max offline catch-up
            foreach (var plant in _plants)
            {
                if (plant == null || plant.IsDead) continue;
                var elapsed = plant.GetTimeSinceLastSimulation();
                float totalSeconds = (float)elapsed.TotalSeconds;
                if (totalSeconds <= 0) continue;
                totalSeconds = Mathf.Min(totalSeconds, maxCatchUpSeconds); // Prevent freeze from corrupted data

                while (totalSeconds > 0)
                {
                    float step = Mathf.Min(totalSeconds, maxStepSeconds);
                    plant.SimulateTick(step);
                    totalSeconds -= step;
                }
            }
        }

        /// <summary>
        /// Force save now. Call before scene unload if needed.
        /// </summary>
        public void SaveNow()
        {
            PlantSaveLoadService.SavePlants(_plants);
        }
    }
}
