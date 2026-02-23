using UnityEngine;
using System;

namespace PlantGrowth
{
    /// <summary>
    /// ScriptableObject defining plant growth stages, ideal ranges, and decay rates.
    /// Create via: Right-click > Create > Plant Growth > Plant Stage Asset
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlantStageAsset", menuName = "Plant Growth/Plant Stage Asset")]
    public class PlantStageAsset : ScriptableObject
    {
        [Header("Stage Configuration")]
        [Tooltip("Prefabs for each growth stage (Seed, Sprout, Young, Mature, optional Dead)")]
        public GameObject[] stagePrefabs;

        [Tooltip("Duration in seconds to complete each stage. Length should match stage count.")]
        public float[] stageDurations = { 10f, 30f, 60f, 120f };

        [Tooltip("If true, Stage 4 is Dead/Overripe. Otherwise, Stage 3 (Mature) is final.")]
        public bool hasDeadStage = false;

        [Tooltip("Scale applied to stage visuals. Use 2-3 for Pandazole pack models (they're small).")]
        public float stageVisualScale = 1f;

        [Header("Ideal Ranges (0-100 unless noted)")]
        [Range(0, 100)]
        public float waterIdealMin = 50f;
        [Range(0, 100)]
        public float waterIdealMax = 80f;

        [Range(0, 100)]
        public float sunlightIdealMin = 60f;
        [Range(0, 100)]
        public float sunlightIdealMax = 90f;

        [Tooltip("Temperature in Celsius")]
        public float temperatureIdealMin = 18f;
        public float temperatureIdealMax = 30f;

        [Range(0, 100)]
        public float fertilizerIdealMin = 30f;
        [Range(0, 100)]
        public float fertilizerIdealMax = 70f;

        [Header("Decay Rates (per real-time second)")]
        [Tooltip("Water lost per second when not watered")]
        public float waterDecayPerSecond = 2f;

        [Tooltip("Fertilizer lost per second")]
        public float fertilizerDecayPerSecond = 0.5f;

        [Header("Health Penalties")]
        [Tooltip("Health lost per second when water below critical threshold")]
        public float healthDecayLowWater = 5f;
        [Range(0, 100)]
        public float waterCriticalThreshold = 20f;

        [Tooltip("Health lost per second when temp out of range")]
        public float healthDecayBadTemp = 3f;

        [Tooltip("Health lost per second when sunlight too low")]
        public float healthDecayLowSunlight = 2f;
        [Range(0, 100)]
        public float sunlightCriticalThreshold = 30f;

        /// <summary>
        /// Returns the duration for a given stage index. Returns 0 for invalid/dead stages.
        /// </summary>
        public float GetStageDuration(int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= stageDurations.Length)
                return 0f;
            return stageDurations[stageIndex];
        }

        /// <summary>
        /// Returns true if the given stage index is the final (mature or dead) stage.
        /// </summary>
        public bool IsFinalStage(int stageIndex)
        {
            int finalIndex = hasDeadStage ? stageDurations.Length - 1 : stageDurations.Length - 1;
            return stageIndex >= finalIndex || stageIndex < 0;
        }

        /// <summary>
        /// Number of growth stages (excluding dead if separate).
        /// </summary>
        public int StageCount => stageDurations?.Length ?? 0;

        private void OnValidate()
        {
            if (stageDurations != null && stageDurations.Length > 0)
            {
                for (int i = 0; i < stageDurations.Length; i++)
                    if (stageDurations[i] < 0.1f)
                        stageDurations[i] = 1f;
            }
        }
    }
}
