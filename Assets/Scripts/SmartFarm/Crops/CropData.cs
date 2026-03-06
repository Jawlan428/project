using UnityEngine;

namespace SmartFarm
{
    public enum CropType { Wheat, Corn }

    /// <summary>
    /// ScriptableObject that defines all parameters for a single crop type (Wheat or Corn).
    /// Create via: Assets > Create > SmartFarm > Crop Data
    /// </summary>
    [CreateAssetMenu(fileName = "NewCropData", menuName = "SmartFarm/Crop Data")]
    public class CropData : ScriptableObject
    {
        [Header("Identity")]
        public string cropName = "Wheat";
        public CropType cropType = CropType.Wheat;

        [Header("Stage Prefabs (0=Seed · 1=Sprout · 2=Young · 3=Mature · 4=Dead)")]
        [Tooltip("Assign Wild Harvest prefabs for each growth stage. Index matches CropStage enum.")]
        public GameObject[] stagePrefabs = new GameObject[5];

        [Header("Stage Durations (seconds per stage)")]
        [Tooltip("How long (in real-time seconds) each stage takes to complete at 1× growth speed.\n" +
                 "Index 0=Seed→Sprout, 1=Sprout→Young, 2=Young→Mature, 3=Mature (steady state).")]
        public float[] stageDurations = { 30f, 60f, 90f, 120f };

        [Header("Ideal Soil & Temperature Ranges")]
        [Range(0f, 100f)] public float waterIdealMin = 40f;
        [Range(0f, 100f)] public float waterIdealMax = 80f;
        [Range(0f,  50f)] public float tempIdealMin  = 15f;
        [Range(0f,  50f)] public float tempIdealMax  = 32f;

        [Header("Soil Moisture Decay")]
        [Tooltip("Soil moisture lost per second naturally (all weather conditions).")]
        public float soilMoistureDecayPerSecond = 1.5f;

        [Header("Weather — Sunny")]
        [Tooltip("Growth speed multiplier when weather is Sunny.")]
        public float sunnyGrowthMultiplier = 1.5f;
        [Tooltip("Extra soil moisture evaporation per second when Sunny (sun dries soil faster).")]
        public float sunnyMoistureDecayBonus = 0.5f;

        [Header("Weather — Rainy")]
        [Tooltip("Growth speed multiplier when weather is Rainy.")]
        public float rainyGrowthMultiplier = 1.0f;
        [Tooltip("Soil moisture gained per second during Rain.")]
        public float rainyMoistureGainPerSecond = 5f;
        [Tooltip("Health recovered per second during Rain.")]
        public float rainyHealthRecoveryPerSecond = 2f;

        [Header("Weather — Storm")]
        [Tooltip("Growth speed multiplier during a Storm.")]
        public float stormGrowthMultiplier = 0.4f;
        [Tooltip("Health damage per second during a Storm.")]
        public float stormHealthDamagePerSecond = 3f;
        [Range(0f, 1f)]
        [Tooltip("Probability per second of a random storm damage spike.")]
        public float stormRandomDamageChance = 0.15f;
        [Tooltip("Extra health damage when a random storm hit occurs.")]
        public float stormRandomDamageAmount = 8f;
        [Tooltip("Soil moisture gained per second during a Storm.")]
        public float stormMoistureGainPerSecond = 8f;

        [Header("Health Penalties")]
        [Tooltip("Health lost per second when soil moisture drops below the critical threshold.")]
        public float healthDecayLowMoisture = 4f;
        [Range(0f, 100f)]
        [Tooltip("Soil moisture level below which drought damage begins.")]
        public float moistureCriticalThreshold = 20f;
        [Tooltip("Health lost per second when temperature is outside the ideal range.")]
        public float healthDecayBadTemp = 2f;

        [Header("Mature Stage Visual")]
        [Tooltip("Extra uniform scale applied only to the Mature stage model. " +
                 "Increase to make ripe wheat/corn look taller and more prominent. Default 1.5.")]
        public float matureStageScaleMultiplier = 1.5f;

        [Header("Yield")]
        [Tooltip("Maximum yield units produced at 100% health.")]
        public float baseYield = 100f;
        [Range(0f, 1f)]
        [Tooltip("Minimum yield ratio even if the crop is in poor health.")]
        public float minYieldHealthRatio = 0.3f;

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns the duration for a given stage index, with a safe fallback.</summary>
        public float GetStageDuration(int stageIndex)
        {
            if (stageDurations == null || stageIndex < 0 || stageIndex >= stageDurations.Length)
                return 60f;
            return Mathf.Max(1f, stageDurations[stageIndex]);
        }
    }
}
