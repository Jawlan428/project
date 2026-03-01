using UnityEngine;
using System;

namespace PlantGrowth
{
    /// <summary>
    /// Controls a single plant instance: growth, health, environmental factors.
    /// No Update() - all simulation driven by PlantGrowthManager tick.
    /// </summary>
    public class PlantController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PlantStageAsset stageAsset;

        [Header("Stage Visual Holder")]
        [Tooltip("Transform where current stage model is instantiated as child")]
        [SerializeField] private Transform stageHolder;

        [Header("Initial State")]
        [SerializeField] [Range(0, 100)] private float initialWater = 70f;
        [SerializeField] [Range(0, 100)] private float initialSunlight = 75f;
        [SerializeField] [Range(0, 50)] private float initialTemperature = 24f;
        [SerializeField] [Range(0, 100)] private float initialFertilizer = 50f;

        // Runtime state (exposed for debug/save)
        private int _stageIndex;
        private float _stageProgress;
        private float _health;
        private float _waterLevel;
        private float _sunlightLevel;
        private float _temperature;
        private float _fertilizerLevel;
        private long _lastSimulatedTimeTicks;
        private string _plantId;
        private GameObject _currentStageInstance;
        private bool _isDead;

        public int StageIndex => _stageIndex;
        public float StageProgress => _stageProgress;
        public float Health => _health;
        public float WaterLevel => _waterLevel;
        public float SunlightLevel => _sunlightLevel;
        public float Temperature => _temperature;
        public float FertilizerLevel => _fertilizerLevel;
        public string PlantId => _plantId;
        public bool IsDead => _isDead;
        public PlantStageAsset StageAsset => stageAsset;

        /// <summary>
        /// Fired when plant advances to a new growth stage. (plant, oldStageIndex, newStageIndex)
        /// Subscribe from SmartFarm.EventLogger or other systems.
        /// </summary>
        public static event System.Action<PlantController, int, int> OnStageChanged;

        private void Awake()
        {
            if (stageHolder == null)
                stageHolder = transform;
            if (string.IsNullOrEmpty(_plantId))
                _plantId = Guid.NewGuid().ToString("N");
        }

        private void OnEnable()
        {
            if (PlantGrowthManager.Instance != null)
                PlantGrowthManager.Instance.RegisterPlant(this);
        }

        private void OnDisable()
        {
            if (PlantGrowthManager.Instance != null)
                PlantGrowthManager.Instance.UnregisterPlant(this);
        }

        /// <summary>
        /// Initialize plant with optional saved data. Call before first tick.
        /// </summary>
        public void Initialize(PlantData savedData = null)
        {
            if (stageAsset == null)
            {
                Debug.LogWarning($"[PlantController] {name}: No PlantStageAsset assigned.");
                return;
            }

            if (savedData != null)
            {
                _plantId = savedData.plantId;
                _stageIndex = Mathf.Clamp(savedData.stageIndex, 0, stageAsset.StageCount - 1);
                _stageProgress = Mathf.Clamp01(savedData.stageProgress);
                _health = Mathf.Clamp(savedData.health, 0, 100);
                _waterLevel = Mathf.Clamp(savedData.waterLevel, 0, 100);
                _sunlightLevel = Mathf.Clamp(savedData.sunlightLevel, 0, 100);
                _temperature = savedData.temperature;
                _fertilizerLevel = Mathf.Clamp(savedData.fertilizerLevel, 0, 100);
                _lastSimulatedTimeTicks = savedData.lastSimulatedTimeTicks;
                _isDead = _health <= 0 || _stageIndex >= stageAsset.StageCount;
            }
            else
            {
                _plantId = Guid.NewGuid().ToString("N");
                _stageIndex = 0;
                _stageProgress = 0f;
                _health = 100f;
                _waterLevel = initialWater;
                _sunlightLevel = initialSunlight;
                _temperature = initialTemperature;
                _fertilizerLevel = initialFertilizer;
                _lastSimulatedTimeTicks = DateTime.UtcNow.Ticks;
                _isDead = false;
            }

            RefreshStageVisual();
        }

        /// <summary>
        /// Simulate plant for given delta time (real seconds).
        /// Called by PlantGrowthManager.
        /// </summary>
        public void SimulateTick(float deltaTime)
        {
            if (stageAsset == null || _isDead) return;

            // Decay water and fertilizer
            _waterLevel = Mathf.Max(0, _waterLevel - stageAsset.waterDecayPerSecond * deltaTime);
            _fertilizerLevel = Mathf.Max(0, _fertilizerLevel - stageAsset.fertilizerDecayPerSecond * deltaTime);

            // Health penalties
            if (_waterLevel < stageAsset.waterCriticalThreshold)
                _health = Mathf.Max(0, _health - stageAsset.healthDecayLowWater * deltaTime);

            float tempMin = stageAsset.temperatureIdealMin;
            float tempMax = stageAsset.temperatureIdealMax;
            if (_temperature < tempMin || _temperature > tempMax)
                _health = Mathf.Max(0, _health - stageAsset.healthDecayBadTemp * deltaTime);

            if (_sunlightLevel < stageAsset.sunlightCriticalThreshold)
                _health = Mathf.Max(0, _health - stageAsset.healthDecayLowSunlight * deltaTime);

            if (_health <= 0)
            {
                _isDead = true;
                if (stageAsset.hasDeadStage && stageAsset.stagePrefabs != null &&
                    stageAsset.stagePrefabs.Length > stageAsset.StageCount)
                {
                    _stageIndex = stageAsset.StageCount; // Dead stage
                    RefreshStageVisual();
                }
                return;
            }

            // Growth multiplier from environmental factors
            float growthMultiplier = ComputeGrowthMultiplier();
            float stageDuration = stageAsset.GetStageDuration(_stageIndex);
            if (stageDuration <= 0) return;

            _stageProgress += (deltaTime / stageDuration) * growthMultiplier;

            while (_stageProgress >= 1f && !stageAsset.IsFinalStage(_stageIndex))
            {
                _stageProgress -= 1f;
                int oldStage = _stageIndex;
                _stageIndex++;
                RefreshStageVisual();
                OnStageChanged?.Invoke(this, oldStage, _stageIndex);
            }

            if (_stageProgress > 1f)
                _stageProgress = 1f;

            _lastSimulatedTimeTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Compute 0..1 multiplier from water, sunlight, temp, fertilizer.
        /// </summary>
        private float ComputeGrowthMultiplier()
        {
            float waterScore = ScoreInRange(_waterLevel, stageAsset.waterIdealMin, stageAsset.waterIdealMax);
            float sunScore = ScoreInRange(_sunlightLevel, stageAsset.sunlightIdealMin, stageAsset.sunlightIdealMax);
            float tempScore = ScoreInRange(_temperature, stageAsset.temperatureIdealMin, stageAsset.temperatureIdealMax);
            float fertScore = ScoreInRange(_fertilizerLevel, stageAsset.fertilizerIdealMin, stageAsset.fertilizerIdealMax);

            // Geometric mean favors balanced conditions
            float product = waterScore * sunScore * tempScore * fertScore;
            return Mathf.Pow(Mathf.Max(0.0001f, product), 0.25f);
        }

        private static float ScoreInRange(float value, float min, float max)
        {
            float mid = (min + max) * 0.5f;
            float halfRange = (max - min) * 0.5f;
            if (halfRange <= 0) return 1f;
            float dist = Mathf.Abs(value - mid);
            if (dist >= halfRange) return 0f;
            return 1f - (dist / halfRange) * 0.5f; // 0.5 to 1.0 in range
        }

        private void RefreshStageVisual()
        {
            if (_currentStageInstance != null)
            {
                Destroy(_currentStageInstance);
                _currentStageInstance = null;
            }

            Transform parent = stageHolder != null ? stageHolder : transform;
            GameObject toInstantiate = null;

            if (stageAsset?.stagePrefabs != null && stageAsset.stagePrefabs.Length > 0)
            {
                int idx = Mathf.Clamp(_stageIndex, 0, stageAsset.stagePrefabs.Length - 1);
                toInstantiate = stageAsset.stagePrefabs[idx];
            }

            if (toInstantiate != null)
            {
                _currentStageInstance = Instantiate(toInstantiate, parent);
            }
            else
            {
                _currentStageInstance = CreateFallbackVisual(parent);
            }

            if (_currentStageInstance != null)
            {
                _currentStageInstance.transform.localPosition = Vector3.zero;
                _currentStageInstance.transform.localRotation = Quaternion.identity;
                float scale = stageAsset != null && stageAsset.stageVisualScale > 0 ? stageAsset.stageVisualScale : 1f;
                _currentStageInstance.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        private static GameObject CreateFallbackVisual(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PlantVisual_Fallback";
            go.transform.SetParent(parent, false);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(0.2f, 0.7f, 0.2f);
                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
            go.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            return go;
        }

        // --- Interaction API ---

        /// <summary>
        /// Add water. Call from VR watering can, etc.
        /// </summary>
        public void Water(float amount)
        {
            if (_isDead) return;
            _waterLevel = Mathf.Clamp(_waterLevel + amount, 0, 100);
        }

        /// <summary>
        /// Add fertilizer. Call from VR fertilizer tool.
        /// </summary>
        public void AddFertilizer(float amount)
        {
            if (_isDead) return;
            _fertilizerLevel = Mathf.Clamp(_fertilizerLevel + amount, 0, 100);
        }

        /// <summary>
        /// Set sunlight (0..100). Typically set by environment/manager.
        /// </summary>
        public void SetSunlight(float normalized)
        {
            _sunlightLevel = Mathf.Clamp(normalized, 0, 100);
        }

        /// <summary>
        /// Set temperature in Celsius. Typically set by environment/manager.
        /// </summary>
        public void SetTemperature(float value)
        {
            _temperature = value;
        }

        /// <summary>
        /// Export current state for save.
        /// </summary>
        public PlantData ExportData()
        {
            return new PlantData(
                _plantId, transform.position, _stageIndex, _stageProgress, _health,
                _waterLevel, _sunlightLevel, _temperature, _fertilizerLevel,
                _lastSimulatedTimeTicks
            );
        }

        /// <summary>
        /// Get time since last simulation (for catch-up). Returns TimeSpan.Zero if ticks invalid.
        /// </summary>
        public TimeSpan GetTimeSinceLastSimulation()
        {
            if (_lastSimulatedTimeTicks <= 0) return TimeSpan.Zero;
            try
            {
                var last = new DateTime(_lastSimulatedTimeTicks, DateTimeKind.Utc);
                var elapsed = DateTime.UtcNow - last;
                if (elapsed.TotalSeconds < 0) return TimeSpan.Zero;
                return elapsed;
            }
            catch { return TimeSpan.Zero; }
        }
    }
}
