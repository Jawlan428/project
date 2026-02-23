using System;

namespace PlantGrowth
{
    /// <summary>
    /// Serializable plant state for save/load persistence.
    /// </summary>
    [Serializable]
    public class PlantData
    {
        public string plantId;
        public float posX, posY, posZ; // For matching on load
        public int stageIndex;
        public float stageProgress;
        public float health;
        public float waterLevel;
        public float sunlightLevel;
        public float temperature;
        public float fertilizerLevel;
        public long lastSimulatedTimeTicks; // DateTime.UtcNow.Ticks

        public PlantData() { }

        public PlantData(string id, UnityEngine.Vector3 position, int stage, float progress, float health,
            float water, float sunlight, float temp, float fertilizer, long ticks)
        {
            plantId = id;
            posX = position.x; posY = position.y; posZ = position.z;
            stageIndex = stage;
            stageProgress = progress;
            this.health = health;
            waterLevel = water;
            sunlightLevel = sunlight;
            temperature = temp;
            fertilizerLevel = fertilizer;
            lastSimulatedTimeTicks = ticks;
        }

        public DateTime LastSimulatedTime
        {
            get => new DateTime(lastSimulatedTimeTicks, DateTimeKind.Utc);
            set => lastSimulatedTimeTicks = value.ToUniversalTime().Ticks;
        }
    }

    /// <summary>
    /// Wrapper for JSON serialization of multiple plants.
    /// </summary>
    [Serializable]
    public class PlantSaveData
    {
        public PlantData[] plants;
        public long saveTimeTicks;

        public PlantSaveData() { plants = System.Array.Empty<PlantData>(); }

        public PlantSaveData(PlantData[] plantArray, DateTime saveTime)
        {
            plants = plantArray ?? System.Array.Empty<PlantData>();
            saveTimeTicks = saveTime.ToUniversalTime().Ticks;
        }
    }
}
