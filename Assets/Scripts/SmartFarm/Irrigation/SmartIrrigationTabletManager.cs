using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Top-level facade for the Smart Irrigation Tablet system.
    ///
    /// Holds and wires together every irrigation subsystem so the rest of the
    /// project (and the tablet UI) only needs a single entry point:
    ///
    ///   <see cref="IrrigationZoneManager"/>      — per-zone state + flow.
    ///   <see cref="WeatherIntegrationSystem"/>   — auto-mode weather reactions.
    ///   <see cref="SoilMoistureSystem"/>         — moisture classification helper (static).
    ///   <see cref="WaterAnalyticsSystem"/>       — usage history.
    ///   <see cref="CropGrowthBridge"/>           — pushes irrigation outcomes to crops.
    ///   <see cref="IrrigationAlertManager"/>     — alerts.
    ///   <see cref="IrrigationVisualFeedback"/>   — pipe glow, particles, audio.
    ///
    /// Holds an aggregate state object (<see cref="LatestSnapshot"/>) the UI can
    /// poll. Re-publishes <see cref="OnDashboardChanged"/> whenever zones tick so
    /// every UI page refreshes coherently.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Smart Irrigation Tablet Manager")]
    [DefaultExecutionOrder(-50)]
    public class SmartIrrigationTabletManager : MonoBehaviour
    {
        public static SmartIrrigationTabletManager Instance { get; private set; }

        [Header("Subsystems (auto-found if empty)")]
        [SerializeField] private IrrigationZoneManager     zoneManager;
        [SerializeField] private WeatherIntegrationSystem  weatherSystem;
        [SerializeField] private WaterAnalyticsSystem      analytics;
        [SerializeField] private CropGrowthBridge          cropBridge;
        [SerializeField] private IrrigationAlertManager    alertManager;
        [SerializeField] private IrrigationVisualFeedback  visuals;

        [Header("Bindings")]
        [SerializeField] private FarmDataManager farmDataManager;

        // ── Public exposure ──────────────────────────────────────────────────

        public IrrigationZoneManager     Zones      => zoneManager;
        public WeatherIntegrationSystem  Weather    => weatherSystem;
        public WaterAnalyticsSystem      Analytics  => analytics;
        public CropGrowthBridge          CropBridge => cropBridge;
        public IrrigationAlertManager    Alerts     => alertManager;
        public IrrigationVisualFeedback  Visuals    => visuals;

        public IrrigationDashboardSnapshot LatestSnapshot { get; private set; }
        public IReadOnlyList<IrrigationZoneSnapshot> ZoneSnapshots => _zoneSnapshotsCache;

        public event Action<IrrigationDashboardSnapshot> OnDashboardChanged;

        // ── Private ──────────────────────────────────────────────────────────

        private readonly List<IrrigationZoneSnapshot> _zoneSnapshotsCache = new List<IrrigationZoneSnapshot>();
        private Coroutine _refreshLoop;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (zoneManager == null)   zoneManager   = GetOrAdd<IrrigationZoneManager>();
            if (weatherSystem == null) weatherSystem = GetOrAdd<WeatherIntegrationSystem>();
            if (analytics == null)     analytics     = GetOrAdd<WaterAnalyticsSystem>();
            if (cropBridge == null)    cropBridge    = GetOrAdd<CropGrowthBridge>();
            if (alertManager == null)  alertManager  = GetOrAdd<IrrigationAlertManager>();
            if (visuals == null)       visuals       = GetOrAdd<IrrigationVisualFeedback>();
            if (farmDataManager == null) farmDataManager = FindFirstObjectByType<FarmDataManager>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private T GetOrAdd<T>() where T : Component
        {
            var c = GetComponent<T>();
            if (c == null) c = gameObject.AddComponent<T>();
            return c;
        }

        private void OnEnable()
        {
            if (zoneManager  != null) zoneManager.OnZonesChanged       += HandleZonesChanged;
            if (weatherSystem != null) weatherSystem.OnWeatherNotice   += HandleWeatherNotice;
            if (analytics    != null) analytics.OnHistoryChanged       += HandleAnalyticsChanged;
            if (alertManager != null) alertManager.OnActiveListChanged += HandleAlertsChanged;

            // Cross-wire references so subsystems can locate each other quickly.
            if (zoneManager != null)
            {
                zoneManager.SetAnalytics(analytics);
                zoneManager.SetGrowthManager(GrowthManager.Instance ?? FindFirstObjectByType<GrowthManager>());
            }
            if (weatherSystem != null) weatherSystem.SetZoneManager(zoneManager);
            if (cropBridge   != null) { cropBridge.SetZoneManager(zoneManager); cropBridge.SetGrowthManager(GrowthManager.Instance ?? FindFirstObjectByType<GrowthManager>()); }
            if (alertManager != null) { alertManager.SetZoneManager(zoneManager); alertManager.SetWeatherSystem(weatherSystem); }
            if (analytics    != null)   analytics.SetZoneManager(zoneManager);
            if (visuals      != null)   visuals.SetZoneManager(zoneManager);

            _refreshLoop = StartCoroutine(RefreshLoop());
        }

        private void OnDisable()
        {
            if (zoneManager  != null) zoneManager.OnZonesChanged       -= HandleZonesChanged;
            if (weatherSystem != null) weatherSystem.OnWeatherNotice   -= HandleWeatherNotice;
            if (analytics    != null) analytics.OnHistoryChanged       -= HandleAnalyticsChanged;
            if (alertManager != null) alertManager.OnActiveListChanged -= HandleAlertsChanged;

            if (_refreshLoop != null) StopCoroutine(_refreshLoop);
        }

        private void Start()
        {
            PublishDashboard();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Refresh loop (publishes a unified snapshot for the tablet UI)
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator RefreshLoop()
        {
            var wait = new WaitForSeconds(0.4f);
            while (true)
            {
                yield return wait;
                PublishDashboard();
            }
        }

        private void HandleZonesChanged(IReadOnlyList<IrrigationZoneSnapshot> snapshots)
        {
            _zoneSnapshotsCache.Clear();
            for (int i = 0; i < snapshots.Count; i++) _zoneSnapshotsCache.Add(snapshots[i]);
            PublishDashboard();
        }

        private void HandleWeatherNotice(WeatherManager.WeatherType _, string __)  => PublishDashboard();
        private void HandleAnalyticsChanged()                                       => PublishDashboard();
        private void HandleAlertsChanged(IReadOnlyList<IrrigationAlert> _)          => PublishDashboard();

        private void PublishDashboard()
        {
            LatestSnapshot = BuildSnapshot();
            OnDashboardChanged?.Invoke(LatestSnapshot);
        }

        private IrrigationDashboardSnapshot BuildSnapshot()
        {
            var snap = new IrrigationDashboardSnapshot
            {
                averageMoisture = zoneManager != null ? zoneManager.AverageMoisture : 50f,
                averageHealth   = zoneManager != null ? zoneManager.AverageHealth : 100f,
                activeZoneCount = zoneManager != null ? zoneManager.ActiveZoneCount : 0,
                totalZones      = zoneManager != null ? zoneManager.Zones.Count : 0,
                totalWaterUsed  = analytics != null ? analytics.SessionTotal : 0f,
                efficiency      = analytics != null ? analytics.Efficiency : 0.85f,
                weather         = weatherSystem != null ? weatherSystem.CurrentWeather : WeatherManager.WeatherType.Sunny,
                weatherNotice   = weatherSystem != null ? weatherSystem.LastNotice : "",
                stormActive     = weatherSystem != null && weatherSystem.IsStormActive,
                activeAlerts    = alertManager != null ? alertManager.ActiveAlerts.Count : 0,
                timestampUtc    = DateTime.UtcNow
            };
            snap.moistureState = zoneManager != null
                ? ClassifyAggregate(snap.averageMoisture)
                : SoilMoistureState.Healthy;
            return snap;
        }

        private static SoilMoistureState ClassifyAggregate(float moisture)
        {
            if (moisture >= 92f) return SoilMoistureState.Overwatered;
            if (moisture >= 60f) return SoilMoistureState.Healthy;
            if (moisture >= 30f) return SoilMoistureState.Medium;
            return SoilMoistureState.Dry;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API (called from UI)
        // ─────────────────────────────────────────────────────────────────────

        public void SetZoneMode(string zoneId, IrrigationZoneMode mode)
        {
            zoneManager?.SetZoneMode(zoneId, mode);
        }

        public void EnableAllZones()
        {
            if (zoneManager == null) return;
            for (int i = 0; i < zoneManager.Zones.Count; i++)
                zoneManager.SetZoneMode(zoneManager.Zones[i].id, IrrigationZoneMode.On);
        }

        public void DisableAllZones()
        {
            if (zoneManager == null) return;
            for (int i = 0; i < zoneManager.Zones.Count; i++)
                zoneManager.SetZoneMode(zoneManager.Zones[i].id, IrrigationZoneMode.Off);
        }

        public void SetAllZonesAuto()
        {
            if (zoneManager == null) return;
            for (int i = 0; i < zoneManager.Zones.Count; i++)
                zoneManager.SetZoneMode(zoneManager.Zones[i].id, IrrigationZoneMode.Auto);
        }
    }

    /// <summary>
    /// Aggregate snapshot of the entire irrigation system. Cheap struct.
    /// </summary>
    public struct IrrigationDashboardSnapshot
    {
        public float averageMoisture;
        public float averageHealth;
        public int   activeZoneCount;
        public int   totalZones;
        public float totalWaterUsed;
        public float efficiency;
        public WeatherManager.WeatherType weather;
        public string weatherNotice;
        public bool   stormActive;
        public int    activeAlerts;
        public SoilMoistureState moistureState;
        public DateTime timestampUtc;
    }
}
