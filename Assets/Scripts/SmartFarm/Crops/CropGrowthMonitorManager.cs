using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Real-time crop growth monitoring system. Aggregates data from every registered
    /// <see cref="CropGrowthController"/> plus the active <see cref="WeatherManager"/>
    /// and exposes a structured <see cref="CropMonitorReading"/> the world-space monitor
    /// UI consumes.
    ///
    /// Display modes:
    ///   ─ Aggregate (sampleCount &gt; 1): averages across every crop, picks the crop
    ///     closest to harvest as the timer reference.
    ///   ─ Per-Type (Wheat / Corn): averages only crops of the selected CropType.
    ///   ─ Single Crop: focuses on one specific CropGrowthController by index.
    ///
    /// Quest-friendly: zero per-frame Update; one coroutine running at <see cref="pollInterval"/>.
    /// </summary>
    [AddComponentMenu("SmartFarm/Crops/Crop Growth Monitor Manager")]
    public class CropGrowthMonitorManager : MonoBehaviour
    {
        public enum FocusMode
        {
            Aggregate,
            PerType,
            Single
        }

        [Header("Polling")]
        [SerializeField, Tooltip("Seconds between monitor reading updates. Lower = smoother UI, higher = cheaper.")]
        [Range(0.05f, 2f)]
        private float pollInterval = 0.25f;

        [Header("Focus")]
        [SerializeField] private FocusMode focusMode = FocusMode.PerType;
        [SerializeField] private CropType  focusCropType = CropType.Wheat;
        [SerializeField] private int       focusSingleIndex = 0;

        [Header("References (auto-found if empty)")]
        [SerializeField] private GrowthManager  growthManager;
        [SerializeField] private WeatherManager weatherManager;

        // ── Public API ────────────────────────────────────────────────────────

        public CropMonitorReading CurrentReading { get; private set; }
        public FocusMode CurrentMode             => focusMode;
        public CropType  CurrentCropType         => focusCropType;
        public int       CurrentSingleIndex      => focusSingleIndex;

        /// <summary>Fires every poll tick with the latest reading.</summary>
        public event Action<CropMonitorReading> OnReadingChanged;

        /// <summary>Fires when the focus mode/index/type changes (UI buttons can listen).</summary>
        public event Action OnFocusChanged;

        // ── Private ───────────────────────────────────────────────────────────

        private Coroutine _loop;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            CurrentReading = CropMonitorReading.Empty(WeatherManager.WeatherType.Sunny);
        }

        private void OnEnable()
        {
            if (growthManager == null)
                growthManager = GrowthManager.Instance ?? FindFirstObjectByType<GrowthManager>();
            if (weatherManager == null)
                weatherManager = FindFirstObjectByType<WeatherManager>();

            if (weatherManager != null)
                weatherManager.OnWeatherChanged += HandleWeatherChanged;

            CropGrowthController.OnCropStageChanged += HandleCropStageChanged;
            CropGrowthController.OnCropHarvested    += HandleCropHarvested;

            _loop = StartCoroutine(PollLoop());
            Refresh();
        }

        private void OnDisable()
        {
            if (weatherManager != null)
                weatherManager.OnWeatherChanged -= HandleWeatherChanged;

            CropGrowthController.OnCropStageChanged -= HandleCropStageChanged;
            CropGrowthController.OnCropHarvested    -= HandleCropHarvested;

            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Focus controls (called from UI)
        // ─────────────────────────────────────────────────────────────────────

        public void SetFocusMode(FocusMode mode)
        {
            if (focusMode == mode) return;
            focusMode = mode;
            OnFocusChanged?.Invoke();
            Refresh();
        }

        public void SetFocusCropType(CropType cropType)
        {
            focusMode      = FocusMode.PerType;
            focusCropType  = cropType;
            OnFocusChanged?.Invoke();
            Refresh();
        }

        public void SetFocusSingle(int index)
        {
            int total = GetTotalCrops();
            if (total <= 0) { focusMode = FocusMode.Aggregate; OnFocusChanged?.Invoke(); Refresh(); return; }
            focusMode        = FocusMode.Single;
            focusSingleIndex = (index % total + total) % total; // safe modulo
            OnFocusChanged?.Invoke();
            Refresh();
        }

        /// <summary>
        /// Cycles between the two pages: All Wheat ↔ All Corn.
        /// Bound to the monitor's "Next" button.
        /// </summary>
        public void CycleFocusForward()
        {
            focusMode     = FocusMode.PerType;
            focusCropType = focusCropType == CropType.Wheat ? CropType.Corn : CropType.Wheat;
            OnFocusChanged?.Invoke();
            Refresh();
        }

        /// <summary>
        /// Cycles between the two pages: All Wheat ↔ All Corn.
        /// Bound to the monitor's "Previous" button.
        /// </summary>
        public void CycleFocusBackward()
        {
            focusMode     = FocusMode.PerType;
            focusCropType = focusCropType == CropType.Wheat ? CropType.Corn : CropType.Wheat;
            OnFocusChanged?.Invoke();
            Refresh();
        }

        /// <summary>Harvests the currently-focused crop(s). Returns total yield.</summary>
        public float HarvestFocused()
        {
            float total = 0f;
            var crops = GetFocusedCrops();
            for (int i = 0; i < crops.Count; i++)
            {
                if (crops[i] != null && crops[i].IsHarvestable)
                    total += crops[i].Harvest();
            }
            if (total > 0f)
                EventLogger.LogEvent($"Crop Monitor: harvested focused crops — {total:F0} units");
            Refresh();
            return total;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Polling
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator PollLoop()
        {
            var wait = new WaitForSeconds(pollInterval);
            while (true)
            {
                Refresh();
                yield return wait;
            }
        }

        public void Refresh()
        {
            // Late-bind references in case they were created after this manager
            if (growthManager == null)
                growthManager = GrowthManager.Instance ?? FindFirstObjectByType<GrowthManager>();
            if (weatherManager == null)
                weatherManager = FindFirstObjectByType<WeatherManager>();

            var weather = weatherManager != null
                ? weatherManager.CurrentWeather
                : WeatherManager.WeatherType.Sunny;

            var crops = GetFocusedCrops();
            if (crops.Count == 0)
            {
                var empty = CropMonitorReading.Empty(weather);
                empty.displayName = GetFocusLabel();
                CurrentReading = empty;
                OnReadingChanged?.Invoke(CurrentReading);
                return;
            }

            CurrentReading = BuildReading(crops, weather);
            OnReadingChanged?.Invoke(CurrentReading);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Reading builder
        // ─────────────────────────────────────────────────────────────────────

        private CropMonitorReading BuildReading(IList<CropGrowthController> crops, WeatherManager.WeatherType weather)
        {
            float totalHealth   = 0f;
            float totalWater    = 0f;
            float totalProgress = 0f;
            float totalMultiplier = 0f;
            int   harvestableCount = 0;
            int   deadCount        = 0;

            CropGrowthController focusCrop = null;
            int   maxStageRank = -1;
            float maxStageProgress = -1f;

            for (int i = 0; i < crops.Count; i++)
            {
                var c = crops[i];
                if (c == null) continue;

                totalHealth     += c.Health;
                totalWater      += c.SoilMoisture;
                totalProgress   += ComputeOverallProgress(c);
                totalMultiplier += Mathf.Max(0.01f, c.GrowthMultiplier);

                if (c.IsHarvestable) harvestableCount++;
                if (c.CurrentStage == CropStage.Dead) deadCount++;

                int rank = StageRank(c.CurrentStage);
                if (rank > maxStageRank ||
                   (rank == maxStageRank && c.GrowthProgress > maxStageProgress))
                {
                    maxStageRank     = rank;
                    maxStageProgress = c.GrowthProgress;
                    focusCrop        = c;
                }
            }

            int n = crops.Count;
            var reading = new CropMonitorReading
            {
                sampleCount             = n,
                displayName             = GetFocusLabel(),
                cropType                = focusCrop != null && focusCrop.Data != null ? focusCrop.Data.cropType : focusCropType,
                stage                   = focusCrop != null ? focusCrop.CurrentStage : CropStage.Seed,
                stageProgress           = focusCrop != null ? Mathf.Clamp01(focusCrop.GrowthProgress) : 0f,
                overallProgress         = Mathf.Clamp01(totalProgress / n),
                healthPercent           = Mathf.Clamp(totalHealth / n, 0f, 100f),
                waterPercent            = Mathf.Clamp(totalWater  / n, 0f, 100f),
                estimatedHarvestSeconds = focusCrop != null ? EstimateHarvestSeconds(focusCrop) : 0f,
                isHarvestReady          = harvestableCount > 0,
                isDead                  = (deadCount == n) || (focusCrop != null && focusCrop.CurrentStage == CropStage.Dead),
                weather                 = weather,
                growthMultiplier        = totalMultiplier / n,
                timestampTicks          = DateTime.UtcNow.Ticks
            };

            return reading;
        }

        private static int StageRank(CropStage s)
        {
            // Rank Mature highest so the "closest to harvest" crop drives the timer.
            // Dead is forced lowest so dead crops never become the focus row.
            switch (s)
            {
                case CropStage.Dead:   return -1;
                case CropStage.Seed:   return  0;
                case CropStage.Sprout: return  1;
                case CropStage.Young:  return  2;
                case CropStage.Mature: return  3;
                default:               return  0;
            }
        }

        private static float ComputeOverallProgress(CropGrowthController crop)
        {
            if (crop == null) return 0f;
            if (crop.CurrentStage == CropStage.Mature) return 1f;
            if (crop.CurrentStage == CropStage.Dead)   return 0f;
            int stagesUntilMature = (int)CropStage.Mature; // 3 transitions: Seed→Sprout→Young→Mature
            int  current   = (int)crop.CurrentStage;
            float baseProg = current / (float)stagesUntilMature;
            float inStage  = Mathf.Clamp01(crop.GrowthProgress) / stagesUntilMature;
            return Mathf.Clamp01(baseProg + inStage);
        }

        private float EstimateHarvestSeconds(CropGrowthController crop)
        {
            if (crop == null || crop.Data == null) return 0f;
            if (crop.CurrentStage == CropStage.Mature) return 0f;
            if (crop.CurrentStage == CropStage.Dead)   return 0f;

            float globalSpeed = growthManager != null ? growthManager.GlobalGrowthSpeed : 1f;
            float multiplier  = Mathf.Max(0.05f, crop.GrowthMultiplier);

            int   currentIdx = (int)crop.CurrentStage;
            float remainingInStage = (1f - Mathf.Clamp01(crop.GrowthProgress))
                                   * crop.Data.GetStageDuration(currentIdx);
            float seconds = remainingInStage / (globalSpeed * multiplier);

            for (int s = currentIdx + 1; s < (int)CropStage.Mature; s++)
                seconds += crop.Data.GetStageDuration(s) / (globalSpeed * multiplier);

            return Mathf.Max(0f, seconds);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Focus helpers
        // ─────────────────────────────────────────────────────────────────────

        public string GetFocusLabel()
        {
            switch (focusMode)
            {
                case FocusMode.Aggregate: return "All Crops";
                case FocusMode.PerType:   return focusCropType == CropType.Wheat ? "All Wheat" : "All Corn";
                case FocusMode.Single:
                    var single = GetSingleCrop();
                    if (single != null && single.Data != null)
                        return $"{single.Data.cropName} #{focusSingleIndex + 1}";
                    return "Crop";
                default: return "Crop";
            }
        }

        private int GetTotalCrops()
        {
            if (growthManager == null) return 0;
            var all = growthManager.GetAllCrops();
            return all != null ? all.Count : 0;
        }

        private CropGrowthController GetSingleCrop()
        {
            if (growthManager == null) return null;
            var all = growthManager.GetAllCrops();
            if (all == null || all.Count == 0) return null;
            int idx = (focusSingleIndex % all.Count + all.Count) % all.Count;
            return all[idx];
        }

        public List<CropGrowthController> GetFocusedCrops()
        {
            var result = new List<CropGrowthController>();
            if (growthManager == null) return result;

            var all = growthManager.GetAllCrops();
            if (all == null) return result;

            switch (focusMode)
            {
                case FocusMode.Aggregate:
                    for (int i = 0; i < all.Count; i++) if (all[i] != null) result.Add(all[i]);
                    break;
                case FocusMode.PerType:
                    for (int i = 0; i < all.Count; i++)
                    {
                        var c = all[i];
                        if (c == null || c.Data == null) continue;
                        if (c.Data.cropType == focusCropType) result.Add(c);
                    }
                    break;
                case FocusMode.Single:
                    var single = GetSingleCrop();
                    if (single != null) result.Add(single);
                    break;
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleWeatherChanged(WeatherManager.WeatherType _) => Refresh();

        private void HandleCropStageChanged(CropGrowthController crop, CropStage oldStage, CropStage newStage) => Refresh();

        private void HandleCropHarvested(CropGrowthController crop, float yield) => Refresh();
    }
}
