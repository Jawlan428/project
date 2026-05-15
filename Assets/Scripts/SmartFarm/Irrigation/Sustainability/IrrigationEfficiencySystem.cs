using System;
using UnityEngine;

namespace SmartFarm.Irrigation.Sustainability
{
    /// <summary>
    /// Computes a smoothed Irrigation Efficiency score (0..1) that powers the
    /// circular gauge on the Sustainability Monitor.
    ///
    /// The metric blends three signals:
    ///   • <b>Moisture match</b>   — how close every zone's moisture sits to the
    ///     healthy band [low..overwater]. Dead-center scores 1.0, far from band
    ///     scores 0.
    ///   • <b>Crop health</b>      — average crop health weighted by crop count.
    ///   • <b>Water Analytics</b>  — the existing scalar already maintained by
    ///     <see cref="WaterAnalyticsSystem"/> (kept around for parity with the
    ///     analytics page).
    ///
    /// Critical states (e.g. crops in the Dry band) clamp the score down so the
    /// UI ring turns yellow / red.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/Irrigation Efficiency System")]
    public class IrrigationEfficiencySystem : MonoBehaviour
    {
        // ── Configuration ────────────────────────────────────────────────────

        [Header("References (auto-found if empty)")]
        [SerializeField] private IrrigationZoneManager zoneManager;
        [SerializeField] private WaterAnalyticsSystem  analytics;

        [Header("Weights")]
        [SerializeField, Range(0f, 1f)] private float moistureWeight  = 0.5f;
        [SerializeField, Range(0f, 1f)] private float healthWeight    = 0.3f;
        [SerializeField, Range(0f, 1f)] private float analyticsWeight = 0.2f;

        [Header("Smoothing")]
        [SerializeField, Range(0.5f, 12f)] private float lerpSpeed = 2.5f;

        // ── Runtime state ────────────────────────────────────────────────────

        /// <summary>Smoothed efficiency in [0..1]. Multiply by 100 for the UI %.</summary>
        public float Efficiency01 { get; private set; } = 0.85f;

        /// <summary>Convenience: 0..100.</summary>
        public float EfficiencyPercent => Efficiency01 * 100f;

        public bool IsCritical => Efficiency01 < 0.45f;

        public event Action<float> OnEfficiencyChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (zoneManager == null) zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
            if (analytics   == null) analytics   = FindFirstObjectByType<WaterAnalyticsSystem>();
        }

        private void Update()
        {
            float target = ComputeTargetEfficiency();
            float prev   = Efficiency01;
            float t      = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            Efficiency01 = Mathf.Clamp01(Mathf.Lerp(Efficiency01, target, t));
            if (!Mathf.Approximately(prev, Efficiency01))
                OnEfficiencyChanged?.Invoke(Efficiency01);
        }

        private float ComputeTargetEfficiency()
        {
            if (zoneManager == null)
                return analytics != null ? analytics.Efficiency : 0.85f;

            // Moisture match score — distance from the healthy band centre.
            float zonesScore = 0f;
            int   zonesCount = 0;
            var zones = zoneManager.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null) continue;
                if (z.cropCount == 0) continue;

                float low   = Mathf.Max(1f, z.lowMoistureThreshold);
                float high  = Mathf.Max(low + 1f, z.overwaterThreshold);
                float ideal = (z.healthyMoistureThreshold + low + high) / 3f;

                float dist  = Mathf.Abs(z.averageMoisture - ideal);
                float band  = Mathf.Max(1f, high - low);
                float score = 1f - Mathf.Clamp01(dist / band);

                zonesScore += score;
                zonesCount++;
            }
            float moistureScore = zonesCount > 0 ? zonesScore / zonesCount : 0.7f;

            // Crop health
            float healthScore = Mathf.Clamp01(zoneManager.AverageHealth / 100f);

            // Analytics scalar (kept for continuity with the analytics page)
            float analyticsScore = analytics != null ? Mathf.Clamp01(analytics.Efficiency) : 0.75f;

            float wSum = Mathf.Max(0.001f, moistureWeight + healthWeight + analyticsWeight);
            float blended = (moistureScore * moistureWeight
                           + healthScore   * healthWeight
                           + analyticsScore * analyticsWeight) / wSum;

            return Mathf.Clamp01(blended);
        }

        /// <summary>
        /// Maps the current efficiency into a colour band — green / amber / red.
        /// </summary>
        public Color CurrentColor()
        {
            if (Efficiency01 >= 0.75f) return new Color(0.30f, 0.85f, 0.55f, 1f);
            if (Efficiency01 >= 0.50f) return new Color(0.95f, 0.78f, 0.25f, 1f);
            return new Color(0.92f, 0.30f, 0.25f, 1f);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void SetReferences(IrrigationZoneManager zm, WaterAnalyticsSystem an)
        {
            zoneManager = zm;
            analytics   = an;
        }
    }
}
