using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Central manager for all irrigation zones (e.g. Corn Field, Wheat Field).
    ///
    /// Responsibilities:
    ///   • Owns an authoritative <see cref="IrrigationZone"/> list (configured in Inspector).
    ///   • Auto-resolves <see cref="CropGrowthController"/> instances per zone by CropType.
    ///   • Ticks every zone on a single coroutine (0.5s default) — no per-frame Update.
    ///   • Applies water to crops belonging to a zone when its mode is On or Auto+demand.
    ///   • Updates aggregated stats (moisture, health) and notifies listeners.
    ///   • Logs analytics events to <see cref="WaterAnalyticsSystem"/>.
    ///
    /// Quest VR friendly: zero allocations in the tick path; uses a single coroutine.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Irrigation Zone Manager")]
    public class IrrigationZoneManager : MonoBehaviour
    {
        // ── Configuration ────────────────────────────────────────────────────

        [Header("Zones")]
        [Tooltip("Defines the irrigation zones. The setup editor creates a Corn Field and Wheat Field by default.")]
        [SerializeField] private List<IrrigationZone> zones = new List<IrrigationZone>();

        [Header("Tick Settings")]
        [Tooltip("Seconds between zone ticks. Lower = smoother flow, higher = cheaper.")]
        [SerializeField, Range(0.1f, 2f)] private float tickInterval = 0.5f;

        [Header("References (auto-found if empty)")]
        [SerializeField] private GrowthManager growthManager;

        [Header("Analytics")]
        [Tooltip("Optional analytics sink. Wired automatically by SmartIrrigationTabletManager.")]
        [SerializeField] private WaterAnalyticsSystem analytics;

        // ── Public state ─────────────────────────────────────────────────────

        public IReadOnlyList<IrrigationZone> Zones => zones;
        public float TickInterval => tickInterval;

        /// <summary>Total water used today across all zones (resets via WaterAnalyticsSystem).</summary>
        public float TotalWaterUsedSession { get; private set; }

        /// <summary>Total active zones (mode=On or Auto with flow).</summary>
        public int ActiveZoneCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < zones.Count; i++)
                    if (zones[i] != null && zones[i].isFlowing) n++;
                return n;
            }
        }

        /// <summary>Average moisture across every zone (weighted by crop count).</summary>
        public float AverageMoisture
        {
            get
            {
                float total = 0f;
                int   count = 0;
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    if (z == null) continue;
                    int c = Mathf.Max(1, z.cropCount);
                    total += z.averageMoisture * c;
                    count += c;
                }
                return count > 0 ? total / count : 50f;
            }
        }

        /// <summary>Average health across every zone (weighted by crop count).</summary>
        public float AverageHealth
        {
            get
            {
                float total = 0f;
                int   count = 0;
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    if (z == null) continue;
                    int c = Mathf.Max(1, z.cropCount);
                    total += z.averageHealth * c;
                    count += c;
                }
                return count > 0 ? total / count : 100f;
            }
        }

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>Fired after each zone tick with the latest snapshots (cached list, do not mutate).</summary>
        public event Action<IReadOnlyList<IrrigationZoneSnapshot>> OnZonesChanged;

        /// <summary>Fired when any zone's mode changes via UI/API. Carries the zone id + new mode.</summary>
        public event Action<string, IrrigationZoneMode> OnZoneModeChanged;

        // ── Private ──────────────────────────────────────────────────────────

        private readonly List<CropGrowthController> _scratchCrops = new List<CropGrowthController>(16);
        private readonly List<IrrigationZoneSnapshot> _snapshotsCache = new List<IrrigationZoneSnapshot>();
        private Coroutine _tick;

        private bool ShouldRunSimulation => NetworkHelper.IsSimulationAuthority;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (growthManager == null)
                growthManager = GrowthManager.Instance ?? FindFirstObjectByType<GrowthManager>();
        }

        private void OnEnable()
        {
            TryBindSceneVisualRoots();
            _tick = StartCoroutine(TickLoop());
            // Immediate refresh so UI doesn't render an empty state on first frame.
            TickAllZones(0f);
        }

        private void OnDisable()
        {
            if (_tick != null) StopCoroutine(_tick);
            _tick = null;
        }

        /// <summary>
        /// If zone.pipeRoot / sprinklerRoot are missing, looks under
        /// <c>SmartIrrigationSceneVisuals/&lt;zone.id&gt;</c> and assigns them.
        /// Called automatically on enable; safe to call again after loading a scene.
        /// </summary>
        public void TryBindSceneVisualRoots()
        {
            var sceneRoot = GameObject.Find("SmartIrrigationSceneVisuals");
            if (sceneRoot == null) return;

            bool anyChange = false;
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null) continue;
                var zt = sceneRoot.transform.Find(zone.id);
                if (zt == null) continue;

                if (zone.pipeRoot == null)
                {
                    var p = zt.Find("Pipes");
                    if (p != null) { zone.pipeRoot = p; anyChange = true; }
                }
                if (zone.sprinklerRoot == null)
                {
                    var s = zt.Find("Sprinklers");
                    if (s != null) { zone.sprinklerRoot = s; anyChange = true; }
                }
            }

            if (anyChange)
            {
                var visuals = FindFirstObjectByType<IrrigationVisualFeedback>();
                if (visuals != null) visuals.RefreshCache();
            }
        }

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(tickInterval);
            while (true)
            {
                yield return wait;
                TickAllZones(tickInterval);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Tick logic
        // ─────────────────────────────────────────────────────────────────────

        private void TickAllZones(float deltaTime)
        {
            if (growthManager == null)
                growthManager = GrowthManager.Instance ?? FindFirstObjectByType<GrowthManager>();

            _snapshotsCache.Clear();
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (zone == null) continue;

                ResolveCropsForZone(zone, _scratchCrops);
                UpdateAggregates(zone, _scratchCrops);

                bool wantsFlow = ResolveDesiredFlow(zone);
                if (wantsFlow && ShouldRunSimulation)
                    ApplyWater(zone, _scratchCrops, deltaTime);

                // Smooth flow rate towards the desired state for UI lerps
                float targetFlow = wantsFlow ? 1f : 0f;
                zone.flowRate    = Mathf.MoveTowards(zone.flowRate, targetFlow, deltaTime * 4f);
                zone.isFlowing   = wantsFlow;

                _snapshotsCache.Add(zone.Snapshot(_scratchCrops));
            }

            OnZonesChanged?.Invoke(_snapshotsCache);
        }

        private void ResolveCropsForZone(IrrigationZone zone, List<CropGrowthController> outList)
        {
            outList.Clear();
            if (growthManager == null) return;

            var all = growthManager.GetAllCrops();
            if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.Data == null) continue;
                if (c.Data.cropType == zone.cropType) outList.Add(c);
            }
        }

        private static void UpdateAggregates(IrrigationZone zone, IList<CropGrowthController> crops)
        {
            if (crops == null || crops.Count == 0)
            {
                zone.averageMoisture = 50f;
                zone.averageHealth   = 100f;
                zone.cropCount       = 0;
                return;
            }

            float moistureSum = 0f, healthSum = 0f;
            int   sampled = 0;
            for (int i = 0; i < crops.Count; i++)
            {
                var c = crops[i];
                if (c == null) continue;
                moistureSum += c.SoilMoisture;
                healthSum   += c.Health;
                sampled++;
            }

            zone.averageMoisture = sampled > 0 ? moistureSum / sampled : 50f;
            zone.averageHealth   = sampled > 0 ? healthSum   / sampled : 100f;
            zone.cropCount       = sampled;
        }

        private bool ResolveDesiredFlow(IrrigationZone zone)
        {
            switch (zone.mode)
            {
                case IrrigationZoneMode.Off:
                    zone.lastReason = "Manual: OFF";
                    return false;

                case IrrigationZoneMode.On:
                    // Manual override still respects the overwater safety
                    if (zone.averageMoisture >= zone.overwaterThreshold)
                    {
                        zone.lastReason = "Soil already saturated";
                        return false;
                    }
                    zone.lastReason = "Manual: ON";
                    return true;

                case IrrigationZoneMode.Auto:
                default:
                    // Stop when soil is over-saturated
                    if (zone.averageMoisture >= zone.overwaterThreshold)
                    {
                        zone.lastReason = "Auto: soil saturated";
                        return false;
                    }
                    // Trigger when below the healthy moisture target
                    if (zone.averageMoisture < zone.healthyMoistureThreshold)
                    {
                        zone.lastReason = zone.averageMoisture < zone.lowMoistureThreshold
                            ? "Auto: low moisture"
                            : "Auto: top-up";
                        return true;
                    }
                    zone.lastReason = "Auto: standby";
                    return false;
            }
        }

        private void ApplyWater(IrrigationZone zone, IList<CropGrowthController> crops, float deltaTime)
        {
            if (crops == null || crops.Count == 0) return;
            float waterPerCrop = zone.waterPerTick * deltaTime;

            float totalAdded = 0f;
            for (int i = 0; i < crops.Count; i++)
            {
                var c = crops[i];
                if (c == null || c.CurrentStage == CropStage.Dead) continue;
                c.Water(waterPerCrop);
                totalAdded += waterPerCrop;
            }

            zone.totalWaterUsed     += totalAdded;
            TotalWaterUsedSession   += totalAdded;

            if (analytics != null)
                analytics.RecordWaterUsage(zone.id, totalAdded);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  External API (called from UI + SmartIrrigationTabletManager)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Replaces the zone configuration (called by the setup editor).</summary>
        public void SetZones(List<IrrigationZone> newZones)
        {
            zones = newZones ?? new List<IrrigationZone>();
        }

        /// <summary>Adds a new zone. Returns true if added.</summary>
        public bool AddZone(IrrigationZone zone)
        {
            if (zone == null) return false;
            for (int i = 0; i < zones.Count; i++)
                if (zones[i] != null && zones[i].id == zone.id) return false;
            zones.Add(zone);
            return true;
        }

        public IrrigationZone GetZoneById(string id)
        {
            for (int i = 0; i < zones.Count; i++)
                if (zones[i] != null && zones[i].id == id) return zones[i];
            return null;
        }

        public void SetZoneMode(string zoneId, IrrigationZoneMode mode)
        {
            var zone = GetZoneById(zoneId);
            if (zone == null) return;
            if (zone.mode == mode) return;

            zone.mode = mode;
            EventLogger.LogEvent($"Irrigation zone '{zone.displayName}' mode set to {mode}");
            OnZoneModeChanged?.Invoke(zoneId, mode);

            // Force a refresh so UI updates instantly
            TickAllZones(tickInterval);
        }

        /// <summary>
        /// Force-disable every zone (e.g. for storm safety override).
        /// Doesn't mutate user-selected mode — just clamps the resolved flow.
        /// </summary>
        public void ForceDisableAllForReason(string reason)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null) continue;
                z.isFlowing  = false;
                z.flowRate   = Mathf.MoveTowards(z.flowRate, 0f, 1f);
                z.lastReason = reason;
            }
        }

        public void SetAnalytics(WaterAnalyticsSystem system) => analytics = system;
        public void SetGrowthManager(GrowthManager mgr)       => growthManager = mgr;
    }
}
