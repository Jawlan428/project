using System.Collections;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Growth stage enum matching the 5-stage crop lifecycle.
    /// Integer values align with CropData.stagePrefabs array indices.
    /// </summary>
    public enum CropStage
    {
        Seed   = 0,
        Sprout = 1,
        Young  = 2,
        Mature = 3,
        Dead   = 4
    }

    /// <summary>
    /// Tick-based crop controller for Wheat and Corn.
    /// No per-frame Update — all simulation driven by GrowthManager tick.
    ///
    /// Features:
    ///   - 5 growth stages (Seed → Sprout → Young → Mature → Dead)
    ///   - Weather-driven growth multiplier (Sunny 1.5×, Rainy 1.0×, Storm 0.4×)
    ///   - Soil moisture and health tracking with weather effects
    ///   - Health penalties for drought and temperature stress
    ///   - Yield calculation at harvest based on final health
    ///   - Automatic stage model swap from CropData.stagePrefabs
    ///   - Full EventLogger integration on every stage change
    /// </summary>
    [AddComponentMenu("SmartFarm/Crops/Crop Growth Controller")]
    public class CropGrowthController : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Assign the CropData ScriptableObject for this crop (Wheat_CropData or Corn_CropData).")]
        [SerializeField] private CropData cropData;

        [Tooltip("Empty Transform used as parent when stage prefabs are instantiated. " +
                 "Defaults to this GameObject's transform if left null.")]
        [SerializeField] private Transform stageHolder;

        [Header("Initial Values")]
        [SerializeField, Range(0f, 100f)] private float initialHealth       = 100f;
        [SerializeField, Range(0f, 100f)] private float initialSoilMoisture = 60f;

        [Header("Visual Growth Animation")]
        [Tooltip("How long (seconds) the scale-in animation plays when a new stage model spawns.")]
        [SerializeField, Range(0f, 3f)] private float stageTransitionDuration = 1.0f;

        [Tooltip("Within a stage, the model starts at this scale fraction and grows to 1× as the stage completes.")]
        [SerializeField, Range(0.1f, 1f)] private float stageStartScaleFraction = 0.45f;

        // ── Runtime State ─────────────────────────────────────────────────────

        /// <summary>Current growth stage (Seed, Sprout, Young, Mature, Dead).</summary>
        public CropStage CurrentStage   { get; private set; } = CropStage.Seed;

        /// <summary>Progress within the current stage, 0–1.</summary>
        public float GrowthProgress     { get; private set; }

        /// <summary>Crop health, 0–100. Reaches 0 → crop dies.</summary>
        public float Health             { get; private set; }

        /// <summary>Soil moisture, 0–100. Decays over time; replenished by rain or irrigation.</summary>
        public float SoilMoisture       { get; private set; }

        /// <summary>Current weather-driven growth speed multiplier.</summary>
        public float GrowthMultiplier   { get; private set; } = 1f;

        /// <summary>True once the crop reaches Mature stage and is ready to harvest.</summary>
        public bool  IsHarvestable      { get; private set; }

        /// <summary>Estimated yield at current health. Updated whenever the crop reaches Mature.</summary>
        public float EstimatedYield     { get; private set; }

        /// <summary>Unique identifier for audit log entries.</summary>
        public string CropId            { get; private set; }

        /// <summary>Reference to the assigned CropData asset.</summary>
        public CropData Data            => cropData;

        // ── Static Events ─────────────────────────────────────────────────────

        /// <summary>Fired when a crop changes stage. (controller, oldStage, newStage)</summary>
        public static event System.Action<CropGrowthController, CropStage, CropStage> OnCropStageChanged;

        /// <summary>Fired when a crop is harvested. (controller, yieldAmount)</summary>
        public static event System.Action<CropGrowthController, float> OnCropHarvested;

        // ── Private Fields ────────────────────────────────────────────────────

        private WeatherManager.WeatherType _currentWeather = WeatherManager.WeatherType.Sunny;
        private GameObject _activeStageObject;
        private Vector3    _activeStageBaseScale = Vector3.one;
        private Coroutine  _scaleCoroutine;
        private float      _globalTemperature = 24f;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            CropId       = $"{(cropData != null ? cropData.cropName : "Crop")}_{System.Guid.NewGuid():N}"[..16];
            Health       = initialHealth;
            SoilMoisture = initialSoilMoisture;

            if (stageHolder == null)
                stageHolder = transform;
        }

        private void Start()
        {
            // Subscribe to weather changes so GrowthMultiplier updates instantly
            var wm = FindFirstObjectByType<WeatherManager>();
            if (wm != null)
            {
                wm.OnWeatherChanged += HandleWeatherChanged;
                _currentWeather      = wm.CurrentWeather;
                UpdateGrowthMultiplier(_currentWeather);
            }

            // Register with the central GrowthManager
            GrowthManager.Instance?.RegisterCrop(this);

            // Show correct model for starting stage
            ApplyStageVisual(CurrentStage);
        }

        private void OnDestroy()
        {
            var wm = FindFirstObjectByType<WeatherManager>();
            if (wm != null) wm.OnWeatherChanged -= HandleWeatherChanged;

            GrowthManager.Instance?.UnregisterCrop(this);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  External API (called by GrowthManager, FarmSimulationManager, VR tools)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Simulate one tick. Called exclusively by GrowthManager — never per-frame.
        /// </summary>
        public void SimulateTick(float deltaTime)
        {
            if (cropData == null || CurrentStage == CropStage.Dead) return;

            // Mature crops are permanently grown — immune to all weather damage.
            // In Sunny/Rainy they grew to completion; we keep them at full health forever.
            if (CurrentStage == CropStage.Mature)
            {
                Health = 100f;
                return;
            }

            ApplyWeatherEffects(deltaTime);
            ApplyHealthPenalties(deltaTime);

            if (Health <= 0f)
            {
                KillCrop();
                return;
            }

            AdvanceGrowth(deltaTime);
            UpdateProgressiveScale();
        }

        /// <summary>Sync global temperature from FarmSimulationManager state.</summary>
        public void SetGlobalTemperature(float temp) => _globalTemperature = temp;

        /// <summary>Add water (irrigation or manual watering). Clamps to 0–100.</summary>
        public void Water(float amount) =>
            SoilMoisture = Mathf.Min(100f, SoilMoisture + amount);

        /// <summary>Apply a health change (positive = heal, negative = damage).</summary>
        public void ModifyHealth(float delta) =>
            Health = Mathf.Clamp(Health + delta, 0f, 100f);

        /// <summary>
        /// Client-only: applies a host-authoritative stage and health without running
        /// any growth logic or firing stage-change events.
        /// Called by CropFieldNetworkSync when it receives updated data from the host.
        /// </summary>
        public void ApplyNetworkState(CropStage stage, float health)
        {
            bool stageChanged = CurrentStage != stage;

            CurrentStage  = stage;
            Health        = Mathf.Clamp(health, 0f, 100f);
            IsHarvestable = (stage == CropStage.Mature);

            if (stageChanged)
                ApplyStageVisual(stage); // triggers scale-in animation on client too

            // For mature crops, ensure scale is at 100%
            if (stage == CropStage.Mature && _activeStageObject != null && _scaleCoroutine == null)
                _activeStageObject.transform.localScale = _activeStageBaseScale;
        }

        /// <summary>
        /// Harvest this crop (VR grab or tablet button).
        /// Returns the yield amount; resets the crop to Seed stage.
        /// Returns 0 if not yet harvestable.
        /// </summary>
        public float Harvest()
        {
            if (!IsHarvestable) return 0f;

            float yield = CalculateYield();
            OnCropHarvested?.Invoke(this, yield);
            EventLogger.LogEvent(
                $"{cropData.cropName} harvested — Yield: {yield:F0} units (Health: {Health:F0}%)",
                transform.position);

            ResetToSeed();
            return yield;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Weather Handling
        // ─────────────────────────────────────────────────────────────────────

        private void HandleWeatherChanged(WeatherManager.WeatherType newWeather)
        {
            _currentWeather = newWeather;
            UpdateGrowthMultiplier(newWeather);
        }

        private void UpdateGrowthMultiplier(WeatherManager.WeatherType weather)
        {
            if (cropData == null) return;

            GrowthMultiplier = weather switch
            {
                WeatherManager.WeatherType.Sunny => cropData.sunnyGrowthMultiplier,
                WeatherManager.WeatherType.Rainy => cropData.rainyGrowthMultiplier,
                WeatherManager.WeatherType.Storm => cropData.stormGrowthMultiplier,
                _                                => 1f
            };
        }

        private void ApplyWeatherEffects(float deltaTime)
        {
            switch (_currentWeather)
            {
                case WeatherManager.WeatherType.Sunny:
                    // Extra evaporation when sunny
                    SoilMoisture = Mathf.Max(0f, SoilMoisture - cropData.sunnyMoistureDecayBonus * deltaTime);
                    break;

                case WeatherManager.WeatherType.Rainy:
                    SoilMoisture = Mathf.Min(100f, SoilMoisture + cropData.rainyMoistureGainPerSecond    * deltaTime);
                    Health       = Mathf.Min(100f, Health       + cropData.rainyHealthRecoveryPerSecond  * deltaTime);
                    break;

                case WeatherManager.WeatherType.Storm:
                    SoilMoisture = Mathf.Min(100f, SoilMoisture + cropData.stormMoistureGainPerSecond   * deltaTime);
                    Health       = Mathf.Max(0f,   Health       - cropData.stormHealthDamagePerSecond   * deltaTime);

                    // Random spike damage from wind/hail
                    if (cropData.stormRandomDamageChance > 0f &&
                        Random.value < cropData.stormRandomDamageChance * deltaTime)
                    {
                        Health = Mathf.Max(0f, Health - cropData.stormRandomDamageAmount);
                    }
                    break;
            }

            // Natural soil moisture evaporation (all weather conditions)
            SoilMoisture = Mathf.Max(0f, SoilMoisture - cropData.soilMoistureDecayPerSecond * deltaTime);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Health Penalties / Recovery
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyHealthPenalties(float deltaTime)
        {
            // Sunny and Rainy are safe weather — crops slowly recover health and cannot die.
            // Only Storm weather can damage and kill crops.
            if (_currentWeather != WeatherManager.WeatherType.Storm)
            {
                Health = Mathf.Min(100f, Health + 2f * deltaTime); // passive health recovery
                return;
            }

            // Storm: drought damage
            if (SoilMoisture < cropData.moistureCriticalThreshold)
                Health -= cropData.healthDecayLowMoisture * deltaTime;

            // Storm: temperature stress
            bool tempOk = _globalTemperature >= cropData.tempIdealMin &&
                          _globalTemperature <= cropData.tempIdealMax;
            if (!tempOk)
                Health -= cropData.healthDecayBadTemp * deltaTime;

            Health = Mathf.Clamp(Health, 0f, 100f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Growth Progression
        // ─────────────────────────────────────────────────────────────────────

        private void AdvanceGrowth(float deltaTime)
        {
            int   stageIndex    = (int)CurrentStage;
            float stageDuration = cropData.GetStageDuration(stageIndex);

            GrowthProgress += (deltaTime * GrowthMultiplier) / stageDuration;

            if (GrowthProgress >= 1f)
            {
                GrowthProgress = 0f;
                PromoteStage();
            }
        }

        private void PromoteStage()
        {
            CropStage oldStage  = CurrentStage;
            int       nextIndex = Mathf.Min((int)CurrentStage + 1, (int)CropStage.Mature);
            CurrentStage        = (CropStage)nextIndex;

            ApplyStageVisual(CurrentStage);

            OnCropStageChanged?.Invoke(this, oldStage, CurrentStage);
            EventLogger.LogEvent($"{cropData.cropName} reached {CurrentStage} stage", transform.position);
            EventLogger.LogPlantStageChanged(CropId, (int)CurrentStage, CurrentStage.ToString());

            if (CurrentStage == CropStage.Mature)
            {
                IsHarvestable  = true;
                EstimatedYield = CalculateYield();
                EventLogger.LogEvent(
                    $"{cropData.cropName} is ready to harvest! " +
                    $"Estimated yield: {EstimatedYield:F0} units (Health: {Health:F0}%)",
                    transform.position);
            }
        }

        private void KillCrop()
        {
            CropStage old = CurrentStage;
            CurrentStage  = CropStage.Dead;
            Health        = 0f;
            IsHarvestable = false;
            GrowthProgress = 0f;

            ApplyStageVisual(CropStage.Dead);
            OnCropStageChanged?.Invoke(this, old, CropStage.Dead);
            EventLogger.LogEvent($"{cropData.cropName} died (poor conditions)", transform.position);
            EventLogger.LogPlantStageChanged(CropId, (int)CropStage.Dead, "Dead");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Yield & Reset
        // ─────────────────────────────────────────────────────────────────────

        private float CalculateYield()
        {
            if (cropData == null) return 0f;
            float healthRatio = Mathf.Max(cropData.minYieldHealthRatio, Health / 100f);
            return cropData.baseYield * healthRatio;
        }

        /// <summary>
        /// Resets the crop fully back to Seed stage with full health and moisture.
        /// Called automatically on weather change (via GrowthManager) and after harvest.
        /// </summary>
        public void ResetToSeed()
        {
            if (_scaleCoroutine != null) { StopCoroutine(_scaleCoroutine); _scaleCoroutine = null; }
            CurrentStage   = CropStage.Seed;
            GrowthProgress = 0f;
            Health         = initialHealth;
            SoilMoisture   = initialSoilMoisture;
            IsHarvestable  = false;
            EstimatedYield = 0f;
            ApplyStageVisual(CropStage.Seed);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Stage Visuals — animated
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyStageVisual(CropStage stage)
        {
            if (cropData == null || stageHolder == null) return;

            // Cancel any running scale coroutine before destroying the old object
            if (_scaleCoroutine != null) { StopCoroutine(_scaleCoroutine); _scaleCoroutine = null; }
            if (_activeStageObject != null) Destroy(_activeStageObject);

            int index = (int)stage;
            if (cropData.stagePrefabs == null || index >= cropData.stagePrefabs.Length) return;

            GameObject prefab = cropData.stagePrefabs[index];
            if (prefab == null) return;

            _activeStageObject = Instantiate(prefab, stageHolder.position, stageHolder.rotation, stageHolder);
            _activeStageBaseScale = _activeStageObject.transform.localScale;

            // Dead stage → no animation, just show it flat
            if (stage == CropStage.Dead)
            {
                _activeStageObject.transform.localScale = _activeStageBaseScale;
                return;
            }

            // Start at a small scale and animate up to the stage-start fraction
            float startFrac = (stage == CropStage.Seed) ? 0f : stageStartScaleFraction * 0.5f;
            _activeStageObject.transform.localScale = _activeStageBaseScale * startFrac;

            // Transition animation: scale to stageStartScaleFraction over stageTransitionDuration
            float targetFrac = (stage == CropStage.Mature) ? 1f : stageStartScaleFraction;
            _scaleCoroutine = StartCoroutine(AnimateScale(
                _activeStageObject, _activeStageBaseScale * startFrac,
                _activeStageBaseScale * targetFrac, stageTransitionDuration));
        }

        private IEnumerator AnimateScale(GameObject target, Vector3 from, Vector3 to, float duration)
        {
            if (target == null || duration <= 0f) { if (target != null) target.transform.localScale = to; yield break; }

            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                target.transform.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }
            if (target != null) target.transform.localScale = to;
            _scaleCoroutine = null;
        }

        /// <summary>
        /// Called every tick while growing. Lerps the model's scale based on GrowthProgress
        /// so the plant visually gets bigger as it advances through the stage.
        /// Only runs while a stage transition coroutine is NOT active.
        /// </summary>
        private void UpdateProgressiveScale()
        {
            if (_activeStageObject == null || _scaleCoroutine != null) return;
            if (CurrentStage == CropStage.Dead || CurrentStage == CropStage.Mature) return;

            // Scale goes from stageStartScaleFraction (progress=0) to 1.0 (progress=1)
            float scaleFrac = Mathf.Lerp(stageStartScaleFraction, 1f, GrowthProgress);
            _activeStageObject.transform.localScale = _activeStageBaseScale * scaleFrac;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Debug
        // ─────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        public override string ToString() =>
            $"[{cropData?.cropName ?? "Crop"}] Stage:{CurrentStage} ({GrowthProgress:P0}) " +
            $"HP:{Health:F1} Moisture:{SoilMoisture:F1} Multiplier:{GrowthMultiplier:F2} " +
            $"Harvestable:{IsHarvestable} Yield:{EstimatedYield:F0}";
#endif
    }
}
