using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm
{
    /// <summary>
    /// Severity classification for crop monitor alerts.
    /// </summary>
    public enum CropAlertLevel
    {
        Info,
        Warning,
        Critical,
        Success
    }

    /// <summary>
    /// A single notification raised by <see cref="CropMonitorAlertSystem"/>.
    /// </summary>
    [Serializable]
    public struct CropMonitorAlert
    {
        public string         id;
        public string         title;
        public string         message;
        public CropAlertLevel level;
        public DateTime       timestampUtc;
    }

    /// <summary>
    /// Watches every <see cref="CropMonitorReading"/> emitted by
    /// <see cref="CropGrowthMonitorManager"/> and raises contextual alerts:
    ///
    ///   • "Low Water Detected"      — water % crosses below threshold
    ///   • "Crop Health Critical"    — health % crosses below critical threshold
    ///   • "Harvest Ready"           — first crop reaches mature stage
    ///   • "Storm Damage Risk"       — weather changes to Storm
    ///
    /// All alerts are de-duplicated using a per-id cooldown so the same warning
    /// doesn't spam the popup UI.
    ///
    /// Subscribers (popup, audio, history etc.) listen via
    /// <see cref="OnAlertRaised"/>.
    /// </summary>
    [AddComponentMenu("SmartFarm/Crops/Crop Monitor Alert System")]
    public class CropMonitorAlertSystem : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private CropGrowthMonitorManager monitor;

        [Header("Thresholds")]
        [SerializeField, Range(0f, 100f), Tooltip("Below this water level, a Low Water alert is raised.")]
        private float lowWaterThreshold = 30f;

        [SerializeField, Range(0f, 100f), Tooltip("Below this health, a Critical Health alert is raised.")]
        private float criticalHealthThreshold = 25f;

        [SerializeField, Range(0f, 100f), Tooltip("Below this health, a Health Warning alert is raised.")]
        private float warningHealthThreshold = 55f;

        [Header("Cooldowns")]
        [SerializeField, Tooltip("Minimum seconds between repeats of the same alert id.")]
        private float duplicateCooldownSeconds = 12f;

        [Header("Optional Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   warningClip;
        [SerializeField] private AudioClip   criticalClip;
        [SerializeField] private AudioClip   successClip;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fires every time a new alert passes the de-duplication filter.</summary>
        public event Action<CropMonitorAlert> OnAlertRaised;

        /// <summary>Fires when an alert previously raised becomes resolved (e.g. water restored).</summary>
        public event Action<string> OnAlertResolved;

        // ── State ─────────────────────────────────────────────────────────────

        private readonly Dictionary<string, float> _lastRaisedAt = new();
        private readonly HashSet<string>           _activeIds    = new();
        private bool _hasLastReading;
        private CropMonitorReading _lastReading;

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (monitor == null)
                monitor = FindFirstObjectByType<CropGrowthMonitorManager>();
            if (monitor != null)
                monitor.OnReadingChanged += OnReadingChanged;
        }

        private void OnDisable()
        {
            if (monitor != null)
                monitor.OnReadingChanged -= OnReadingChanged;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Reading handler
        // ─────────────────────────────────────────────────────────────────────

        private void OnReadingChanged(CropMonitorReading reading)
        {
            // No crops in scene → nothing to alert about
            if (reading.sampleCount == 0)
            {
                _lastReading = reading;
                _hasLastReading = true;
                return;
            }

            // ── Storm Damage Risk ─────────────────────────────────────────────
            bool stormNow  = reading.weather == WeatherManager.WeatherType.Storm;
            bool stormPrev = _hasLastReading && _lastReading.weather == WeatherManager.WeatherType.Storm;
            if (stormNow)
            {
                Raise("storm_risk",
                    "Storm Damage Risk",
                    "Storm conditions active — crops may take damage. Consider protective irrigation.",
                    CropAlertLevel.Warning);
            }
            else if (stormPrev)
            {
                Resolve("storm_risk");
            }

            // ── Low Water Detected ────────────────────────────────────────────
            if (reading.waterPercent < lowWaterThreshold && !reading.isDead)
            {
                Raise("low_water",
                    "Low Water Detected",
                    $"Soil moisture at {reading.waterPercent:F0}% on {reading.displayName}. Trigger irrigation.",
                    CropAlertLevel.Warning);
            }
            else if (_hasLastReading && _lastReading.waterPercent < lowWaterThreshold)
            {
                Resolve("low_water");
            }

            // ── Crop Health Critical ──────────────────────────────────────────
            if (reading.healthPercent < criticalHealthThreshold && !reading.isDead)
            {
                Raise("health_critical",
                    "Crop Health Critical",
                    $"Health at {reading.healthPercent:F0}% on {reading.displayName}. Immediate action required.",
                    CropAlertLevel.Critical);
            }
            else if (reading.healthPercent < warningHealthThreshold && !reading.isDead)
            {
                Raise("health_warning",
                    "Health Warning",
                    $"Health declining ({reading.healthPercent:F0}%) on {reading.displayName}.",
                    CropAlertLevel.Warning);
                Resolve("health_critical");
            }
            else
            {
                Resolve("health_critical");
                Resolve("health_warning");
            }

            // ── Harvest Ready (rising-edge: only when newly mature) ───────────
            bool readyNow  = reading.isHarvestReady;
            bool readyPrev = _hasLastReading && _lastReading.isHarvestReady;
            if (readyNow && !readyPrev)
            {
                Raise("harvest_ready",
                    "Harvest Ready",
                    $"{reading.displayName} reached the Mature stage. Tap Harvest to collect yield.",
                    CropAlertLevel.Success);
            }
            else if (!readyNow && readyPrev)
            {
                Resolve("harvest_ready");
            }

            _lastReading    = reading;
            _hasLastReading = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Raise / resolve
        // ─────────────────────────────────────────────────────────────────────

        private void Raise(string id, string title, string message, CropAlertLevel level)
        {
            float now = Time.unscaledTime;
            if (_lastRaisedAt.TryGetValue(id, out float prev) &&
                now - prev < duplicateCooldownSeconds &&
                _activeIds.Contains(id))
            {
                return;
            }

            _lastRaisedAt[id] = now;
            _activeIds.Add(id);

            var alert = new CropMonitorAlert
            {
                id           = id,
                title        = title,
                message      = message,
                level        = level,
                timestampUtc = DateTime.UtcNow
            };

            OnAlertRaised?.Invoke(alert);
            EventLogger.LogEvent($"[Crop Monitor] {title}: {message}");
            PlayCue(level);
        }

        private void Resolve(string id)
        {
            if (!_activeIds.Remove(id)) return;
            OnAlertResolved?.Invoke(id);
        }

        private void PlayCue(CropAlertLevel level)
        {
            if (audioSource == null) return;
            AudioClip clip = level switch
            {
                CropAlertLevel.Critical => criticalClip,
                CropAlertLevel.Warning  => warningClip,
                CropAlertLevel.Success  => successClip,
                _                       => null
            };
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        /// <summary>Returns colour codes that match the popup design.</summary>
        public static Color ColorFor(CropAlertLevel level) => level switch
        {
            CropAlertLevel.Critical => new Color(0.95f, 0.30f, 0.30f, 1f),
            CropAlertLevel.Warning  => new Color(0.95f, 0.72f, 0.20f, 1f),
            CropAlertLevel.Success  => new Color(0.20f, 0.92f, 0.55f, 1f),
            _                       => new Color(0.30f, 0.70f, 0.95f, 1f)
        };
    }
}
