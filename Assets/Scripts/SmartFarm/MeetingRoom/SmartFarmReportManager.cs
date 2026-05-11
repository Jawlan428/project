using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.MeetingRoom
{
    /// <summary>
    /// Central data source for every <see cref="VRDocumentInteractable"/> in the
    /// meeting area. Each tick the manager pulls fresh stats from
    /// <see cref="FarmSimulationManager"/>, <see cref="WeatherManager"/> and
    /// <see cref="SmartIrrigationManager"/> and pushes them into the documents
    /// that are subscribed to it.
    ///
    /// The manager is intentionally tick-based (default 1 second) to stay
    /// Quest-friendly — it never allocates per-frame.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SmartFarmReportManager : MonoBehaviour
    {
        public static SmartFarmReportManager Instance { get; private set; }

        [Header("Smart Farm Sources")]
        [SerializeField] private FarmSimulationManager simulationManager;
        [SerializeField] private WeatherManager weatherManager;
        [SerializeField] private SmartIrrigationManager irrigationManager;

        [Header("Tick Settings")]
        [Tooltip("Seconds between live report refresh ticks.")]
        [Range(0.25f, 5f)] [SerializeField] private float tickInterval = 1f;

        [Header("Reports")]
        [Tooltip("All report assets that should receive live data.")]
        [SerializeField] private List<SmartFarmReportData> reports = new List<SmartFarmReportData>();

        public event Action<SmartFarmReportData> OnReportUpdated;

        private readonly Dictionary<string, SmartFarmReportData> _byId = new Dictionary<string, SmartFarmReportData>();
        private Coroutine _tickCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            CacheLookup();
        }

        private void Start()
        {
            if (simulationManager == null) simulationManager = FindFirstObjectByType<FarmSimulationManager>();
            if (weatherManager == null) weatherManager = FindFirstObjectByType<WeatherManager>();
            if (irrigationManager == null) irrigationManager = SmartIrrigationManager.Instance;

            _tickCoroutine = StartCoroutine(TickLoop());
        }

        private void OnDestroy()
        {
            if (_tickCoroutine != null) StopCoroutine(_tickCoroutine);
            if (Instance == this) Instance = null;
        }

        private void CacheLookup()
        {
            _byId.Clear();
            for (int i = 0; i < reports.Count; i++)
            {
                var r = reports[i];
                if (r == null || string.IsNullOrEmpty(r.reportId)) continue;
                _byId[r.reportId] = r;
            }
        }

        /// <summary>Returns a registered report by id, or null if not found.</summary>
        public SmartFarmReportData GetReport(string reportId)
        {
            if (string.IsNullOrEmpty(reportId)) return null;
            _byId.TryGetValue(reportId, out var r);
            return r;
        }

        /// <summary>Registers a report so it will receive live data from the next tick onwards.</summary>
        public void Register(SmartFarmReportData report)
        {
            if (report == null) return;
            if (!reports.Contains(report)) reports.Add(report);
            if (!string.IsNullOrEmpty(report.reportId)) _byId[report.reportId] = report;
        }

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(tickInterval);
            while (true)
            {
                yield return wait;
                RefreshAll();
            }
        }

        /// <summary>Force a refresh on demand (e.g. when a document is picked up).</summary>
        public void RefreshAll()
        {
            FarmSimulationState state = simulationManager != null
                ? simulationManager.GetState()
                : FarmSimulationState.Default;

            for (int i = 0; i < reports.Count; i++)
            {
                var r = reports[i];
                if (r == null) continue;
                PopulateReport(r, state);
                OnReportUpdated?.Invoke(r);
            }
        }

        /// <summary>Refresh a single report immediately.</summary>
        public void Refresh(SmartFarmReportData report)
        {
            if (report == null) return;
            FarmSimulationState state = simulationManager != null
                ? simulationManager.GetState()
                : FarmSimulationState.Default;
            PopulateReport(report, state);
            OnReportUpdated?.Invoke(report);
        }

        private void PopulateReport(SmartFarmReportData r, FarmSimulationState state)
        {
            switch (r.reportType)
            {
                case SmartFarmReportType.CropHealth: BuildCropHealth(r, state); break;
                case SmartFarmReportType.Irrigation: BuildIrrigation(r, state); break;
                case SmartFarmReportType.WeatherForecast: BuildWeather(r, state); break;
                case SmartFarmReportType.HarvestPlanning: BuildHarvest(r, state); break;
                case SmartFarmReportType.SoilAnalysis: BuildSoil(r, state); break;
                case SmartFarmReportType.WaterUsage: BuildWaterUsage(r, state); break;
                case SmartFarmReportType.Custom: /* leave user-authored values alone */ break;
            }
        }

        // ── Builders ──────────────────────────────────────────────────────────

        private static ReportMetric Bar(string label, string unit, float value, float max, Color color, bool critical = false)
        {
            return new ReportMetric
            {
                label = label,
                unit = unit,
                value = value,
                maxValue = max,
                color = color,
                isCritical = critical
            };
        }

        private void BuildCropHealth(SmartFarmReportData r, FarmSimulationState s)
        {
            r.title = "Crop Health Report";
            r.subtitle = $"Generated {DateTime.Now:HH:mm}";
            r.body = $"Average crop health is {s.cropHealthPercent:0}%.\n" +
                     $"Predicted yield this cycle: {s.predictedYield} plants.";

            bool healthLow = s.cropHealthPercent < 50f;
            r.recommendations = healthLow
                ? "! Crop health below threshold — increase irrigation cycle.\nReview pest exposure logs."
                : "Maintain current schedule.\nReassess after next watering cycle.";

            r.metrics.Clear();
            r.metrics.Add(Bar("Avg Health", "%", s.cropHealthPercent, 100f, new Color(0.27f, 0.7f, 0.35f), healthLow));
            r.metrics.Add(Bar("Soil Moisture", "%", s.soilMoisturePercent, 100f, new Color(0.32f, 0.55f, 0.92f), s.soilMoisturePercent < 30f));
            r.metrics.Add(Bar("Predicted Yield", " crops", s.predictedYield, Mathf.Max(10, s.predictedYield + 5), new Color(0.92f, 0.68f, 0.18f)));
            r.metrics.Add(Bar("Temperature", "°C", s.temperature, 50f, new Color(0.88f, 0.4f, 0.2f), s.temperature > 35f));
        }

        private void BuildIrrigation(SmartFarmReportData r, FarmSimulationState s)
        {
            string mode = irrigationManager != null ? irrigationManager.CurrentMode.ToString() : "Manual";
            bool active = irrigationManager != null ? irrigationManager.IsIrrigationActive : s.irrigationEnabled;
            string reason = irrigationManager != null ? irrigationManager.LastDecisionReason : "—";

            r.title = "Smart Irrigation Report";
            r.subtitle = $"Mode: {mode}  •  Status: {(active ? "ACTIVE" : "STANDBY")}";
            r.body = string.IsNullOrEmpty(reason)
                ? $"Daily water usage so far: {s.waterUsageToday:0} L."
                : $"Last decision: {reason}.\nDaily water usage so far: {s.waterUsageToday:0} L.";

            r.recommendations = active
                ? "Water is currently being delivered to the field.\nMonitor moisture every 5 minutes."
                : "! Schedule a watering cycle if moisture drops below 30%.";

            r.metrics.Clear();
            r.metrics.Add(Bar("Soil Moisture", "%", s.soilMoisturePercent, 100f, new Color(0.32f, 0.55f, 0.92f), s.soilMoisturePercent < 30f));
            r.metrics.Add(Bar("Water Used Today", " L", s.waterUsageToday, Mathf.Max(50f, s.waterUsageToday + 20f), new Color(0.18f, 0.65f, 0.9f)));
            r.metrics.Add(Bar("Irrigation State", "", active ? 1f : 0f, 1f, active ? new Color(0.27f, 0.7f, 0.35f) : new Color(0.6f, 0.6f, 0.6f)));
        }

        private void BuildWeather(SmartFarmReportData r, FarmSimulationState s)
        {
            string weather = weatherManager != null ? weatherManager.CurrentWeather.ToString() : "Sunny";

            r.title = "Weather Forecast";
            r.subtitle = $"Current condition: {weather}";

            switch (weather)
            {
                case "Sunny":
                    r.body = "Clear skies. Sunlight is at peak for photosynthesis.\nGrowth rate boosted.";
                    r.recommendations = "Maintain irrigation to compensate for evaporation.\n! Watch for heat stress above 35°C.";
                    break;
                case "Rainy":
                    r.body = "Steady rain across the field. Natural irrigation active.\nReduced manual watering needed.";
                    r.recommendations = "Pause scheduled irrigation if soil moisture > 80%.";
                    break;
                case "Storm":
                    r.body = "Storm front detected. Wind and rain intensity high.\nCrop damage risk elevated.";
                    r.recommendations = "! Disable open-field irrigation.\n! Secure greenhouse covers.";
                    break;
                default:
                    r.body = "Forecast data unavailable.";
                    r.recommendations = "—";
                    break;
            }

            r.metrics.Clear();
            r.metrics.Add(Bar("Temperature", "°C", s.temperature, 50f, new Color(0.88f, 0.4f, 0.2f), s.temperature > 35f));
            r.metrics.Add(Bar("Storm Risk", "%", weather == "Storm" ? 85f : weather == "Rainy" ? 35f : 5f, 100f, new Color(0.55f, 0.2f, 0.75f), weather == "Storm"));
            r.metrics.Add(Bar("Sun Intensity", "%", weather == "Sunny" ? 90f : weather == "Rainy" ? 45f : 20f, 100f, new Color(0.95f, 0.78f, 0.25f)));
        }

        private void BuildHarvest(SmartFarmReportData r, FarmSimulationState s)
        {
            r.title = "Harvest Planning";
            r.subtitle = "Projected output for current cycle";
            r.body = $"Projected harvest: {s.predictedYield} mature & healthy crops.\n" +
                     $"Average field health: {s.cropHealthPercent:0}%.";

            r.recommendations = s.predictedYield < 5
                ? "! Yield below target — review planting density and irrigation."
                : "On track. Prepare harvest crew for end of cycle.";

            r.metrics.Clear();
            r.metrics.Add(Bar("Predicted Yield", " crops", s.predictedYield, Mathf.Max(20, s.predictedYield + 5), new Color(0.92f, 0.68f, 0.18f)));
            r.metrics.Add(Bar("Field Health", "%", s.cropHealthPercent, 100f, new Color(0.27f, 0.7f, 0.35f), s.cropHealthPercent < 50f));
            r.metrics.Add(Bar("Soil Moisture", "%", s.soilMoisturePercent, 100f, new Color(0.32f, 0.55f, 0.92f)));
        }

        private void BuildSoil(SmartFarmReportData r, FarmSimulationState s)
        {
            r.title = "Soil Analysis";
            r.subtitle = "Composite reading across field sensors";
            r.body = $"Average soil moisture: {s.soilMoisturePercent:0}%.\n" +
                     $"Field temperature: {s.temperature:0}°C.";

            r.recommendations = s.soilMoisturePercent < 30f
                ? "! Soil moisture critical — start irrigation cycle."
                : "Soil profile within healthy range.";

            r.metrics.Clear();
            r.metrics.Add(Bar("Moisture", "%", s.soilMoisturePercent, 100f, new Color(0.32f, 0.55f, 0.92f), s.soilMoisturePercent < 30f));
            r.metrics.Add(Bar("Temperature", "°C", s.temperature, 50f, new Color(0.88f, 0.4f, 0.2f)));
            // Static-ish synthetic readings to round out the analysis page.
            r.metrics.Add(Bar("pH", "", 6.8f, 14f, new Color(0.5f, 0.35f, 0.2f)));
            r.metrics.Add(Bar("Nitrogen", "%", 62f, 100f, new Color(0.4f, 0.7f, 0.45f)));
        }

        private void BuildWaterUsage(SmartFarmReportData r, FarmSimulationState s)
        {
            r.title = "Water Usage Analytics";
            r.subtitle = $"Today: {s.waterUsageToday:0} L";
            r.body = "Tracks cumulative water delivered to the field since the last daily reset.";

            r.recommendations = s.waterUsageToday > 500f
                ? "! Above average consumption — review schedules."
                : "Consumption within sustainable limits.";

            r.metrics.Clear();
            r.metrics.Add(Bar("Used Today", " L", s.waterUsageToday, Mathf.Max(200f, s.waterUsageToday + 50f), new Color(0.18f, 0.65f, 0.9f)));
            r.metrics.Add(Bar("Soil Moisture", "%", s.soilMoisturePercent, 100f, new Color(0.32f, 0.55f, 0.92f)));
            r.metrics.Add(Bar("Crops Watered", "", s.predictedYield, Mathf.Max(10, s.predictedYield + 5), new Color(0.6f, 0.78f, 0.3f)));
        }
    }
}
