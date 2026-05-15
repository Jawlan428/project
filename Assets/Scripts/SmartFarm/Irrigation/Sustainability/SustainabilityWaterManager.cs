using System;
using System.Collections;
using UnityEngine;

namespace SmartFarm.Irrigation.Sustainability
{
    /// <summary>
    /// Top-level facade for the Smart Irrigation Sustainability layer.
    ///
    /// Aggregates the five sustainability sub-modules and re-publishes a single
    /// <see cref="LatestSnapshot"/> the UI can poll:
    ///
    ///   • <see cref="WaterSavingTracker"/>        — Water Saved Today counter.
    ///   • <see cref="IrrigationEfficiencySystem"/> — circular efficiency gauge.
    ///   • <see cref="WeatherOptimizationSystem"/>  — smart recommendation text.
    ///   • <see cref="SustainabilityScoreSystem"/>  — overall score 0..100.
    ///   • <see cref="EcoAlertManager"/>            — eco popups/list.
    ///
    /// The manager is intentionally lightweight — it just wires references, runs
    /// a slow 4Hz refresh loop, and publishes <see cref="OnSnapshotChanged"/>
    /// every time the player should see new numbers.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/Sustainability Water Manager")]
    [DefaultExecutionOrder(-40)]
    public class SustainabilityWaterManager : MonoBehaviour
    {
        public static SustainabilityWaterManager Instance { get; private set; }

        [Header("Sub-systems (auto-found / added if empty)")]
        [SerializeField] private WaterSavingTracker         waterSaver;
        [SerializeField] private IrrigationEfficiencySystem efficiencySystem;
        [SerializeField] private WeatherOptimizationSystem  weatherOptimization;
        [SerializeField] private SustainabilityScoreSystem  scoreSystem;
        [SerializeField] private EcoAlertManager            ecoAlerts;

        [Header("Existing irrigation system (auto-found if empty)")]
        [SerializeField] private SmartIrrigationTabletManager tabletManager;
        [SerializeField] private IrrigationZoneManager        zoneManager;
        [SerializeField] private WeatherIntegrationSystem     weatherSystem;
        [SerializeField] private WeatherManager               weatherManager;
        [SerializeField] private WaterAnalyticsSystem         analytics;

        [Header("Auto-irrigation toggle")]
        [Tooltip("When enabled, the Sustainability Monitor's Auto toggle drives every zone into Auto mode.")]
        [SerializeField] private bool autoIrrigationEnabled = true;

        // ── Exposure ─────────────────────────────────────────────────────────

        public WaterSavingTracker         Saver       => waterSaver;
        public IrrigationEfficiencySystem Efficiency  => efficiencySystem;
        public WeatherOptimizationSystem  WeatherOpt  => weatherOptimization;
        public SustainabilityScoreSystem  Score       => scoreSystem;
        public EcoAlertManager            Alerts      => ecoAlerts;
        public bool                       AutoIrrigation => autoIrrigationEnabled;

        public SustainabilitySnapshot LatestSnapshot { get; private set; }
        public event Action<SustainabilitySnapshot> OnSnapshotChanged;
        public event Action<bool>                   OnAutoIrrigationToggled;

        // ── Private ──────────────────────────────────────────────────────────

        private Coroutine _refresh;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            ResolveReferences();
            CrossWire();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            _refresh = StartCoroutine(RefreshLoop());
        }

        private void OnDisable()
        {
            if (_refresh != null) StopCoroutine(_refresh);
        }

        private void Start()
        {
            ApplyAutoIrrigationState(silent: true);
            PublishSnapshot();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring
        // ─────────────────────────────────────────────────────────────────────

        private void ResolveReferences()
        {
            if (waterSaver           == null) waterSaver           = GetOrAdd<WaterSavingTracker>();
            if (efficiencySystem     == null) efficiencySystem     = GetOrAdd<IrrigationEfficiencySystem>();
            if (weatherOptimization  == null) weatherOptimization  = GetOrAdd<WeatherOptimizationSystem>();
            if (scoreSystem          == null) scoreSystem          = GetOrAdd<SustainabilityScoreSystem>();
            if (ecoAlerts            == null) ecoAlerts            = GetOrAdd<EcoAlertManager>();

            if (tabletManager  == null) tabletManager  = SmartIrrigationTabletManager.Instance ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
            if (zoneManager    == null) zoneManager    = tabletManager != null ? tabletManager.Zones    : FindFirstObjectByType<IrrigationZoneManager>();
            if (weatherSystem  == null) weatherSystem  = tabletManager != null ? tabletManager.Weather  : FindFirstObjectByType<WeatherIntegrationSystem>();
            if (analytics      == null) analytics      = tabletManager != null ? tabletManager.Analytics: FindFirstObjectByType<WaterAnalyticsSystem>();
            if (weatherManager == null) weatherManager = FindFirstObjectByType<WeatherManager>();
        }

        private void CrossWire()
        {
            if (waterSaver         != null) waterSaver.SetReferences(zoneManager, weatherSystem, analytics);
            if (efficiencySystem   != null) efficiencySystem.SetReferences(zoneManager, analytics);
            if (weatherOptimization!= null) weatherOptimization.SetReferences(weatherSystem, weatherManager, zoneManager);
            if (scoreSystem        != null) scoreSystem.SetReferences(efficiencySystem, weatherOptimization, waterSaver, zoneManager);
            if (ecoAlerts          != null) ecoAlerts.SetReferences(weatherOptimization, waterSaver, efficiencySystem, scoreSystem, zoneManager);
        }

        private T GetOrAdd<T>() where T : Component
        {
            var c = GetComponent<T>();
            if (c == null) c = gameObject.AddComponent<T>();
            return c;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Refresh loop
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator RefreshLoop()
        {
            var wait = new WaitForSeconds(0.25f);
            while (true)
            {
                yield return wait;
                PublishSnapshot();
            }
        }

        private void PublishSnapshot()
        {
            LatestSnapshot = new SustainabilitySnapshot
            {
                waterSavedLitres      = waterSaver != null ? waterSaver.WaterSavedTodayLitres : 0f,
                efficiency01          = efficiencySystem != null ? efficiencySystem.Efficiency01 : 0f,
                weatherOptimization01 = weatherOptimization != null ? weatherOptimization.OptimizationScore01 : 0f,
                sustainabilityScore01 = scoreSystem != null ? scoreSystem.Score01 : 0f,
                grade                 = scoreSystem != null ? scoreSystem.Grade() : "—",
                recommendation        = weatherOptimization != null ? weatherOptimization.Recommendation : "",
                weather               = weatherOptimization != null ? weatherOptimization.CurrentWeather : WeatherManager.WeatherType.Sunny,
                activeEcoAlerts       = ecoAlerts != null ? ecoAlerts.ActiveAlerts.Count : 0,
                autoIrrigation        = autoIrrigationEnabled,
                timestampUtc          = DateTime.UtcNow
            };
            OnSnapshotChanged?.Invoke(LatestSnapshot);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API (XR / UI)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Resets all sustainability analytics (savings counter, score, alerts).</summary>
        public void ResetAnalytics()
        {
            if (waterSaver  != null) waterSaver.ResetCounter();
            if (scoreSystem != null) scoreSystem.ResetScore();
            if (ecoAlerts   != null) ecoAlerts.ClearAll();
            EventLogger.LogEvent("[Sustainability] Analytics reset by user.");
            PublishSnapshot();
        }

        /// <summary>Toggles the global "Auto irrigation" state — used by the XR button.</summary>
        public void SetAutoIrrigation(bool enabled)
        {
            if (autoIrrigationEnabled == enabled) return;
            autoIrrigationEnabled = enabled;
            ApplyAutoIrrigationState(silent: false);
            OnAutoIrrigationToggled?.Invoke(autoIrrigationEnabled);
            PublishSnapshot();
        }

        public void ToggleAutoIrrigation() => SetAutoIrrigation(!autoIrrigationEnabled);

        private void ApplyAutoIrrigationState(bool silent)
        {
            if (tabletManager == null) return;
            if (autoIrrigationEnabled) tabletManager.SetAllZonesAuto();
            else                       tabletManager.DisableAllZones();
            if (!silent)
                EventLogger.LogEvent(autoIrrigationEnabled
                    ? "[Sustainability] Auto irrigation enabled."
                    : "[Sustainability] Auto irrigation disabled.");
        }
    }

    /// <summary>Cheap struct holding the live values shown on the Sustainability page.</summary>
    public struct SustainabilitySnapshot
    {
        public float    waterSavedLitres;
        public float    efficiency01;
        public float    weatherOptimization01;
        public float    sustainabilityScore01;
        public string   grade;
        public string   recommendation;
        public WeatherManager.WeatherType weather;
        public int      activeEcoAlerts;
        public bool     autoIrrigation;
        public DateTime timestampUtc;
    }
}
