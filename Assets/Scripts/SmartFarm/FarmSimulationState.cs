using System;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Serializable state snapshot for farm simulation.
    /// Used by NetworkSyncLayer to broadcast from host to clients.
    /// </summary>
    [Serializable]
    public struct FarmSimulationState
    {
        public float soilMoisturePercent;
        public float cropHealthPercent;
        public float waterUsageToday;
        public float temperature;
        public int predictedYield;
        public bool irrigationEnabled;
        public string activeAlertsJson;  // JSON array of alert strings
        public long timestampTicks;

        public static FarmSimulationState Default => new FarmSimulationState
        {
            soilMoisturePercent = 50f,
            cropHealthPercent = 100f,
            waterUsageToday = 0f,
            temperature = 24f,
            predictedYield = 0,
            irrigationEnabled = false,
            activeAlertsJson = "[]",
            timestampTicks = DateTime.UtcNow.Ticks
        };
    }
}
