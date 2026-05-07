using System;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Immutable snapshot of a crop monitor reading produced by
    /// <see cref="CropGrowthMonitorManager"/> every tick.
    ///
    /// Consumed by the world-space monitor UI, alert system and any analytics
    /// listener. Designed to be cheap to construct (struct) and self-contained
    /// so the UI never needs to reach back into the simulation classes.
    /// </summary>
    [Serializable]
    public struct CropMonitorReading
    {
        /// <summary>Number of crops contributing to this reading (0 in empty fields).</summary>
        public int   sampleCount;

        /// <summary>Display name (e.g. "Wheat", "Corn", or "All Crops" when aggregated).</summary>
        public string displayName;

        /// <summary>Type of the focused crop (defaults to Wheat for aggregate).</summary>
        public CropType cropType;

        /// <summary>Stage to display (uses the most-advanced stage for aggregate readings).</summary>
        public CropStage stage;

        /// <summary>Progress within the current stage, 0..1 (used to drive the stage progress bar).</summary>
        public float stageProgress;

        /// <summary>Overall lifecycle progress, 0..1 (Seed start → Mature complete).</summary>
        public float overallProgress;

        /// <summary>Crop health, 0..100.</summary>
        public float healthPercent;

        /// <summary>Soil moisture / water level, 0..100.</summary>
        public float waterPercent;

        /// <summary>Estimated real-time seconds until the focused crop is ready to harvest. Negative = already mature.</summary>
        public float estimatedHarvestSeconds;

        /// <summary>True when the focused crop (or any crop in aggregate mode) is currently mature.</summary>
        public bool isHarvestReady;

        /// <summary>True when stage == Dead.</summary>
        public bool isDead;

        /// <summary>Active world weather.</summary>
        public WeatherManager.WeatherType weather;

        /// <summary>Current growth multiplier driven by the weather (1.0 = baseline).</summary>
        public float growthMultiplier;

        /// <summary>Timestamp (UTC ticks) at which the reading was generated.</summary>
        public long timestampTicks;

        /// <summary>Returns a friendly mm:ss / "READY" / "—" string for the harvest timer.</summary>
        public string FormatHarvestTime()
        {
            if (isDead) return "—";
            if (isHarvestReady || estimatedHarvestSeconds <= 0f) return "READY";

            float total = Mathf.Max(0f, estimatedHarvestSeconds);
            int   minutes = Mathf.FloorToInt(total / 60f);
            int   seconds = Mathf.FloorToInt(total - minutes * 60f);
            if (minutes >= 60)
            {
                int hours = minutes / 60;
                minutes %= 60;
                return $"{hours:D1}:{minutes:D2}:{seconds:D2}";
            }
            return $"{minutes:D2}:{seconds:D2}";
        }

        public static CropMonitorReading Empty(WeatherManager.WeatherType weather) => new CropMonitorReading
        {
            sampleCount             = 0,
            displayName             = "No Crops",
            cropType                = CropType.Wheat,
            stage                   = CropStage.Seed,
            stageProgress           = 0f,
            overallProgress         = 0f,
            healthPercent           = 100f,
            waterPercent            = 60f,
            estimatedHarvestSeconds = 0f,
            isHarvestReady          = false,
            isDead                  = false,
            weather                 = weather,
            growthMultiplier        = 1f,
            timestampTicks          = DateTime.UtcNow.Ticks
        };
    }
}
