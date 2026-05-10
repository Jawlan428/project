using System.Collections;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Glue layer that pushes irrigation outcomes into the live crop simulation.
    ///
    /// Effects applied per tick (host-only):
    ///   • Healthy moisture (zone moisture in [healthy..overwater]) → gentle health bonus.
    ///   • Dry moisture below low threshold              → small extra health drain so the
    ///                                                     crop visibly suffers when neglected.
    ///   • Overwatered moisture above overwater threshold → small health penalty for
    ///                                                     waterlogging.
    ///
    /// All values are tuned to be subtle so the crop simulation remains primarily
    /// driven by <see cref="CropGrowthController"/>; this just biases the result
    /// toward "good irrigation = healthier crops" feedback the player can feel.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Crop Growth Bridge")]
    public class CropGrowthBridge : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private IrrigationZoneManager zoneManager;
        [SerializeField] private GrowthManager         growthManager;

        [Header("Health Modifiers (per second)")]
        [Tooltip("Bonus health applied per second when zone moisture is in the healthy band.")]
        [SerializeField, Range(0f, 5f)] private float healthyBonusPerSecond = 1.2f;

        [Tooltip("Penalty per second when zone is dry (below low moisture threshold).")]
        [SerializeField, Range(0f, 5f)] private float dryPenaltyPerSecond = 0.6f;

        [Tooltip("Penalty per second when zone is over-watered.")]
        [SerializeField, Range(0f, 5f)] private float overwaterPenaltyPerSecond = 0.4f;

        [Tooltip("Tick interval (seconds). Smaller = smoother feedback, higher = cheaper.")]
        [SerializeField, Range(0.25f, 3f)] private float tickInterval = 0.5f;

        private Coroutine _tick;

        private bool ShouldRunSimulation => NetworkHelper.IsSimulationAuthority;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (zoneManager == null) zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
            if (growthManager == null) growthManager = GrowthManager.Instance ?? FindFirstObjectByType<GrowthManager>();
        }

        private void OnEnable()
        {
            _tick = StartCoroutine(TickLoop());
        }

        private void OnDisable()
        {
            if (_tick != null) StopCoroutine(_tick);
            _tick = null;
        }

        private IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(tickInterval);
            while (true)
            {
                yield return wait;
                if (!ShouldRunSimulation) continue;
                ApplyTickEffects(tickInterval);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Apply
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyTickEffects(float deltaTime)
        {
            if (zoneManager == null) return;
            if (growthManager == null) growthManager = GrowthManager.Instance ?? FindFirstObjectByType<GrowthManager>();
            if (growthManager == null) return;

            var zones = zoneManager.Zones;
            var crops = growthManager.GetAllCrops();
            if (zones == null || crops == null) return;

            for (int z = 0; z < zones.Count; z++)
            {
                var zone = zones[z];
                if (zone == null) continue;

                float healthDelta = ResolveHealthDeltaPerSecond(zone) * deltaTime;
                if (Mathf.Approximately(healthDelta, 0f)) continue;

                for (int i = 0; i < crops.Count; i++)
                {
                    var c = crops[i];
                    if (c == null || c.Data == null) continue;
                    if (c.Data.cropType != zone.cropType) continue;
                    if (c.CurrentStage == CropStage.Dead) continue;
                    c.ModifyHealth(healthDelta);
                }
            }
        }

        private float ResolveHealthDeltaPerSecond(IrrigationZone zone)
        {
            if (zone.cropCount == 0) return 0f;

            switch (zone.ClassifyMoisture())
            {
                case SoilMoistureState.Dry:         return -dryPenaltyPerSecond;
                case SoilMoistureState.Medium:      return 0f; // neutral
                case SoilMoistureState.Healthy:     return zone.isFlowing ? healthyBonusPerSecond : healthyBonusPerSecond * 0.5f;
                case SoilMoistureState.Overwatered: return -overwaterPenaltyPerSecond;
                default:                            return 0f;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetZoneManager(IrrigationZoneManager mgr)  => zoneManager = mgr;
        public void SetGrowthManager(GrowthManager mgr)        => growthManager = mgr;
    }
}
