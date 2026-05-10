using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Operating mode for a single irrigation zone.
    /// </summary>
    public enum IrrigationZoneMode
    {
        Off  = 0,
        On   = 1,
        Auto = 2
    }

    /// <summary>
    /// Soil moisture classification used by the tablet UI and alert system.
    /// Mirrors the user-facing labels: Dry → Medium → Healthy → Overwatered.
    /// </summary>
    public enum SoilMoistureState
    {
        Dry         = 0,
        Medium      = 1,
        Healthy     = 2,
        Overwatered = 3
    }

    /// <summary>
    /// Definition + live state for a single irrigation zone (e.g. "Corn Field").
    /// Designed as a serialisable object so it can be configured in the Inspector
    /// and queried/mutated at runtime by <see cref="IrrigationZoneManager"/>.
    /// </summary>
    [Serializable]
    public class IrrigationZone
    {
        // ── Configuration ────────────────────────────────────────────────────

        [Tooltip("Stable id used for analytics + alerts. Must be unique across zones.")]
        public string id = "zone_corn";

        [Tooltip("Display name shown on the tablet (e.g. \"Corn Field\").")]
        public string displayName = "Corn Field";

        [Tooltip("Crop type this zone manages. Used to find matching CropGrowthControllers.")]
        public CropType cropType = CropType.Corn;

        [Tooltip("Optional irrigation pipe glow root — gets enabled when zone is active.")]
        public Transform pipeRoot;

        [Tooltip("Optional water particle system root — toggled with zone activation.")]
        public Transform sprinklerRoot;

        [Header("Moisture Targets")]
        [Range(0f, 100f)] public float lowMoistureThreshold      = 30f;
        [Range(0f, 100f)] public float healthyMoistureThreshold  = 60f;
        [Range(0f, 100f)] public float overwaterThreshold        = 92f;

        [Header("Water Flow")]
        [Tooltip("Water units added per crop per tick when irrigation is ON.")]
        public float waterPerTick = 6f;

        // ── Runtime state ────────────────────────────────────────────────────

        [NonSerialized] public IrrigationZoneMode mode = IrrigationZoneMode.Auto;
        [NonSerialized] public bool   isFlowing;          // pipes currently spraying water
        [NonSerialized] public float  averageMoisture = 50f;
        [NonSerialized] public float  averageHealth   = 100f;
        [NonSerialized] public float  totalWaterUsed;     // session-wide water counter
        [NonSerialized] public float  flowRate;           // smoothed 0..1 visualisation value
        [NonSerialized] public string lastReason = "";    // why irrigation is on/off
        [NonSerialized] public int    cropCount;          // crops registered in zone

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current moisture classification. Cheap, no allocations.
        /// </summary>
        public SoilMoistureState ClassifyMoisture()
        {
            if (averageMoisture >= overwaterThreshold) return SoilMoistureState.Overwatered;
            if (averageMoisture >= healthyMoistureThreshold) return SoilMoistureState.Healthy;
            if (averageMoisture >= lowMoistureThreshold)     return SoilMoistureState.Medium;
            return SoilMoistureState.Dry;
        }

        /// <summary>
        /// Convenience copy of the live state for UI consumers that need a snapshot.
        /// </summary>
        public IrrigationZoneSnapshot Snapshot(IList<CropGrowthController> crops)
        {
            return new IrrigationZoneSnapshot
            {
                id              = id,
                displayName     = displayName,
                cropType        = cropType,
                mode            = mode,
                isFlowing       = isFlowing,
                averageMoisture = averageMoisture,
                averageHealth   = averageHealth,
                totalWaterUsed  = totalWaterUsed,
                flowRate        = flowRate,
                cropCount       = crops != null ? crops.Count : cropCount,
                moistureState   = ClassifyMoisture(),
                lastReason      = lastReason
            };
        }
    }

    /// <summary>
    /// Immutable snapshot of a single zone. Passed to the UI so renderers never
    /// reach back into the live runtime state.
    /// </summary>
    public struct IrrigationZoneSnapshot
    {
        public string id;
        public string displayName;
        public CropType cropType;
        public IrrigationZoneMode mode;
        public bool isFlowing;
        public float averageMoisture;
        public float averageHealth;
        public float totalWaterUsed;
        public float flowRate;
        public int cropCount;
        public SoilMoistureState moistureState;
        public string lastReason;
    }
}
