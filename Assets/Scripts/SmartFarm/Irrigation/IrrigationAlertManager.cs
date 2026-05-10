using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm.Irrigation
{
    /// <summary>
    /// Severity levels for irrigation tablet alerts.
    /// </summary>
    public enum IrrigationAlertLevel
    {
        Info     = 0,
        Warning  = 1,
        Critical = 2,
        Success  = 3
    }

    /// <summary>
    /// One alert entry exposed to the UI.
    /// </summary>
    [Serializable]
    public struct IrrigationAlert
    {
        public string id;
        public string title;
        public string message;
        public IrrigationAlertLevel level;
        public DateTime timestampUtc;

        public Color GetColor() => level switch
        {
            IrrigationAlertLevel.Critical => new Color(0.95f, 0.30f, 0.30f, 1f),
            IrrigationAlertLevel.Warning  => new Color(0.95f, 0.72f, 0.20f, 1f),
            IrrigationAlertLevel.Success  => new Color(0.30f, 0.85f, 0.55f, 1f),
            _                             => new Color(0.30f, 0.70f, 0.95f, 1f)
        };
    }

    /// <summary>
    /// Watches zone snapshots + weather state and raises de-duplicated alerts:
    ///   • "Low Moisture Detected"      — any zone below low threshold.
    ///   • "Overwatering Risk"          — any zone above overwater threshold.
    ///   • "Storm Irrigation Disabled"  — weather is Storm.
    ///   • "Crop Requires Water"        — health falling below safe band.
    ///
    /// Alerts are surfaced through <see cref="OnAlertRaised"/> which the tablet UI
    /// and Crop Growth Monitor (via <see cref="CropMonitorAlertSystem"/>) listen
    /// to. Resolved alerts fire <see cref="OnAlertResolved"/> so banners can clear.
    /// </summary>
    [AddComponentMenu("SmartFarm/Irrigation/Irrigation Alert Manager")]
    public class IrrigationAlertManager : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private IrrigationZoneManager zoneManager;
        [SerializeField] private WeatherIntegrationSystem weatherSystem;

        [Header("Cooldowns")]
        [SerializeField, Tooltip("Minimum seconds between repeated alerts of the same id.")]
        private float duplicateCooldownSeconds = 12f;

        [Header("Optional Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   warningClip;
        [SerializeField] private AudioClip   criticalClip;
        [SerializeField] private AudioClip   successClip;

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>Fired when an alert passes the cooldown filter (UI banner / popup).</summary>
        public event Action<IrrigationAlert> OnAlertRaised;

        /// <summary>Fired when an active alert id resolves (e.g. moisture restored).</summary>
        public event Action<string> OnAlertResolved;

        /// <summary>Fired any time the active alerts list changes (UI list updates).</summary>
        public event Action<IReadOnlyList<IrrigationAlert>> OnActiveListChanged;

        // ── Public State ─────────────────────────────────────────────────────

        public IReadOnlyList<IrrigationAlert> ActiveAlerts => _activeAlerts;

        // ── Private ──────────────────────────────────────────────────────────

        private readonly Dictionary<string, float> _lastRaisedAt = new();
        private readonly List<IrrigationAlert>     _activeAlerts = new();
        private readonly HashSet<string>           _activeIds    = new();

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (zoneManager == null) zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
            if (weatherSystem == null) weatherSystem = FindFirstObjectByType<WeatherIntegrationSystem>();
        }

        private void OnEnable()
        {
            if (zoneManager == null) zoneManager = FindFirstObjectByType<IrrigationZoneManager>();
            if (weatherSystem == null) weatherSystem = FindFirstObjectByType<WeatherIntegrationSystem>();

            if (zoneManager != null)
                zoneManager.OnZonesChanged += HandleZonesChanged;
            if (weatherSystem != null)
                weatherSystem.OnWeatherNotice += HandleWeatherNotice;
        }

        private void OnDisable()
        {
            if (zoneManager != null)
                zoneManager.OnZonesChanged -= HandleZonesChanged;
            if (weatherSystem != null)
                weatherSystem.OnWeatherNotice -= HandleWeatherNotice;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Detection
        // ─────────────────────────────────────────────────────────────────────

        private void HandleZonesChanged(IReadOnlyList<IrrigationZoneSnapshot> snapshots)
        {
            if (snapshots == null) return;

            bool anyDry         = false;
            bool anyOverwater   = false;
            bool anyHealthLow   = false;
            string driestZone   = null;
            string overWetZone  = null;
            string sickZone     = null;
            float  lowestMoist  = 100f;
            float  highestMoist = 0f;
            float  lowestHealth = 100f;

            for (int i = 0; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                if (s.cropCount == 0) continue;

                if (s.moistureState == SoilMoistureState.Dry)
                {
                    anyDry = true;
                    if (s.averageMoisture < lowestMoist)
                    {
                        lowestMoist = s.averageMoisture;
                        driestZone  = s.displayName;
                    }
                }
                if (s.moistureState == SoilMoistureState.Overwatered)
                {
                    anyOverwater = true;
                    if (s.averageMoisture > highestMoist)
                    {
                        highestMoist = s.averageMoisture;
                        overWetZone  = s.displayName;
                    }
                }
                if (s.averageHealth < 45f)
                {
                    anyHealthLow = true;
                    if (s.averageHealth < lowestHealth)
                    {
                        lowestHealth = s.averageHealth;
                        sickZone     = s.displayName;
                    }
                }
            }

            if (anyDry)
                Raise("low_moisture", "Low Moisture Detected",
                      $"{driestZone}: soil at {lowestMoist:F0}%. Trigger irrigation.",
                      IrrigationAlertLevel.Warning);
            else
                Resolve("low_moisture");

            if (anyOverwater)
                Raise("overwater", "Overwatering Risk",
                      $"{overWetZone}: soil at {highestMoist:F0}%. Reduce flow.",
                      IrrigationAlertLevel.Warning);
            else
                Resolve("overwater");

            if (anyHealthLow)
                Raise("crop_requires_water", "Crop Requires Water",
                      $"{sickZone}: crop health at {lowestHealth:F0}%. Increase irrigation.",
                      IrrigationAlertLevel.Critical);
            else
                Resolve("crop_requires_water");
        }

        private void HandleWeatherNotice(WeatherManager.WeatherType weather, string notice)
        {
            if (weather == WeatherManager.WeatherType.Storm)
            {
                Raise("storm_disabled", "Storm Irrigation Disabled",
                      "Storm detected — irrigation paused for safety. Crops may take damage.",
                      IrrigationAlertLevel.Critical);
            }
            else
            {
                Resolve("storm_disabled");
                if (weather == WeatherManager.WeatherType.Rainy)
                    Raise("rain_top_up", "Natural Rain — Reduced Flow",
                          notice ?? "Rain reduces irrigation demand automatically.",
                          IrrigationAlertLevel.Info);
                else
                    Resolve("rain_top_up");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Raise / Resolve
        // ─────────────────────────────────────────────────────────────────────

        private void Raise(string id, string title, string message, IrrigationAlertLevel level)
        {
            float now = Time.unscaledTime;
            bool alreadyActive = _activeIds.Contains(id);

            if (alreadyActive
                && _lastRaisedAt.TryGetValue(id, out float prev)
                && now - prev < duplicateCooldownSeconds)
            {
                // Update existing entry's text/timestamp without spamming events
                UpdateExisting(id, title, message, level);
                return;
            }

            _lastRaisedAt[id] = now;

            var alert = new IrrigationAlert
            {
                id           = id,
                title        = title,
                message      = message,
                level        = level,
                timestampUtc = DateTime.UtcNow
            };

            if (!alreadyActive)
            {
                _activeIds.Add(id);
                _activeAlerts.Insert(0, alert);
            }
            else
            {
                UpdateExisting(id, title, message, level);
            }

            OnAlertRaised?.Invoke(alert);
            OnActiveListChanged?.Invoke(_activeAlerts);
            EventLogger.LogEvent($"[Irrigation] {title}: {message}");
            PlayCue(level);
        }

        private void UpdateExisting(string id, string title, string message, IrrigationAlertLevel level)
        {
            for (int i = 0; i < _activeAlerts.Count; i++)
            {
                if (_activeAlerts[i].id != id) continue;
                _activeAlerts[i] = new IrrigationAlert
                {
                    id           = id,
                    title        = title,
                    message      = message,
                    level        = level,
                    timestampUtc = DateTime.UtcNow
                };
                OnActiveListChanged?.Invoke(_activeAlerts);
                return;
            }
        }

        private void Resolve(string id)
        {
            if (!_activeIds.Remove(id)) return;
            for (int i = _activeAlerts.Count - 1; i >= 0; i--)
            {
                if (_activeAlerts[i].id == id)
                {
                    _activeAlerts.RemoveAt(i);
                    break;
                }
            }
            OnAlertResolved?.Invoke(id);
            OnActiveListChanged?.Invoke(_activeAlerts);
        }

        private void PlayCue(IrrigationAlertLevel level)
        {
            if (audioSource == null) return;
            AudioClip clip = level switch
            {
                IrrigationAlertLevel.Critical => criticalClip,
                IrrigationAlertLevel.Warning  => warningClip,
                IrrigationAlertLevel.Success  => successClip,
                _                             => null
            };
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Wiring helpers
        // ─────────────────────────────────────────────────────────────────────

        public void SetZoneManager(IrrigationZoneManager mgr)        => zoneManager   = mgr;
        public void SetWeatherSystem(WeatherIntegrationSystem sys)   => weatherSystem = sys;
    }
}
