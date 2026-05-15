using System.Collections;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Play-mode helper to guarantee you see water spraying from the pipes.
    ///
    /// Drop one on the SmartIrrigationHub (or anywhere in the scene). On
    /// <see cref="Start"/> it:
    ///   • Forces every irrigation zone into <see cref="IrrigationZoneMode.On"/>
    ///     so the visual feedback receives a non-zero flow rate.
    ///   • Re-applies the URP-safe water material to every sprinkler.
    ///   • Refreshes the <see cref="IrrigationVisualFeedback"/> cache so the
    ///     newly built spray layers from <see cref="IrrigationSprayBuilder"/>
    ///     are picked up.
    ///   • Optionally logs a one-line status report every couple of seconds
    ///     so you can confirm flow rates and particle counts in the Console.
    ///
    /// Once you've verified water works you can disable this component (or
    /// remove it) to let the normal Auto/On/Off logic drive the visuals.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Irrigation Spray Debug")]
    public class IrrigationSprayDebug : MonoBehaviour
    {
        [Header("Behaviour")]
        [Tooltip("On Play, forces every zone into On mode so water sprays immediately.")]
        [SerializeField] private bool forceAllZonesOnAtStart = true;

        [Tooltip("Re-apply the URP-safe water material to every sprinkler at Play start. " +
                 "Fixes invisible-particle problems caused by missing or broken shaders.")]
        [SerializeField] private bool reapplyParticleMaterial = true;

        [Tooltip("Verbose console logging — prints a status report every interval.")]
        [SerializeField] private bool verboseLogging = true;

        [Tooltip("How often (seconds) to log the status report.")]
        [SerializeField, Range(0.5f, 30f)] private float logIntervalSeconds = 4f;

        private SmartIrrigationTabletManager _manager;
        private IrrigationZoneManager        _zoneManager;
        private IrrigationVisualFeedback     _feedback;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // Wait one frame so the SmartIrrigationTabletManager has wired everything.
            yield return null;

            _manager     = SmartIrrigationTabletManager.Instance
                           ?? FindFirstObjectByType<SmartIrrigationTabletManager>();
            _zoneManager = _manager != null ? _manager.Zones : FindFirstObjectByType<IrrigationZoneManager>();
            _feedback    = _manager != null ? _manager.Visuals : FindFirstObjectByType<IrrigationVisualFeedback>();

            if (_zoneManager == null)
            {
                Debug.LogWarning("[SprayDebug] No IrrigationZoneManager found. Run " +
                                 "'Tools › Smart Farm › Setup Smart Irrigation Tablet' first.");
                yield break;
            }

            // Rebind scene visual roots if the editor rebuild orphaned them.
            _zoneManager.TryBindSceneVisualRoots();

            if (reapplyParticleMaterial) ReapplyMaterials();

            if (_feedback != null) _feedback.RefreshCache();

            if (forceAllZonesOnAtStart)
            {
                if (_manager != null) _manager.EnableAllZones();
                else
                {
                    for (int i = 0; i < _zoneManager.Zones.Count; i++)
                    {
                        var z = _zoneManager.Zones[i];
                        if (z != null) _zoneManager.SetZoneMode(z.id, IrrigationZoneMode.On);
                    }
                }
                Debug.Log("[SprayDebug] Forced every zone to ON. You should see water now.");
            }

            if (verboseLogging) StartCoroutine(LogStatusLoop());
        }

        private void ReapplyMaterials()
        {
            if (_zoneManager == null) return;
            int patched = 0;
            for (int i = 0; i < _zoneManager.Zones.Count; i++)
            {
                var z = _zoneManager.Zones[i];
                if (z == null || z.sprinklerRoot == null) continue;
                var systems = z.sprinklerRoot.GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    if (systems[s] == null) continue;
                    IrrigationSprayMaterial.ApplyTo(systems[s]);
                    patched++;
                }
            }
            Debug.Log($"[SprayDebug] Re-applied URP water material to {patched} particle system(s).");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Status logging
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator LogStatusLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.5f, logIntervalSeconds));
            while (this != null && this.enabled && Application.isPlaying)
            {
                yield return wait;
                LogStatusOnce();
            }
        }

        [ContextMenu("Log Status Now")]
        public void LogStatusOnce()
        {
            if (_zoneManager == null) _zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
            if (_zoneManager == null) { Debug.Log("[SprayDebug] No zone manager."); return; }

            var sb = new System.Text.StringBuilder(256);
            sb.Append("[SprayDebug] ");
            for (int i = 0; i < _zoneManager.Zones.Count; i++)
            {
                var z = _zoneManager.Zones[i];
                if (z == null) continue;
                int systems = z.sprinklerRoot != null
                    ? z.sprinklerRoot.GetComponentsInChildren<ParticleSystem>(true).Length
                    : 0;
                int alive = 0;
                if (z.sprinklerRoot != null)
                {
                    var arr = z.sprinklerRoot.GetComponentsInChildren<ParticleSystem>(true);
                    for (int j = 0; j < arr.Length; j++)
                        if (arr[j] != null) alive += arr[j].particleCount;
                }
                if (i > 0) sb.Append("  ·  ");
                sb.Append($"{z.displayName}: mode={z.mode}, flow={z.flowRate:F2}, " +
                          $"moist={z.averageMoisture:F0}%, systems={systems}, particles={alive}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
