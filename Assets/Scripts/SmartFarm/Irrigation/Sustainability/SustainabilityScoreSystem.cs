using System;
using UnityEngine;

namespace SmartFarm.Irrigation.Sustainability
{
    /// <summary>
    /// Aggregates the sub-modules into a single 0..100 Sustainability Score the
    /// player sees on the Sustainability Monitor.
    ///
    /// Inputs:
    ///   • Efficiency (0..1) from <see cref="IrrigationEfficiencySystem"/>.
    ///   • Weather optimisation (0..1) from <see cref="WeatherOptimizationSystem"/>.
    ///   • Water savings — normalised by a "daily goal" so the score climbs as
    ///     more water is saved, up to a soft cap.
    ///   • Penalty — large overwatering events drag the score down.
    ///
    /// All inputs are smoothed independently, then combined and smoothed once
    /// more so the score never jumps in the UI.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Sustainability/Sustainability Score System")]
    public class SustainabilityScoreSystem : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private IrrigationEfficiencySystem efficiencySystem;
        [SerializeField] private WeatherOptimizationSystem  weatherOptimization;
        [SerializeField] private WaterSavingTracker         waterSaver;
        [SerializeField] private IrrigationZoneManager      zoneManager;

        [Header("Weights")]
        [SerializeField, Range(0f, 1f)] private float efficiencyWeight = 0.45f;
        [SerializeField, Range(0f, 1f)] private float weatherWeight    = 0.30f;
        [SerializeField, Range(0f, 1f)] private float savingsWeight    = 0.25f;

        [Header("Savings normalisation")]
        [Tooltip("Litres saved that map to a perfect contribution from this term.")]
        [SerializeField, Range(20f, 5000f)] private float savingsCapLitres = 300f;

        [Header("Penalties")]
        [Tooltip("Score reduction when any zone is over-saturated.")]
        [SerializeField, Range(0f, 0.4f)] private float overwaterPenalty = 0.15f;

        [Header("Smoothing")]
        [SerializeField, Range(0.5f, 8f)] private float lerpSpeed = 1.5f;

        [SerializeField, Range(0f, 100f)] private float startingScore = 78f;

        // ── State ────────────────────────────────────────────────────────────

        public float Score01 { get; private set; }
        public float ScorePercent => Score01 * 100f;

        public event Action<float> OnScoreChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            Score01 = Mathf.Clamp01(startingScore / 100f);
            if (efficiencySystem     == null) efficiencySystem     = FindFirstObjectByType<IrrigationEfficiencySystem>();
            if (weatherOptimization  == null) weatherOptimization  = FindFirstObjectByType<WeatherOptimizationSystem>();
            if (waterSaver           == null) waterSaver           = FindFirstObjectByType<WaterSavingTracker>();
            if (zoneManager          == null) zoneManager          = FindFirstObjectByType<IrrigationZoneManager>();
        }

        private void Update()
        {
            float target = ComputeTarget();
            float t      = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            float prev   = Score01;
            Score01      = Mathf.Clamp01(Mathf.Lerp(Score01, target, t));
            if (!Mathf.Approximately(prev, Score01))
                OnScoreChanged?.Invoke(Score01);
        }

        private float ComputeTarget()
        {
            float eff      = efficiencySystem    != null ? efficiencySystem.Efficiency01    : 0.75f;
            float weather  = weatherOptimization != null ? weatherOptimization.OptimizationScore01 : 0.75f;

            float savings01 = 0f;
            if (waterSaver != null && savingsCapLitres > 0.001f)
                savings01 = Mathf.Clamp01(waterSaver.WaterSavedTodayLitres / savingsCapLitres);

            float wSum = Mathf.Max(0.001f, efficiencyWeight + weatherWeight + savingsWeight);
            float blended = (eff * efficiencyWeight + weather * weatherWeight + savings01 * savingsWeight) / wSum;

            // Penalise heavy overwatering — proxy via aggregate moisture > 92 in any zone.
            if (zoneManager != null)
            {
                var zones = zoneManager.Zones;
                for (int i = 0; i < zones.Count; i++)
                {
                    var z = zones[i];
                    if (z != null && z.cropCount > 0
                        && z.averageMoisture >= z.overwaterThreshold)
                    {
                        blended -= overwaterPenalty;
                        break;
                    }
                }
            }

            return Mathf.Clamp01(blended);
        }

        /// <summary>Colour-coding used by the score badge on the UI.</summary>
        public Color CurrentColor()
        {
            if (Score01 >= 0.80f) return new Color(0.30f, 0.85f, 0.55f, 1f);
            if (Score01 >= 0.55f) return new Color(0.95f, 0.78f, 0.25f, 1f);
            return new Color(0.92f, 0.30f, 0.25f, 1f);
        }

        /// <summary>Returns a friendly grade label A..D for use on the UI badge.</summary>
        public string Grade()
        {
            if (Score01 >= 0.90f) return "A+";
            if (Score01 >= 0.80f) return "A";
            if (Score01 >= 0.70f) return "B";
            if (Score01 >= 0.60f) return "C";
            if (Score01 >= 0.50f) return "D";
            return "E";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void ResetScore()
        {
            Score01 = Mathf.Clamp01(startingScore / 100f);
            OnScoreChanged?.Invoke(Score01);
        }

        public void SetReferences(IrrigationEfficiencySystem eff, WeatherOptimizationSystem wo,
            WaterSavingTracker saver, IrrigationZoneManager zm)
        {
            efficiencySystem    = eff;
            weatherOptimization = wo;
            waterSaver          = saver;
            zoneManager         = zm;
        }
    }
}
